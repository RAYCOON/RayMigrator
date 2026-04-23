// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Main migration service implementation.
/// Handles MigrateUp, MigrateDown, and shared rollback execution.
/// </summary>
public class MigrationService : IMigrationService
{
    private readonly ILogger<MigrationService> _logger;
    private readonly IOptions<RayMigratorOptions> _options;
    private readonly TemplateExecutor _templateExecutor;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICliToolExecutor _cliToolExecutor;

    public MigrationService(
        ILogger<MigrationService> logger,
        IOptions<RayMigratorOptions> options,
        TemplateExecutor templateExecutor,
        IMigrationContextAccessor ctxAccessor,
        IServiceProvider serviceProvider,
        ICliToolExecutor cliToolExecutor)
    {
        _logger = logger;
        _options = options;
        _templateExecutor = templateExecutor;
        _ctxAccessor = ctxAccessor;
        _serviceProvider = serviceProvider;
        _cliToolExecutor = cliToolExecutor;
    }

    #region MigrateUp

    public async Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request)
    {
        var startTime = DateTime.UtcNow;
        var runId = Guid.NewGuid();
        var migrationResults = new List<MigrationFileResult>();
        int successfulMigrations = 0;
        int failedMigrations = 0;

        try
        {
            _logger.LogInformation("Executing Migrate-Up for product {Product} in environment {Environment}",
                request.ProductAlias, request.Environment);

            // Validate context matches request
            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Phase 1: Initialization ---
            _ctxAccessor.Current.MigrationState.MigrationRunResult = MigrationRunResult.Running;
            _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;
            _logger.LogDebug("State initialized: MigrationRunResult={MigrationRunResult}, MigrationOperation={MigrationOperation}",
                MigrationRunResult.Running, MigrationOperation.MigrateUp);

            if (request.RunMode.ShouldWriteRepository())
            {
                await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
                await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
                await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

                // Check for interrupted migrations
                var interruptedMigration = await Task.Run(() => _templateExecutor.RepositoryMigrationGetInterrupted());
                if (interruptedMigration != null)
                {
                    _logger.LogWarning(
                        "Interrupted migration detected: MigrationId={MigrationId} file {Filename} at block {BlocksMigrated}/{BlocksTotal}",
                        interruptedMigration.MigrationId, interruptedMigration.Filename,
                        interruptedMigration.BlocksMigrated, interruptedMigration.BlocksTotal);
                }

                // Create migration run with settings snapshot (auto-fixes orphaned runs if needed)
                var settingsJson = BuildMigrationRunSettingsJson(_ctxAccessor.Current);
                _logger.LogTrace("MigrationRun settings snapshot:\n{SettingsJson}", settingsJson);
                await RepositoryMigrationRunInsertWithAutoFix(settingsJson);
            }

            // --- Phase 2: File Discovery & Preparation ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
            var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);

            if (migrationFiles.Count == 0)
            {
                _logger.LogInformation("No migration files found for product {Product}", request.ProductAlias);
                if (request.RunMode.ShouldWriteRepository())
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                }
                return new MigrationOperationResult
                {
                    Success = true,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateUp,
                    Result = MigrationRunResult.Ok,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { "No migration files found to execute" }
                };
            }

            // Query existing migrations from repository and filter.
            // Always query with MigrationRunMode.Migrate to read actual Migrate-mode records,
            // even when running in Simulate mode (which no longer writes its own records).
            List<MigrationFileInfo> filesToMigrate;
            List<MigrationRecord> existingRecords;
            if (request.RunMode.ShouldReadRepository())
            {
                try
                {
                    existingRecords = await Task.Run(() =>
                        _templateExecutor.RepositoryMigrationSelect(MigrationRunMode.Migrate));
                    filesToMigrate = FilterAlreadyMigratedFiles(migrationFiles, existingRecords, productOptions);
                }
                catch (Exception ex) when (!request.RunMode.ShouldWriteRepository())
                {
                    // Repository doesn't exist yet — treat all files as pending
                    _logger.LogInformation("Repository not accessible in {RunMode} mode, simulating as if no migrations have been applied. Reason: {ErrorMessage}",
                        request.RunMode, ex.Message);
                    existingRecords = new List<MigrationRecord>();
                    filesToMigrate = migrationFiles;
                }
            }
            else
            {
                existingRecords = new List<MigrationRecord>();
                filesToMigrate = migrationFiles; // Validate: process all files
            }

            // Filter by target release if specified
            filesToMigrate = FilterByTargetRelease(filesToMigrate, request.TargetReleaseVersion);

            // Filter by target group if specified
            ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
            filesToMigrate = FilterByTargetGroups(filesToMigrate, request.TargetGroupAliases);

            // Check for out-of-order migrations
            var outOfOrderFiles = DetectOutOfOrderFiles(filesToMigrate, existingRecords);
            if (outOfOrderFiles.Count > 0)
            {
                if (!request.AllowOutOfOrder)
                {
                    string highestMigratedRelease = existingRecords
                        .Where(r => r.MigrationStatusId == MigrationStatus.Migrated)
                        .OrderByDescending(r => r.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
                        .Select(r => r.ReleaseVersion)
                        .First();

                    _logger.LogError(
                        "Out-of-order migrations detected but --allow-out-of-order not specified. {Count} file(s) from releases before '{HighestRelease}' not applied. Aborting.",
                        outOfOrderFiles.Count, highestMigratedRelease);

                    throw new InvalidOperationException(
                        $"Out-of-order migrations detected: {outOfOrderFiles.Count} file(s) from releases before '{highestMigratedRelease}' " +
                        $"have not been applied yet. Use --allow-out-of-order to execute them.");
                }

                _logger.LogWarning(
                    "Out-of-order migration: executing {Count} file(s) from older releases",
                    outOfOrderFiles.Count);
            }

            if (filesToMigrate.Count == 0)
            {
                _logger.LogInformation("All migration files already applied for product {Product}", request.ProductAlias);
                if (request.RunMode.ShouldWriteRepository())
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                }
                return new MigrationOperationResult
                {
                    Success = true,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateUp,
                    Result = MigrationRunResult.Ok,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { "All migrations already applied" }
                };
            }

            _logger.LogInformation("Found {Count} migration files to execute for product {Product}",
                filesToMigrate.Count, request.ProductAlias);

            // OPT-3: Log safety warnings for dangerous configuration combinations
            LogMigrationSafetyWarnings(filesToMigrate, productOptions);

            // --- Phase 3: Execute Migrations ---
            // Track successfully migrated records for potential rollback
            var successfullyMigratedRecords = new List<(MigrationFileInfo File, int MigrationId, string TargetAlias)>();
            string? lastErrorMessage = null;

            // Release-based ordering: Release → TargetGroup → Targets
            var orderedReleases = filesToMigrate
                .Select(f => f.ReleaseVersion)
                .Distinct()
                .ToList();

            var filesByReleaseAndTG = filesToMigrate
                .GroupBy(f => (f.ReleaseVersion, f.TargetGroupAlias))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Load migsettings for TargetGroupMigrationOrder resolution
            var migSettingsForOrder = LoadMigSettingsDefaults(
                productOptions.MigrationFilesRootDirectory!, request.Environment,
                productOptions.MigrationFilesExtension ?? "sql");

            foreach (var release in orderedReleases)
            {
                // Resolve TargetGroup execution order for this release
                var releaseDir = Path.Combine(productOptions.MigrationFilesRootDirectory!, release);
                var tgExecutionOrder = ResolveTargetGroupMigrationOrder(
                    releaseDir, productOptions, migSettingsForOrder, request.TargetGroupMigrationOrder);
                var orderedTargetGroups = tgExecutionOrder != null
                    ? ValidateAndReorderTargetGroups(tgExecutionOrder, productOptions.TargetGroups!, "Migrate-Up")
                    : productOptions.TargetGroups!;

                foreach (var targetGroup in orderedTargetGroups)
                {
                    var key = (release, targetGroup.Alias!);
                    if (!filesByReleaseAndTG.TryGetValue(key, out var tgFiles) || tgFiles.Count == 0)
                        continue;

                    // Dispatch to Simultaneously or Successively
                    var result = targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously
                        ? await ExecuteTargetGroupSimultaneously(
                            tgFiles, targetGroup, productOptions, request,
                            successfullyMigratedRecords, migrationResults, existingRecords)
                        : await ExecuteTargetGroupSuccessively(
                            tgFiles, targetGroup, productOptions, request,
                            successfullyMigratedRecords, migrationResults, existingRecords);

                    successfulMigrations += result.SuccessCount;
                    failedMigrations += result.FailCount;

                    if (!result.Success)
                    {
                        lastErrorMessage = result.ErrorMessage;

                        if (request.RunMode.ShouldWriteRepository())
                        {
                            // Both modes: error aborts entire run
                            await HandleMigrationError(
                                productOptions, result.FailedFile!, result.FailedMigrationId,
                                successfullyMigratedRecords);

                            await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Error));
                        }

                        return new MigrationOperationResult
                        {
                            Success = false,
                            RunId = runId,
                            ProductAlias = request.ProductAlias,
                            Environment = request.Environment,
                            Operation = MigrationOperation.MigrateUp,
                            Result = MigrationRunResult.Error,
                            TotalMigrations = successfulMigrations + failedMigrations,
                            SuccessfulMigrations = successfulMigrations,
                            FailedMigrations = failedMigrations,
                            Duration = DateTime.UtcNow - startTime,
                            ErrorMessage = lastErrorMessage,
                            MigrationResults = migrationResults,
                            Messages = new List<string> { $"Migration failed at file {result.FailedFile!.Filename}: {lastErrorMessage}" }
                        };
                    }
                }
            }

            // --- Phase 5: Finalization ---
            var finalResult = failedMigrations > 0 ? MigrationRunResult.Error : MigrationRunResult.Ok;
            if (request.RunMode.ShouldWriteRepository())
            {
                await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(finalResult));
            }

            if (failedMigrations > 0)
            {
                _logger.LogError(
                    "Migrate-Up completed with errors for product {Product}: {Successful} succeeded, {Failed} failed",
                    request.ProductAlias, successfulMigrations, failedMigrations);

                return new MigrationOperationResult
                {
                    Success = false,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateUp,
                    Result = MigrationRunResult.Error,
                    TotalMigrations = successfulMigrations + failedMigrations,
                    SuccessfulMigrations = successfulMigrations,
                    FailedMigrations = failedMigrations,
                    CurrentRelease = filesToMigrate.Last().ReleaseVersion,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = lastErrorMessage,
                    MigrationResults = migrationResults,
                    Messages = new List<string> { $"Migration completed with errors: {successfulMigrations} succeeded, {failedMigrations} failed" }
                };
            }

            _logger.LogInformation("Migrate-Up completed successfully for product {Product} with {Count} migrations",
                request.ProductAlias, successfulMigrations);

            return new MigrationOperationResult
            {
                Success = true,
                RunId = runId,
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                Operation = MigrationOperation.MigrateUp,
                Result = MigrationRunResult.Ok,
                TotalMigrations = successfulMigrations,
                SuccessfulMigrations = successfulMigrations,
                FailedMigrations = 0,
                CurrentRelease = filesToMigrate.Last().ReleaseVersion,
                Duration = DateTime.UtcNow - startTime,
                MigrationResults = migrationResults,
                Messages = new List<string> { $"Successfully executed {successfulMigrations} migration(s)" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Migrate-Up for product {Product}", request.ProductAlias);

            // Try to finalize run with error
            if (request.RunMode.ShouldWriteRepository())
            {
                try
                {
                    if (_ctxAccessor.Current.MigrationState.MigrationRunId > 0)
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Error));
                    }
                }
                catch (Exception finalizeEx)
                {
                    _logger.LogWarning(finalizeEx,
                        "Failed to finalize MigrationRun {RunId} with error status",
                        _ctxAccessor.Current.MigrationState.MigrationRunId);
                }
            }

            return new MigrationOperationResult
            {
                Success = false,
                RunId = runId,
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                Operation = MigrationOperation.MigrateUp,
                Result = MigrationRunResult.Error,
                TotalMigrations = successfulMigrations + failedMigrations,
                SuccessfulMigrations = successfulMigrations,
                FailedMigrations = failedMigrations,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                MigrationResults = migrationResults,
                Messages = new List<string> { $"Migration failed: {ex.Message}" }
            };
        }
    }

    #endregion MigrateUp

    #region TargetGroup Execution

    /// <summary>
    /// Executes migrations in Simultaneously order: foreach file → foreach target.
    /// An error aborts the entire TargetGroup unless MigrationErrorAction is Ignore,
    /// in which case the file is marked as Failed and execution continues.
    /// </summary>
    internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupSimultaneously(
        List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
        ProductOptions productOptions, MigrateUpRequest request,
        List<(MigrationFileInfo File, int MigrationId, string TargetAlias)> successfullyMigratedRecords,
        List<MigrationFileResult> migrationResults,
        List<MigrationRecord> existingRecords)
    {
        _logger.LogDebug("Executing TargetGroup {TargetGroup} in Simultaneously mode ({FileCount} files, {TargetCount} targets)",
            targetGroupOptions.Alias, files.Count, targetGroupOptions.Targets!.Count);
        var result = new TargetGroupExecutionResult();

        foreach (var file in files)
        {
            var fileStartTime = DateTime.UtcNow;
            var errorAction = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;
            bool ignoreErrors = errorAction == MigrationErrorAction.Ignore;

            try
            {
                bool fileHadBlockFailures = false;

                foreach (var targetOptions in targetGroupOptions.Targets!)
                {
                    // OPT-1: Check if a previous run completed all blocks but wasn't finalized
                    if (request.RunMode.ShouldWriteRepository())
                    {
                        int finalizedId = TryFinalizeCompletedMigration(file, targetOptions.Alias!, existingRecords);
                        if (finalizedId > 0)
                        {
                            successfullyMigratedRecords.Add((file, finalizedId, targetOptions.Alias!));
                            _logger.LogInformation(
                                "Migration recovered | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} (finalized from previous incomplete run)",
                                productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, finalizedId);
                            continue; // Skip to next target - this file+target is already done
                        }
                    }

                    // Look up existing record for this file+target (e.g. archived record to reuse)
                    var existingRecord = existingRecords.FirstOrDefault(r =>
                        r.Filename == file.Filename &&
                        r.ReleaseVersion == file.ReleaseVersion &&
                        r.TargetGroupAlias == file.TargetGroupAlias &&
                        r.TargetAlias == targetOptions.Alias);
                    int existingMigrationId = existingRecord?.Id ?? 0;

                    int migrationId = 0;
                    if (request.RunMode.ShouldWriteRepository())
                    {
                        migrationId = await Task.Run(() => _templateExecutor.RepositoryMigrationInsert(
                            existingMigrationId,
                            file.Filename,
                            file.ReleaseVersion,
                            file.TargetGroupAlias,
                            targetOptions.Alias!,
                            file.FileOrderId,
                            file.FileUpHash,
                            file.FileUpConfigHash,
                            file.FileUpBlocksHash,
                            file.FileUpBlocksTotal,
                            file.FileUpConfigJson,
                            file.MigrateDownFileExists));

                        if (existingMigrationId > 0)
                            migrationId = existingMigrationId;
                    }

                    _ctxAccessor.Current.MigrationState.MigrationId = migrationId;
                    _ctxAccessor.Current.MigrationState.ReleaseVersionFromFileNameWithPath = file.ReleaseVersion;
                    _ctxAccessor.Current.MigrationState.FilenameWithRelativePath = file.FilenameWithRelativePath;
                    _ctxAccessor.Current.MigrationState.FileOrderId = file.FileOrderId;
                    _ctxAccessor.Current.MigrationState.TargetGroupAlias = file.TargetGroupAlias;
                    _ctxAccessor.Current.MigrationState.TargetAlias = targetOptions.Alias!;

                    // Check if this file+target can resume from a previous partial execution
                    int startFromBlock = FindResumableBlock(file, targetOptions.Alias!, existingRecords);

                    // Branch: CLI tool or DAL execution
                    int succeededBlocks, failedBlocks;
                    bool atomicCommitCompleted;
                    string? resolvedCliAlias = ResolveUseCliToolAlias(file, targetOptions);
                    if (resolvedCliAlias != null)
                    {
                        var cliTool = GetCliToolByAlias(resolvedCliAlias);
                        (succeededBlocks, failedBlocks) = await ExecuteWithCliTool(
                            file, targetGroupOptions, targetOptions, migrationId, request.RunMode, cliTool);
                        atomicCommitCompleted = false;
                    }
                    else
                    {
                        (succeededBlocks, failedBlocks, atomicCommitCompleted) = await ExecuteSqlBlocks(
                            file, targetGroupOptions, targetOptions, migrationId, request.RunMode,
                            ignoreBlockErrors: ignoreErrors,
                            startFromBlock: startFromBlock);
                    }

                    if (failedBlocks > 0)
                    {
                        // Block-level failures with Ignore: mark as Failed, skip remaining targets for this file
                        fileHadBlockFailures = true;

                        _logger.LogWarning(
                            "MigrationErrorAction=Ignore: {FailedBlocks}/{TotalBlocks} block(s) failed in {Filename} on target {Target}. Skipping remaining targets for this file.",
                            failedBlocks, file.FileUpBlocksTotal, file.Filename, targetOptions.Alias);

                        if (request.RunMode.ShouldWriteRepository())
                        {
                            await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                                migrationId, MigrationStatus.Failed, _ctxAccessor.Current.MigrationState.FileBlockId));
                        }

                        break; // Skip remaining targets for this file
                    }

                    if (!atomicCommitCompleted && request.RunMode.ShouldWriteRepository())
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                            migrationId, MigrationStatus.Migrated, file.FileUpBlocksTotal));
                    }

                    successfullyMigratedRecords.Add((file, migrationId, targetOptions.Alias!));

                    _logger.LogInformation(
                        "Migration successful | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlocks: {SqlBlocksTotal}",
                        productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, migrationId, file.FileUpBlocksTotal);
                }

                if (fileHadBlockFailures)
                {
                    result.FailCount++;
                    migrationResults.Add(new MigrationFileResult
                    {
                        FileName = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroup = file.TargetGroupAlias,
                        Success = false,
                        ErrorMessage = $"MigrationErrorAction=Ignore: One or more SQL blocks failed in {file.Filename}",
                        ExecutedAt = fileStartTime,
                        Duration = DateTime.UtcNow - fileStartTime
                    });
                    continue; // Continue to next file
                }

                result.SuccessCount++;
                migrationResults.Add(new MigrationFileResult
                {
                    FileName = file.Filename,
                    ReleaseVersion = file.ReleaseVersion,
                    TargetGroup = file.TargetGroupAlias,
                    Success = true,
                    ExecutedAt = fileStartTime,
                    Duration = DateTime.UtcNow - fileStartTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Migration FAILED | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlock: {SqlBlock}/{SqlBlocksTotal}",
                    productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, _ctxAccessor.Current.MigrationState.TargetAlias, file.Filename, _ctxAccessor.Current.MigrationState.MigrationId, _ctxAccessor.Current.MigrationState.FileBlockId, file.FileUpBlocksTotal);

                try
                {
                    if (request.RunMode.ShouldWriteRepository())
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                            _ctxAccessor.Current.MigrationState.MigrationId, MigrationStatus.Failed,
                            _ctxAccessor.Current.MigrationState.FileBlockId));
                    }
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx,
                        "Failed to update migration record {MigrationId} ({Filename}) for error state",
                        _ctxAccessor.Current.MigrationState.MigrationId, file.Filename);
                }

                if (ignoreErrors)
                {
                    // Ignore: log warning, mark as Failed, continue to next file
                    _logger.LogWarning(
                        "MigrationErrorAction=Ignore: Exception in {Filename} on target {Target}. Continuing with next file.",
                        file.Filename, _ctxAccessor.Current.MigrationState.TargetAlias);

                    result.FailCount++;
                    migrationResults.Add(new MigrationFileResult
                    {
                        FileName = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroup = file.TargetGroupAlias,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ExecutedAt = fileStartTime,
                        Duration = DateTime.UtcNow - fileStartTime
                    });
                    continue; // Continue to next file
                }

                // Non-Ignore: abort this TargetGroup immediately — caller handles rollback
                result.FailCount++;
                result.Success = false;
                result.FailedFile = file;
                result.FailedMigrationId = _ctxAccessor.Current.MigrationState.MigrationId;
                result.ErrorMessage = ex.Message;

                migrationResults.Add(new MigrationFileResult
                {
                    FileName = file.Filename,
                    ReleaseVersion = file.ReleaseVersion,
                    TargetGroup = file.TargetGroupAlias,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutedAt = fileStartTime,
                    Duration = DateTime.UtcNow - fileStartTime
                });

                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Executes migrations in Successively order: foreach target → foreach file.
    /// An error aborts the entire TargetGroup unless MigrationErrorAction is Ignore,
    /// in which case the file is marked as Failed and execution continues.
    /// </summary>
    internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupSuccessively(
        List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
        ProductOptions productOptions, MigrateUpRequest request,
        List<(MigrationFileInfo File, int MigrationId, string TargetAlias)> successfullyMigratedRecords,
        List<MigrationFileResult> migrationResults,
        List<MigrationRecord> existingRecords)
    {
        _logger.LogDebug("Executing TargetGroup {TargetGroup} in Successively mode ({FileCount} files, {TargetCount} targets)",
            targetGroupOptions.Alias, files.Count, targetGroupOptions.Targets!.Count);
        var result = new TargetGroupExecutionResult();

        foreach (var targetOptions in targetGroupOptions.Targets!)
        {
            foreach (var file in files)
            {
                var fileStartTime = DateTime.UtcNow;
                var errorAction = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;
                bool ignoreErrors = errorAction == MigrationErrorAction.Ignore;

                try
                {
                    // OPT-1: Check if a previous run completed all blocks but wasn't finalized
                    if (request.RunMode.ShouldWriteRepository())
                    {
                        int finalizedId = TryFinalizeCompletedMigration(file, targetOptions.Alias!, existingRecords);
                        if (finalizedId > 0)
                        {
                            successfullyMigratedRecords.Add((file, finalizedId, targetOptions.Alias!));
                            _logger.LogInformation(
                                "Migration recovered | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} (finalized from previous incomplete run)",
                                productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, finalizedId);

                            result.SuccessCount++;
                            migrationResults.Add(new MigrationFileResult
                            {
                                FileName = file.Filename,
                                ReleaseVersion = file.ReleaseVersion,
                                TargetGroup = file.TargetGroupAlias,
                                Success = true,
                                ExecutedAt = fileStartTime,
                                Duration = DateTime.UtcNow - fileStartTime
                            });
                            continue; // Skip to next file - this file+target is already done
                        }
                    }

                    // Look up existing record for this file+target (e.g. archived record to reuse)
                    var existingRecord = existingRecords.FirstOrDefault(r =>
                        r.Filename == file.Filename &&
                        r.ReleaseVersion == file.ReleaseVersion &&
                        r.TargetGroupAlias == file.TargetGroupAlias &&
                        r.TargetAlias == targetOptions.Alias);
                    int existingMigrationId = existingRecord?.Id ?? 0;

                    int migrationId = 0;
                    if (request.RunMode.ShouldWriteRepository())
                    {
                        migrationId = await Task.Run(() => _templateExecutor.RepositoryMigrationInsert(
                            existingMigrationId,
                            file.Filename,
                            file.ReleaseVersion,
                            file.TargetGroupAlias,
                            targetOptions.Alias!,
                            file.FileOrderId,
                            file.FileUpHash,
                            file.FileUpConfigHash,
                            file.FileUpBlocksHash,
                            file.FileUpBlocksTotal,
                            file.FileUpConfigJson,
                            file.MigrateDownFileExists));

                        if (existingMigrationId > 0)
                            migrationId = existingMigrationId;
                    }

                    _ctxAccessor.Current.MigrationState.MigrationId = migrationId;
                    _ctxAccessor.Current.MigrationState.ReleaseVersionFromFileNameWithPath = file.ReleaseVersion;
                    _ctxAccessor.Current.MigrationState.FilenameWithRelativePath = file.FilenameWithRelativePath;
                    _ctxAccessor.Current.MigrationState.FileOrderId = file.FileOrderId;
                    _ctxAccessor.Current.MigrationState.TargetGroupAlias = file.TargetGroupAlias;
                    _ctxAccessor.Current.MigrationState.TargetAlias = targetOptions.Alias!;

                    // Check if this file+target can resume from a previous partial execution
                    int startFromBlock = FindResumableBlock(file, targetOptions.Alias!, existingRecords);

                    // Branch: CLI tool or DAL execution
                    int succeededBlocks, failedBlocks;
                    bool atomicCommitCompleted;
                    string? resolvedCliAlias = ResolveUseCliToolAlias(file, targetOptions);
                    if (resolvedCliAlias != null)
                    {
                        var cliTool = GetCliToolByAlias(resolvedCliAlias);
                        (succeededBlocks, failedBlocks) = await ExecuteWithCliTool(
                            file, targetGroupOptions, targetOptions, migrationId, request.RunMode, cliTool);
                        atomicCommitCompleted = false;
                    }
                    else
                    {
                        (succeededBlocks, failedBlocks, atomicCommitCompleted) = await ExecuteSqlBlocks(
                            file, targetGroupOptions, targetOptions, migrationId, request.RunMode,
                            ignoreBlockErrors: ignoreErrors,
                            startFromBlock: startFromBlock);
                    }

                    if (failedBlocks > 0)
                    {
                        // Block-level failures with Ignore: mark as Failed, continue to next file
                        _logger.LogWarning(
                            "MigrationErrorAction=Ignore: {FailedBlocks}/{TotalBlocks} block(s) failed in {Filename} on target {Target}. Continuing with next file.",
                            failedBlocks, file.FileUpBlocksTotal, file.Filename, targetOptions.Alias);

                        if (request.RunMode.ShouldWriteRepository())
                        {
                            await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                                migrationId, MigrationStatus.Failed, _ctxAccessor.Current.MigrationState.FileBlockId));
                        }

                        result.FailCount++;
                        migrationResults.Add(new MigrationFileResult
                        {
                            FileName = file.Filename,
                            ReleaseVersion = file.ReleaseVersion,
                            TargetGroup = file.TargetGroupAlias,
                            Success = false,
                            ErrorMessage = $"MigrationErrorAction=Ignore: One or more SQL blocks failed in {file.Filename}",
                            ExecutedAt = fileStartTime,
                            Duration = DateTime.UtcNow - fileStartTime
                        });
                        continue; // Continue to next file
                    }

                    if (!atomicCommitCompleted && request.RunMode.ShouldWriteRepository())
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                            migrationId, MigrationStatus.Migrated, file.FileUpBlocksTotal));
                    }

                    successfullyMigratedRecords.Add((file, migrationId, targetOptions.Alias!));

                    _logger.LogInformation(
                        "Migration successful | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlocks: {SqlBlocksTotal}",
                        productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, migrationId, file.FileUpBlocksTotal);

                    result.SuccessCount++;
                    migrationResults.Add(new MigrationFileResult
                    {
                        FileName = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroup = file.TargetGroupAlias,
                        Success = true,
                        ExecutedAt = fileStartTime,
                        Duration = DateTime.UtcNow - fileStartTime
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Migration FAILED | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlock: {SqlBlock}/{SqlBlocksTotal}",
                        productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, file.ReleaseVersion, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, _ctxAccessor.Current.MigrationState.MigrationId, _ctxAccessor.Current.MigrationState.FileBlockId, file.FileUpBlocksTotal);

                    try
                    {
                        if (request.RunMode.ShouldWriteRepository())
                        {
                            await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                                _ctxAccessor.Current.MigrationState.MigrationId, MigrationStatus.Failed,
                                _ctxAccessor.Current.MigrationState.FileBlockId));
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogWarning(updateEx,
                            "Failed to update migration record {MigrationId} ({Filename}) for error state",
                            _ctxAccessor.Current.MigrationState.MigrationId, file.Filename);
                    }

                    if (ignoreErrors)
                    {
                        // Ignore: log warning, mark as Failed, continue to next file
                        _logger.LogWarning(
                            "MigrationErrorAction=Ignore: Exception in {Filename} on target {Target}. Continuing with next file.",
                            file.Filename, targetOptions.Alias);

                        result.FailCount++;
                        migrationResults.Add(new MigrationFileResult
                        {
                            FileName = file.Filename,
                            ReleaseVersion = file.ReleaseVersion,
                            TargetGroup = file.TargetGroupAlias,
                            Success = false,
                            ErrorMessage = ex.Message,
                            ExecutedAt = fileStartTime,
                            Duration = DateTime.UtcNow - fileStartTime
                        });
                        continue; // Continue to next file
                    }

                    // Non-Ignore: abort this TargetGroup immediately — caller handles rollback
                    result.FailCount++;
                    result.Success = false;
                    result.FailedFile = file;
                    result.FailedMigrationId = _ctxAccessor.Current.MigrationState.MigrationId;
                    result.ErrorMessage = ex.Message;

                    migrationResults.Add(new MigrationFileResult
                    {
                        FileName = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroup = file.TargetGroupAlias,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ExecutedAt = fileStartTime,
                        Duration = DateTime.UtcNow - fileStartTime
                    });

                    return result;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether a migration file can use the atomic shared-connection path,
    /// where target SQL blocks and repository status updates execute in a single transaction.
    /// Requires: UseTransaction=true, no retries, block errors not ignored,
    /// same DatabaseType, and identical ConnectionString between target and repository.
    /// </summary>
    internal static bool CanUseSharedConnection(
        MigrationFileInfo file,
        TargetOptions targetOptions,
        RepositoryOptions repository,
        string targetGroupDatabaseType,
        bool ignoreBlockErrors)
    {
        return file.UseTransaction
            && !ignoreBlockErrors
            && string.Equals(repository.DatabaseType, targetGroupDatabaseType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(targetOptions.ConnectionString, repository.ConnectionString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the ordered sequence of (FileOrderId, TargetAlias) pairs based on TargetMigrationOrder.
    /// Pure static helper for unit testing execution order logic without requiring TemplateExecutor.
    /// </summary>
    internal static List<(int FileOrderId, string TargetAlias)> GetExecutionOrder(
        List<MigrationFileInfo> files, TargetGroupOptions targetGroup)
    {
        var result = new List<(int FileOrderId, string TargetAlias)>();
        var targets = targetGroup.Targets?.ToList() ?? new List<TargetOptions>();

        if (targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously)
        {
            // File → Target (Simultaneously)
            foreach (var file in files)
            {
                foreach (var target in targets)
                {
                    result.Add((file.FileOrderId, target.Alias!));
                }
            }
        }
        else
        {
            // Target → File (Successively, default for Undefined)
            foreach (var target in targets)
            {
                foreach (var file in files)
                {
                    result.Add((file.FileOrderId, target.Alias!));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the full execution order across all releases and target groups.
    /// Release → TargetGroup (config order or explicit order) → inner file/target order (per TargetMigrationOrder).
    /// Pure static helper for unit testing the complete execution order without requiring TemplateExecutor.
    /// </summary>
    internal static List<(int FileOrderId, string TargetGroupAlias, string TargetAlias)> GetFullExecutionOrder(
        List<MigrationFileInfo> files, List<TargetGroupOptions> targetGroups,
        string[]? targetGroupMigrationOrder = null)
    {
        var result = new List<(int FileOrderId, string TargetGroupAlias, string TargetAlias)>();

        // If explicit execution order is provided, reorder target groups
        var effectiveTargetGroups = targetGroupMigrationOrder != null
            ? ValidateAndReorderTargetGroups(targetGroupMigrationOrder, targetGroups, "GetFullExecutionOrder")
            : targetGroups;

        // Releases in FileOrderId order (Distinct preserves first-seen order)
        var orderedReleases = files
            .Select(f => f.ReleaseVersion)
            .Distinct()
            .ToList();

        var filesByReleaseAndTG = files
            .GroupBy(f => (f.ReleaseVersion, f.TargetGroupAlias))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var release in orderedReleases)
        {
            foreach (var targetGroup in effectiveTargetGroups)
            {
                var key = (release, targetGroup.Alias!);
                if (!filesByReleaseAndTG.TryGetValue(key, out var tgFiles) || tgFiles.Count == 0)
                    continue;

                // Get inner order from existing helper
                var innerOrder = GetExecutionOrder(tgFiles, targetGroup);

                foreach (var (fileOrderId, targetAlias) in innerOrder)
                {
                    result.Add((fileOrderId, targetGroup.Alias!, targetAlias));
                }
            }
        }

        return result;
    }

    #endregion TargetGroup Execution

    #region MigrateDown

    public async Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request)
    {
        var startTime = DateTime.UtcNow;
        var runId = Guid.NewGuid();

        try
        {
            _logger.LogInformation("Executing Migrate-Down for product {Product} to release {Release}",
                request.ProductAlias, request.TargetReleaseVersion);

            // Validate context matches request
            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Validate mode: validate rollback file existence and parseability ---
            if (!request.RunMode.ShouldReadRepository())
            {
                var productOpts = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
                var allFiles = DiscoverAndPrepareMigrationFiles(productOpts, request.Environment);

                // Filter files in releases AFTER target (same logic as normal rollback)
                var filesToValidate = FilterReleasesAfterTarget(allFiles, request.TargetReleaseVersion!);

                // Filter by target group if specified
                ValidateTargetGroupAliases(request.TargetGroupAliases, productOpts.TargetGroups!);
                filesToValidate = FilterByTargetGroups(filesToValidate, request.TargetGroupAliases);

                int validated = 0;
                var warnings = new List<string>();
                string rollbackPreExt = productOpts.MigrationRollbackFilesPreExtension ?? "rollback";
                string fileExt = productOpts.MigrationFilesExtension ?? "sql";

                foreach (var file in filesToValidate)
                {
                    string rollbackFilename = GetRollbackFilename(file.Filename, rollbackPreExt, fileExt);
                    string rollbackPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, rollbackFilename);

                    if (!File.Exists(rollbackPath))
                    {
                        _logger.LogWarning("[VALIDATE] Rollback file missing: {Expected}", rollbackPath);
                        warnings.Add($"Missing rollback file: {rollbackFilename}");
                        continue;
                    }

                    // Parse to validate structure
                    ParseMigrationFile(rollbackPath, productOpts.MigrationFilesRootDirectory!, productOpts, 0);
                    _logger.LogDebug("[VALIDATE] Rollback file valid: {Filename}", rollbackFilename);
                    validated++;
                }

                var messages = new List<string> { $"Validated {validated} rollback file(s) for {filesToValidate.Count} migration(s)" };
                messages.AddRange(warnings);

                return new MigrationOperationResult
                {
                    Success = warnings.Count == 0,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateDown,
                    Result = warnings.Count == 0 ? MigrationRunResult.Ok : MigrationRunResult.Error,
                    TotalMigrations = filesToValidate.Count,
                    SuccessfulMigrations = validated,
                    FailedMigrations = filesToValidate.Count - validated,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = messages
                };
            }

            // Validate target group aliases early (before any repository operations)
            {
                var productOptsForValidation = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
                ValidateTargetGroupAliases(request.TargetGroupAliases, productOptsForValidation.TargetGroups!);
            }

            // --- Phase 1: Initialization ---
            _ctxAccessor.Current.MigrationState.MigrationRunResult = MigrationRunResult.Running;
            _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateDown;
            _logger.LogDebug("State initialized: MigrationRunResult={MigrationRunResult}, MigrationOperation={MigrationOperation}",
                MigrationRunResult.Running, MigrationOperation.MigrateDown);

            if (request.RunMode.ShouldWriteRepository())
            {
                await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
                await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
                await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

                var settingsJson = BuildMigrationRunSettingsJson(_ctxAccessor.Current);
                _logger.LogTrace("MigrationRun settings snapshot:\n{SettingsJson}", settingsJson);
                await RepositoryMigrationRunInsertWithAutoFix(settingsJson);
            }

            // --- Phase 2: Query migrations for rollback ---
            // Always query with MigrationRunMode.Migrate to read actual Migrate-mode records.
            List<MigrationRecord> existingRecords;
            try
            {
                existingRecords = await Task.Run(() =>
                    _templateExecutor.RepositoryMigrationSelect(MigrationRunMode.Migrate));
            }
            catch (Exception ex) when (!request.RunMode.ShouldWriteRepository())
            {
                // Repository doesn't exist yet — nothing to roll back
                _logger.LogWarning("Repository not accessible in {RunMode} mode, nothing to roll back. Reason: {ErrorMessage}",
                    request.RunMode, ex.Message);
                return new MigrationOperationResult
                {
                    Success = true,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateDown,
                    Result = MigrationRunResult.Ok,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { "Repository not accessible — nothing to roll back" }
                };
            }

            // Filter: Migrated state OR partially-rolled-back (Failed with FileDownBlocksMigrated > 0),
            // releases AFTER targetReleaseVersion, optionally by target group, reverse order
            var migrationsToRollback = existingRecords
                .Where(r => r.MigrationStatusId == MigrationStatus.Migrated
                    || (r.MigrationStatusId == MigrationStatus.Failed
                        && r.FileDownBlocksMigrated.HasValue
                        && r.FileDownBlocksMigrated > 0
                        && r.FileDownBlocksMigrated < r.FileDownBlocksTotal))
                .Where(r => string.Compare(r.ReleaseVersion, request.TargetReleaseVersion, StringComparison.OrdinalIgnoreCase) > 0)
                .Where(r => request.TargetGroupAliases == null || request.TargetGroupAliases.Length == 0 ||
                    request.TargetGroupAliases.Any(alias =>
                        string.Equals(r.TargetGroupAlias, alias, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(r => r.FileOrderId)
                .ToList();

            if (migrationsToRollback.Count == 0)
            {
                _logger.LogInformation("No migrations found to roll back for product {Product} to release {Release}",
                    request.ProductAlias, request.TargetReleaseVersion);
                if (request.RunMode.ShouldWriteRepository())
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                }
                return new MigrationOperationResult
                {
                    Success = true,
                    RunId = runId,
                    ProductAlias = request.ProductAlias,
                    Environment = request.Environment,
                    Operation = MigrationOperation.MigrateDown,
                    Result = MigrationRunResult.Ok,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { $"No migrations to roll back (already at or before {request.TargetReleaseVersion})" }
                };
            }

            _logger.LogInformation("Rolling back {Count} migration(s) for product {Product} to release {Release}",
                migrationsToRollback.Count, request.ProductAlias, request.TargetReleaseVersion);

            // --- Phase 3: Execute rollbacks ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);

            var rollbackResult = await ExecuteRollbackForMigrations(
                migrationsToRollback, productOptions, request.RunMode);

            // --- Phase 4: Finalization ---
            var finalResult = rollbackResult.AllSuccessful ? MigrationRunResult.Ok : MigrationRunResult.Error;
            if (request.RunMode.ShouldWriteRepository())
            {
                await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(finalResult));
            }

            _logger.LogInformation("Migrate-Down completed for product {Product}: {Successful}/{Total} rollbacks successful",
                request.ProductAlias, rollbackResult.SuccessCount, migrationsToRollback.Count);

            return new MigrationOperationResult
            {
                Success = rollbackResult.AllSuccessful,
                RunId = runId,
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                Operation = MigrationOperation.MigrateDown,
                Result = finalResult,
                TotalMigrations = migrationsToRollback.Count,
                SuccessfulMigrations = rollbackResult.SuccessCount,
                FailedMigrations = rollbackResult.FailCount,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = rollbackResult.ErrorMessage,
                MigrationResults = rollbackResult.FileResults,
                Messages = rollbackResult.Messages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Migrate-Down for product {Product}", request.ProductAlias);

            if (request.RunMode.ShouldWriteRepository())
            {
                try
                {
                    if (_ctxAccessor.Current.MigrationState.MigrationRunId > 0)
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Error));
                    }
                }
                catch (Exception finalizeEx)
                {
                    _logger.LogWarning(finalizeEx,
                        "Failed to finalize MigrationRun {RunId} with error status",
                        _ctxAccessor.Current.MigrationState.MigrationRunId);
                }
            }

            return new MigrationOperationResult
            {
                Success = false,
                RunId = runId,
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                Operation = MigrationOperation.MigrateDown,
                Result = MigrationRunResult.Error,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                Messages = new List<string> { $"Rollback failed: {ex.Message}" }
            };
        }
    }

    #endregion MigrateDown

    #region Baseline

    public async Task<BaselineResult> BaselineAsync(BaselineRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            string releaseLabel = string.IsNullOrWhiteSpace(request.TargetReleaseVersion)
                ? "all releases"
                : $"release {request.TargetReleaseVersion}";

            _logger.LogInformation("Executing Baseline for product {Product} for {ReleaseScope}",
                request.ProductAlias, releaseLabel);

            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Phase 1: Initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            // Create migration run with settings snapshot (auto-fixes orphaned runs if needed)
            _ctxAccessor.Current.MigrationState.MigrationRunResult = MigrationRunResult.Running;
            _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;
            _logger.LogDebug("State initialized: MigrationRunResult={MigrationRunResult}, MigrationOperation={MigrationOperation}",
                MigrationRunResult.Running, MigrationOperation.MigrateUp);
            var settingsJson = BuildMigrationRunSettingsJson(_ctxAccessor.Current);
            _logger.LogTrace("MigrationRun settings snapshot:\n{SettingsJson}", settingsJson);
            await RepositoryMigrationRunInsertWithAutoFix(settingsJson);

            // --- Phase 2: File Discovery ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
            var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);

            if (migrationFiles.Count == 0)
            {
                _logger.LogInformation("No migration files found for product {Product}", request.ProductAlias);
                await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                return new BaselineResult
                {
                    Success = true,
                    ProductAlias = request.ProductAlias,
                    TargetReleaseVersion = request.TargetReleaseVersion,
                    BaselinedFiles = 0,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { "No migration files found" }
                };
            }

            // --- Phase 3: Filter files up to target release and target group ---
            var filesToBaseline = FilterByTargetRelease(migrationFiles, request.TargetReleaseVersion);

            ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
            filesToBaseline = FilterByTargetGroups(filesToBaseline, request.TargetGroupAliases);

            if (filesToBaseline.Count == 0)
            {
                _logger.LogInformation("No migration files found for {ReleaseScope}", releaseLabel);
                await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                return new BaselineResult
                {
                    Success = true,
                    ProductAlias = request.ProductAlias,
                    TargetReleaseVersion = request.TargetReleaseVersion,
                    BaselinedFiles = 0,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { $"No migration files found for {releaseLabel}" }
                };
            }

            // Query existing migrations and filter out already-migrated files
            var existingRecords = await Task.Run(() => _templateExecutor.RepositoryMigrationSelect());
            filesToBaseline = FilterAlreadyMigratedFiles(filesToBaseline, existingRecords, productOptions);

            if (filesToBaseline.Count == 0)
            {
                _logger.LogInformation("All files for {ReleaseScope} are already migrated", releaseLabel);
                await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));
                return new BaselineResult
                {
                    Success = true,
                    ProductAlias = request.ProductAlias,
                    TargetReleaseVersion = request.TargetReleaseVersion,
                    BaselinedFiles = 0,
                    Duration = DateTime.UtcNow - startTime,
                    Messages = new List<string> { "All migrations already applied" }
                };
            }

            _logger.LogInformation("Baselining {Count} migration file(s) for {ReleaseScope}",
                filesToBaseline.Count, releaseLabel);

            // --- Phase 4: Record each file as migrated (without executing SQL) ---
            int baselinedCount = 0;

            // Release-based ordering: Release → TargetGroup → Targets
            var baselineOrderedReleases = filesToBaseline
                .Select(f => f.ReleaseVersion)
                .Distinct()
                .ToList();

            var baselineFilesByReleaseAndTG = filesToBaseline
                .GroupBy(f => (f.ReleaseVersion, f.TargetGroupAlias))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Load migsettings for TargetGroupMigrationOrder resolution
            var baselineMigSettings = LoadMigSettingsDefaults(
                productOptions.MigrationFilesRootDirectory!, request.Environment,
                productOptions.MigrationFilesExtension ?? "sql");

            foreach (var release in baselineOrderedReleases)
            {
                // Resolve TargetGroup execution order for this release
                var baselineReleaseDir = Path.Combine(productOptions.MigrationFilesRootDirectory!, release);
                var baselineTgOrder = ResolveTargetGroupMigrationOrder(
                    baselineReleaseDir, productOptions, baselineMigSettings, request.TargetGroupMigrationOrder);
                var baselineOrderedTGs = baselineTgOrder != null
                    ? ValidateAndReorderTargetGroups(baselineTgOrder, productOptions.TargetGroups!, "Baseline")
                    : productOptions.TargetGroups!;

                foreach (var targetGroup in baselineOrderedTGs)
                {
                    var key = (release, targetGroup.Alias!);
                    if (!baselineFilesByReleaseAndTG.TryGetValue(key, out var tgFiles) || tgFiles.Count == 0)
                        continue;

                    if (targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously)
                    {
                        // File → Target order (Simultaneously)
                        foreach (var file in tgFiles)
                        {
                            foreach (var targetOptions in targetGroup.Targets!)
                            {
                                await BaselineFile(file, targetOptions);
                            }
                        }
                    }
                    else
                    {
                        // Target → File order (Successively, default)
                        foreach (var targetOptions in targetGroup.Targets!)
                        {
                            foreach (var file in tgFiles)
                            {
                                await BaselineFile(file, targetOptions);
                            }
                        }
                    }

                    baselinedCount += tgFiles.Count;
                }
            }

            async Task BaselineFile(MigrationFileInfo file, TargetOptions targetOptions)
            {
                // Validate CLI tool alias if set (no execution, but ensures config is correct for future rollbacks)
                string? cliAlias = ResolveUseCliToolAlias(file, targetOptions);
                if (cliAlias != null)
                    GetCliToolByAlias(cliAlias); // Throws ConfigurationValidationException if alias not found

                // Look up existing record for this file+target (e.g. archived record to reuse)
                var existingRecord = existingRecords.FirstOrDefault(r =>
                    r.Filename == file.Filename &&
                    r.ReleaseVersion == file.ReleaseVersion &&
                    r.TargetGroupAlias == file.TargetGroupAlias &&
                    r.TargetAlias == targetOptions.Alias);
                int existingMigrationId = existingRecord?.Id ?? 0;

                int migrationId = await Task.Run(() => _templateExecutor.RepositoryMigrationInsert(
                    existingMigrationId,
                    file.Filename,
                    file.ReleaseVersion,
                    file.TargetGroupAlias,
                    targetOptions.Alias!,
                    file.FileOrderId,
                    file.FileUpHash,
                    file.FileUpConfigHash,
                    file.FileUpBlocksHash,
                    file.FileUpBlocksTotal,
                    file.FileUpConfigJson,
                    file.MigrateDownFileExists));

                if (existingMigrationId > 0)
                    migrationId = existingMigrationId;

                await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                    migrationId, MigrationStatus.Migrated, file.FileUpBlocksTotal));

                _logger.LogDebug("Baselined: {Filename} (Release: {Release}, Target: {Target})",
                    file.Filename, file.ReleaseVersion, targetOptions.Alias);
            }

            // --- Phase 5: Finalization ---
            await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok));

            return new BaselineResult
            {
                Success = true,
                ProductAlias = request.ProductAlias,
                TargetReleaseVersion = request.TargetReleaseVersion,
                BaselinedFiles = baselinedCount,
                Duration = DateTime.UtcNow - startTime,
                Messages = new List<string> { $"Successfully baselined {baselinedCount} migration file(s)" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Baseline for product {Product}", request.ProductAlias);

            try
            {
                if (_ctxAccessor.Current.MigrationState.MigrationRunId > 0)
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Error));
                }
            }
            catch (Exception finalizeEx)
            {
                _logger.LogWarning(finalizeEx,
                    "Failed to finalize MigrationRun {RunId} with error status",
                    _ctxAccessor.Current.MigrationState.MigrationRunId);
            }

            return new BaselineResult
            {
                Success = false,
                ProductAlias = request.ProductAlias,
                TargetReleaseVersion = request.TargetReleaseVersion,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                Messages = new List<string> { $"Baseline failed: {ex.Message}" }
            };
        }
    }

    #endregion Baseline

    #region Shared Rollback Execution

    /// <summary>
    /// Executes rollback for a list of migration records.
    /// Shared by MigrateUp (error recovery) and MigrateDown (explicit rollback).
    /// </summary>
    private async Task<RollbackResult> ExecuteRollbackForMigrations(
        List<MigrationRecord> migrationsToRollback,
        ProductOptions productOptions,
        MigrationRunMode runMode,
        bool isErrorRecovery = false)
    {
        _logger.LogDebug("Executing rollback for {Count} migration(s) for product {Product}",
            migrationsToRollback.Count, productOptions.Alias);

        var result = new RollbackResult();
        string rollbackPreExtension = productOptions.MigrationRollbackFilesPreExtension ?? "rollback";
        string migrationFilesExtension = productOptions.MigrationFilesExtension ?? "sql";
        string rootDirectory = productOptions.MigrationFilesRootDirectory!;

        foreach (var record in migrationsToRollback)
        {
            var fileStartTime = DateTime.UtcNow;

            try
            {
                // Locate rollback file
                string rollbackFilename = GetRollbackFilename(record.Filename, rollbackPreExtension, migrationFilesExtension);
                string rollbackFilePath = Path.Combine(rootDirectory, record.ReleaseVersion, record.TargetGroupAlias, rollbackFilename);

                // Flat layout fallback: try release directory directly for single-TG products
                if (!File.Exists(rollbackFilePath) && productOptions.TargetGroups?.Count == 1)
                {
                    string flatRollbackPath = Path.Combine(rootDirectory, record.ReleaseVersion, rollbackFilename);
                    if (File.Exists(flatRollbackPath))
                    {
                        rollbackFilePath = flatRollbackPath;
                    }
                }

                if (!File.Exists(rollbackFilePath))
                {
                    string rollbackRelativePath = Path.Combine("/", record.ReleaseVersion, record.TargetGroupAlias, rollbackFilename);
                    if (productOptions.RequireRollbackFile == true)
                    {
                        _logger.LogError(
                            "Rollback file missing (RequireRollbackFile=true) | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | Expected: {RollbackPath}",
                            productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, record.ReleaseVersion, record.TargetGroupAlias, record.TargetAlias, record.Filename, record.Id, rollbackRelativePath);

                        // RequireRollbackFile=true + file missing = structural error → abort chain regardless of RollbackErrorAction
                        if (runMode.ShouldWriteRepository())
                        {
                            await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                                record.Id, MigrationStatus.Failed, record.FileUpBlocksMigrated));
                        }

                        result.AddFailure(record.Filename, $"Required rollback file not found: {rollbackFilename}");
                        result.ErrorMessage = $"Rollback chain aborted: required rollback file missing for {record.Filename}";
                        return result;
                    }
                    else
                    {
                        // RequireRollbackFile=false: check StopRollbackOnMissingRollbackFile for error-recovery rollback
                        if (isErrorRecovery)
                        {
                            // Resolve effective value: CLI → TargetGroup → Product → default(true)
                            var tg = productOptions.TargetGroups?.FirstOrDefault(t => t.Alias == record.TargetGroupAlias);
                            bool effectiveStop = _ctxAccessor.Current.RayMigratorConsoleOptions.StopRollbackOnMissingRollbackFile
                                ?? tg?.StopRollbackOnMissingRollbackFile
                                ?? productOptions.StopRollbackOnMissingRollbackFile
                                ?? true;

                            if (effectiveStop)
                            {
                                _logger.LogWarning(
                                    "Rollback stopped (StopRollbackOnMissingRollbackFile=true) | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | Searched: {RollbackPath}",
                                    productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, record.ReleaseVersion, record.TargetGroupAlias, record.TargetAlias, record.Filename, record.Id, rollbackRelativePath);

                                result.AddWarning(record.Filename, $"Rollback stopped: rollback file not found: {rollbackFilename}");
                                return result;
                            }
                        }

                        _logger.LogInformation(
                            "No Rollback file provided | Product: {Product} | Env: {Environment} | Release: {Release} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | Searched: {RollbackPath}",
                            productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, record.ReleaseVersion, record.TargetGroupAlias, record.TargetAlias, record.Filename, record.Id, rollbackRelativePath);
                    }

                    // Don't update status — keep original (Migrated for successful migrations, Failed for failed ones)
                    result.AddWarning(record.Filename, $"Rollback file not found: {rollbackFilename}");
                    result.SuccessCount++;
                    continue;
                }

                // Parse the rollback file
                var rollbackFileInfo = ParseMigrationFile(rollbackFilePath, rootDirectory, productOptions, 0);

                // Resolve effective RollbackErrorAction: file-level override → Product → default (Terminate)
                var rollbackErrorAction = rollbackFileInfo.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;
                if (rollbackErrorAction == RollbackErrorAction.Undefined)
                    rollbackErrorAction = RollbackErrorAction.Terminate;

                // Get target group options for DAL access
                var targetGroupOptions = productOptions.TargetGroups!
                    .FirstOrDefault(tg => tg.Alias == record.TargetGroupAlias);

                if (targetGroupOptions == null)
                {
                    throw new ConfigurationValidationException(
                        $"Target group '{record.TargetGroupAlias}' not found in product configuration");
                }

                var targetOptions = targetGroupOptions.Targets!
                    .FirstOrDefault(t => t.Alias == record.TargetAlias);

                if (targetOptions == null)
                {
                    throw new ConfigurationValidationException(
                        $"Target '{record.TargetAlias}' not found in target group '{record.TargetGroupAlias}'");
                }

                // Update migration record with rollback metadata (initial)
                if (runMode.ShouldWriteRepository())
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                        record.Id,
                        MigrationStatus.Executing,
                        rollbackFileInfo.FileUpHash,
                        rollbackFileInfo.FileUpConfigHash,
                        rollbackFileInfo.FileUpBlocksHash,
                        0,
                        rollbackFileInfo.FileUpBlocksTotal,
                        rollbackFileInfo.FileUpConfigJson));
                }

                // Execute rollback SQL blocks
                bool fileHadBlockError = false;
                bool atomicRollbackDone = false;
                if (runMode.ShouldExecuteSql())
                {
                    // Check if CLI tool should be used for rollback
                    string? rollbackCliAlias = ResolveUseCliToolAlias(rollbackFileInfo, targetOptions);
                    if (rollbackCliAlias != null)
                    {
                        // CLI tool execution for rollback: execute entire file as single unit
                        var cliTool = GetCliToolByAlias(rollbackCliAlias);
                        var (_, failedBlocks) = await ExecuteWithCliTool(
                            rollbackFileInfo, targetGroupOptions, targetOptions, record.Id, runMode, cliTool);

                        if (failedBlocks > 0)
                            fileHadBlockError = true;
                    }
                    else
                    {
                        // DAL execution for rollback
                        if (!DalFactory.TryGetDal(targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!, out var targetDal))
                        {
                            throw new TemplateExecutionException(
                                $"Cannot create DAL for database type [{targetGroupOptions.DatabaseType}]");
                        }

                        var dalSettings = new DalSettings
                        {
                            UseTransaction = rollbackFileInfo.UseTransaction,
                            DbCommandTimeoutInSeconds = targetOptions.DbCommandTimeoutInSeconds ?? 20,
                            MaxRetries = targetOptions.DbCommandMaxRetries ?? 0,
                            RetryDelayMs = targetOptions.DbCommandWaitTimeInMsBeforeRetry ?? 250
                        };
                        _logger.LogTrace("Rollback DalSettings for {Filename} on target {Target}: UseTransaction={UseTransaction}, Timeout={Timeout}s, MaxRetries={MaxRetries}, RetryDelayMs={RetryDelayMs}",
                            record.Filename, targetOptions.Alias, dalSettings.UseTransaction, dalSettings.DbCommandTimeoutInSeconds, dalSettings.MaxRetries, dalSettings.RetryDelayMs);

                        // Resume from previous partial rollback if applicable
                        int rollbackStartBlock = record.FileDownBlocksMigrated ?? 0;
                        if (rollbackStartBlock > 0 && rollbackStartBlock < rollbackFileInfo.SqlBlocks.Count)
                        {
                            _logger.LogInformation(
                                "Resuming rollback for migration {MigrationId} ({Filename}) from block {ResumeBlock}/{Total} (previous run executed {Done} blocks successfully)",
                                record.Id, record.Filename, rollbackStartBlock + 1,
                                rollbackFileInfo.FileUpBlocksTotal, rollbackStartBlock);
                        }

                        bool ignoreRollbackBlockErrors = rollbackErrorAction == RollbackErrorAction.Ignore;
                        bool useSharedRollbackConnection = CanUseSharedConnection(
                            rollbackFileInfo, targetOptions, _ctxAccessor.Current.RayMigratorOptions.Repository!,
                            targetGroupOptions.DatabaseType!, ignoreRollbackBlockErrors);

                        if (useSharedRollbackConnection)
                        {
                            // Atomic rollback: all blocks + final repo update in a single transaction
                            try
                            {
                                await ExecuteRollbackBlocksAtomic(
                                    rollbackFileInfo, targetDal!, dalSettings, record, runMode, rollbackStartBlock);
                                atomicRollbackDone = true;
                            }
                            catch (Exception blockEx)
                            {
                                // Atomic transaction was rolled back -- all repo updates within it are gone.
                                // Write Failed status via non-shared path (separate connection).
                                _logger.LogCritical(blockEx,
                                    "CRITICAL: Atomic rollback failed for migration {MigrationId} ({Filename}). RollbackErrorAction=Terminate — rollback chain ABORTED.",
                                    record.Id, record.Filename);

                                if (runMode.ShouldWriteRepository())
                                {
                                    await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                                        record.Id,
                                        MigrationStatus.Failed,
                                        rollbackFileInfo.FileUpHash,
                                        rollbackFileInfo.FileUpConfigHash,
                                        rollbackFileInfo.FileUpBlocksHash,
                                        0,
                                        rollbackFileInfo.FileUpBlocksTotal,
                                        rollbackFileInfo.FileUpConfigJson));
                                }

                                result.AddFailure(record.Filename, $"Atomic rollback failed: {blockEx.Message}");
                                result.ErrorMessage = $"Rollback chain aborted: atomic rollback of {record.Filename} failed";

                                return result;
                            }
                        }
                        else
                        {
                            for (int blockIndex = rollbackStartBlock; blockIndex < rollbackFileInfo.SqlBlocks.Count; blockIndex++)
                            {
                                string sqlBlock = ReplaceEnvironmentVariablesInSqlBlock(
                                    rollbackFileInfo.SqlBlocks[blockIndex], rollbackFileInfo.Filename, blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal);

                                _logger.LogDebug(
                                    "Executing rollback block {Block}/{Total} for migration {MigrationId} ({Filename})",
                                    blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Id, record.Filename);

                                _logger.LogTrace("Rollback SQL block {Block}/{Total} for {Filename}:\n{SqlContent}",
                                    blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Filename,
                                    SensitiveDataMasker.Mask(sqlBlock));

                                try
                                {
                                    await targetDal!.ExecuteNonQueryAsync(sqlBlock, dalSettings);

                                    // Update block progress
                                    await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                                        record.Id,
                                        MigrationStatus.Executing,
                                        rollbackFileInfo.FileUpHash,
                                        rollbackFileInfo.FileUpConfigHash,
                                        rollbackFileInfo.FileUpBlocksHash,
                                        blockIndex + 1,
                                        rollbackFileInfo.FileUpBlocksTotal,
                                        rollbackFileInfo.FileUpConfigJson));
                                }
                                catch (Exception blockEx)
                                {
                                    if (rollbackErrorAction == RollbackErrorAction.Terminate)
                                    {
                                        _logger.LogCritical(blockEx,
                                            "CRITICAL: Rollback block {Block}/{Total} failed for migration {MigrationId} ({Filename}). RollbackErrorAction=Terminate — rollback chain ABORTED.",
                                            blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Id, record.Filename);

                                        // Mark as failed - cannot recover from a failed rollback
                                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                                            record.Id,
                                            MigrationStatus.Failed,
                                            rollbackFileInfo.FileUpHash,
                                            rollbackFileInfo.FileUpConfigHash,
                                            rollbackFileInfo.FileUpBlocksHash,
                                            blockIndex,
                                            rollbackFileInfo.FileUpBlocksTotal,
                                            rollbackFileInfo.FileUpConfigJson));

                                        result.AddFailure(record.Filename, $"Rollback block {blockIndex + 1} failed: {blockEx.Message}");
                                        result.ErrorMessage = $"Rollback chain aborted: block {blockIndex + 1} of {record.Filename} failed";

                                        // Abort the entire rollback chain
                                        return result;
                                    }

                                    // RollbackErrorAction.Ignore — skip failed block, continue with next block
                                    _logger.LogWarning(blockEx,
                                        "Rollback block {Block}/{Total} failed for migration {MigrationId} ({Filename}). RollbackErrorAction=Ignore — skipping block, continuing rollback.",
                                        blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Id, record.Filename);

                                    fileHadBlockError = true;
                                }
                            }
                        }
                    }
                }

                if (atomicRollbackDone)
                {
                    // Atomic path already committed the final NotMigrated status inside the transaction
                    result.SuccessCount++;
                }
                else if (fileHadBlockError)
                {
                    // At least one block failed with Ignore — mark file as Failed
                    if (runMode.ShouldWriteRepository())
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                            record.Id,
                            MigrationStatus.Failed,
                            rollbackFileInfo.FileUpHash,
                            rollbackFileInfo.FileUpConfigHash,
                            rollbackFileInfo.FileUpBlocksHash,
                            rollbackFileInfo.FileUpBlocksTotal,
                            rollbackFileInfo.FileUpBlocksTotal,
                            rollbackFileInfo.FileUpConfigJson));
                    }

                    result.AddWarning(record.Filename, "Rollback completed with errors (some blocks failed, RollbackErrorAction=Ignore)");
                    result.SuccessCount++;
                }
                else
                {
                    // All rollback blocks successful
                    if (runMode.ShouldWriteRepository())
                    {
                        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateRollback(
                            record.Id,
                            MigrationStatus.NotMigrated,
                            rollbackFileInfo.FileUpHash,
                            rollbackFileInfo.FileUpConfigHash,
                            rollbackFileInfo.FileUpBlocksHash,
                            rollbackFileInfo.FileUpBlocksTotal,
                            rollbackFileInfo.FileUpBlocksTotal,
                            rollbackFileInfo.FileUpConfigJson));
                    }

                    result.SuccessCount++;
                }

                result.FileResults.Add(new MigrationFileResult
                {
                    FileName = record.Filename,
                    ReleaseVersion = record.ReleaseVersion,
                    TargetGroup = record.TargetGroupAlias,
                    Success = !fileHadBlockError,
                    ExecutedAt = fileStartTime,
                    Duration = DateTime.UtcNow - fileStartTime
                });

                _logger.LogInformation(
                    "Rollback {Status} | Product: {Product} | Env: {Environment} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlocks: {SqlBlocksTotal}",
                    fileHadBlockError ? "completed with errors" : "successful",
                    productOptions.Alias, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, record.TargetGroupAlias, record.TargetAlias, record.Filename, record.Id, rollbackFileInfo.FileUpBlocksTotal);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "CRITICAL: Rollback failed for migration {MigrationId} ({Filename}). Rollback chain ABORTED.",
                    record.Id, record.Filename);

                result.AddFailure(record.Filename, ex.Message);
                result.ErrorMessage = $"Rollback chain aborted at {record.Filename}: {ex.Message}";

                // Abort the entire rollback chain
                return result;
            }
        }

        if (result.AllSuccessful)
        {
            result.Messages.Add($"Successfully rolled back {result.SuccessCount} migration(s)");
        }

        return result;
    }

    /// <summary>
    /// Handles migration error based on the configured MigrationErrorAction.
    /// </summary>
    private async Task HandleMigrationError(
        ProductOptions productOptions,
        MigrationFileInfo failedFile,
        int failedMigrationId,
        List<(MigrationFileInfo File, int MigrationId, string TargetAlias)> successfullyMigratedRecords)
    {
        var errorAction = failedFile.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        if (failedFile.MigrationErrorActionOverride.HasValue)
        {
            _logger.LogDebug("Using file-level MigrationErrorAction override: {ErrorAction} (from TOML/migsettings)", errorAction);
        }

        _logger.LogDebug("Handling migration error with action: {ErrorAction}", errorAction);

        switch (errorAction)
        {
            case MigrationErrorAction.Terminate:
                _logger.LogCritical("MigrationErrorAction=Terminate: No rollback will be performed. Database may be in unclear state.");
                break;

            case MigrationErrorAction.RollbackErrorOnly:
                _logger.LogInformation("MigrationErrorAction=RollbackErrorOnly: Rolling back only the failed migration file.");
                await RollbackSingleMigration(productOptions, failedFile, failedMigrationId);
                break;

            case MigrationErrorAction.Rollback:
                _logger.LogInformation(
                    "MigrationErrorAction=Rollback: Rolling back failed migration and {Count} previously successful migration(s).",
                    successfullyMigratedRecords.Count);

                // Build list of all records to rollback (failed first, then successful in reverse order)
                var recordsToRollback = new List<MigrationRecord>();

                // Add the failed migration
                recordsToRollback.Add(new MigrationRecord
                {
                    Id = failedMigrationId,
                    Filename = failedFile.Filename,
                    ReleaseVersion = failedFile.ReleaseVersion,
                    TargetGroupAlias = failedFile.TargetGroupAlias,
                    TargetAlias = _ctxAccessor.Current.MigrationState.TargetAlias,
                    MigrationStatusId = MigrationStatus.Failed,
                    FileUpBlocksMigrated = _ctxAccessor.Current.MigrationState.FileBlockId
                });

                // Add successful migrations in reverse order
                foreach (var (file, migrationId, targetAlias) in successfullyMigratedRecords.AsEnumerable().Reverse())
                {
                    recordsToRollback.Add(new MigrationRecord
                    {
                        Id = migrationId,
                        Filename = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroupAlias = file.TargetGroupAlias,
                        TargetAlias = targetAlias,
                        MigrationStatusId = MigrationStatus.Migrated,
                        FileUpBlocksMigrated = file.FileUpBlocksTotal
                    });
                }

                await ExecuteRollbackForMigrations(recordsToRollback, productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, isErrorRecovery: true);
                break;

            case MigrationErrorAction.RollbackRelease:
                _logger.LogInformation(
                    "MigrationErrorAction=RollbackRelease: Rolling back all migrations from release {Release}.",
                    failedFile.ReleaseVersion);

                var releaseRecords = new List<MigrationRecord>();

                // Add the failed migration first
                releaseRecords.Add(new MigrationRecord
                {
                    Id = failedMigrationId,
                    Filename = failedFile.Filename,
                    ReleaseVersion = failedFile.ReleaseVersion,
                    TargetGroupAlias = failedFile.TargetGroupAlias,
                    TargetAlias = _ctxAccessor.Current.MigrationState.TargetAlias,
                    MigrationStatusId = MigrationStatus.Failed,
                    FileUpBlocksMigrated = _ctxAccessor.Current.MigrationState.FileBlockId
                });

                // Add successful records from same release in reverse order
                foreach (var (file, migrationId, targetAlias) in
                    successfullyMigratedRecords.AsEnumerable().Reverse()
                        .Where(r => r.File.ReleaseVersion == failedFile.ReleaseVersion))
                {
                    releaseRecords.Add(new MigrationRecord
                    {
                        Id = migrationId,
                        Filename = file.Filename,
                        ReleaseVersion = file.ReleaseVersion,
                        TargetGroupAlias = file.TargetGroupAlias,
                        TargetAlias = targetAlias,
                        MigrationStatusId = MigrationStatus.Migrated,
                        FileUpBlocksMigrated = file.FileUpBlocksTotal
                    });
                }

                await ExecuteRollbackForMigrations(releaseRecords, productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, isErrorRecovery: true);
                break;

            case MigrationErrorAction.Ignore:
                _logger.LogDebug(
                    "MigrationErrorAction=Ignore: HandleMigrationError called for {Filename}. No rollback will be performed.",
                    failedFile.Filename);
                break;

            default:
                _logger.LogWarning("Unknown MigrationErrorAction: {ErrorAction}. Defaulting to Terminate behavior.", errorAction);
                break;
        }
    }

    /// <summary>
    /// Rolls back a single failed migration.
    /// </summary>
    private async Task RollbackSingleMigration(
        ProductOptions productOptions,
        MigrationFileInfo failedFile,
        int failedMigrationId)
    {
        var singleRecord = new List<MigrationRecord>
        {
            new MigrationRecord
            {
                Id = failedMigrationId,
                Filename = failedFile.Filename,
                ReleaseVersion = failedFile.ReleaseVersion,
                TargetGroupAlias = failedFile.TargetGroupAlias,
                TargetAlias = _ctxAccessor.Current.MigrationState.TargetAlias,
                MigrationStatusId = MigrationStatus.Failed,
                FileUpBlocksMigrated = _ctxAccessor.Current.MigrationState.FileBlockId
            }
        };

        await ExecuteRollbackForMigrations(singleRecord, productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, isErrorRecovery: true);
    }

    #endregion Shared Rollback Execution

    #region File Discovery & Parsing

    /// <summary>
    /// Discovers and parses all migration files for a product, filtering by environment.
    /// </summary>
    private List<MigrationFileInfo> DiscoverAndPrepareMigrationFiles(
        ProductOptions productOptions, string environment)
    {
        string rootDirectory = productOptions.MigrationFilesRootDirectory!;
        string fileExtension = productOptions.MigrationFilesExtension ?? "sql";
        string rollbackPreExtension = productOptions.MigrationRollbackFilesPreExtension ?? "rollback";

        _logger.LogDebug("Scanning migration files in {RootDirectory}", rootDirectory);

        // Validate: detect TargetGroup alias directories with case mismatch
        ValidateTargetGroupAliasCasing(rootDirectory, productOptions);

        // Load migsettings defaults
        var migSettings = LoadMigSettingsDefaults(rootDirectory, environment, fileExtension);

        // Get all SQL files recursively, sorted by path
        var allFiles = Directory.EnumerateFiles(rootDirectory, $"*.{fileExtension}", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderBy(f => Path.GetRelativePath(rootDirectory, f.FullName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var migrationFiles = new List<MigrationFileInfo>();
        int fileOrderId = 1;

        foreach (var fileInfo in allFiles)
        {
            string relativePath = Path.GetRelativePath(rootDirectory, fileInfo.FullName);
            string filename = fileInfo.Name;

            // Skip rollback files (they are paired with forward migrations)
            if (IsRollbackFile(filename, rollbackPreExtension, fileExtension))
            {
                continue;
            }

            // Skip environment-specific files that don't match the current environment
            if (IsEnvironmentSpecificFile(filename, fileExtension) && !IsForEnvironment(filename, environment, fileExtension))
            {
                _logger.LogDebug("Skipping environment-specific file {Filename} (not for environment {Environment})",
                    filename, environment);
                continue;
            }

            // Skip migsettings files
            if (filename.StartsWith("migsettings", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var migrationFile = ParseMigrationFile(fileInfo.FullName, rootDirectory, productOptions, fileOrderId, migSettings);

                // Apply environment filter from TOML metadata
                if (migrationFile.Environments != null && migrationFile.Environments.Count > 0
                    && !migrationFile.Environments.Contains("*")
                    && !migrationFile.Environments.Contains(environment, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Skipping file {Filename} (TOML Environments filter excludes {Environment})",
                        filename, environment);
                    continue;
                }

                migrationFiles.Add(migrationFile);
                fileOrderId++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse migration file {RelativePath}", relativePath);
                throw new MigrationFileParsingException(
                    $"Error parsing migration file {relativePath}: {ex.Message}", ex);
            }
        }

        // Validate RequireRollbackFile
        var missingRollbacks = migrationFiles
            .Where(f => f.RequireRollbackFile && !f.MigrateDownFileExists)
            .ToList();

        if (missingRollbacks.Count > 0)
        {
            var fileList = string.Join("\n", missingRollbacks.Select(f =>
            {
                string rollbackName = GetRollbackFilename(f.Filename, rollbackPreExtension, fileExtension);
                string dir = Path.GetDirectoryName(f.FilenameWithRelativePath) ?? "";
                return $"  - {Path.Combine(dir, rollbackName)}";
            }));
            throw new MigrationFileParsingException(
                $"RequireRollbackFile validation failed. {missingRollbacks.Count} rollback file(s) " +
                $"are missing:\n{fileList}",
                TemplateResultCode.RequireRollbackFileValidationFailed);
        }

        // Validate: no ambiguous flat+traditional layout within the same release (single-TG only)
        if (productOptions.TargetGroups?.Count == 1)
        {
            ValidateFlatLayoutAmbiguity(migrationFiles, productOptions.TargetGroups[0].Alias!, productOptions.Alias!);
        }

        _logger.LogDebug("Discovered {Count} migration files after filtering", migrationFiles.Count);
        return migrationFiles;
    }

    /// <summary>
    /// Parses a single migration file: reads content, extracts TOML metadata, splits SQL blocks, computes hashes.
    /// </summary>
    private MigrationFileInfo ParseMigrationFile(
        string fullPath, string rootDirectory, ProductOptions productOptions, int fileOrderId,
        Dictionary<string, MigSettingsEntry>? migSettings = null)
    {
        string rollbackPreExtension = productOptions.MigrationRollbackFilesPreExtension ?? "rollback";
        string fileExtension = productOptions.MigrationFilesExtension ?? "sql";
        string relativePath = Path.GetRelativePath(rootDirectory, fullPath);
        string filename = Path.GetFileName(fullPath);

        // Read file content
        var encoding = GetFileEncoding(productOptions.MigrationFilesEncoding);
        string fileContent = File.ReadAllText(fullPath, encoding);

        // Extract TOML metadata and SQL content
        ExtractTomlAndSql(fileContent, out string? tomlContent, out string sqlContent);

        // Parse TOML configuration
        bool useTransaction = true;
        string description = string.Empty;
        List<string>? environments = null;
        List<string>? targets = null;
        bool runAlways = false;
        bool? requireRollbackFile = null;
        MigrationErrorAction? migrationErrorAction = null;
        RollbackErrorAction? rollbackErrorAction = null;
        string? configJson = null;

        string? useCliToolAlias = null;

        bool hasFileToml = !string.IsNullOrWhiteSpace(tomlContent);
        bool fileHasUseTransaction = false;
        bool fileHasRunAlways = false;
        bool fileHasRequireRollbackFile = false;
        bool fileHasEnvironments = false;
        bool fileHasTargets = false;
        bool fileHasMigrationErrorAction = false;
        bool fileHasRollbackErrorAction = false;
        bool fileHasUseCliToolAlias = false;
        bool useTransactionExplicitlySet = false;

        if (hasFileToml)
        {
            ParseTomlConfig(tomlContent!, out useTransaction, out description, out environments, out targets,
                out runAlways, out requireRollbackFile, out migrationErrorAction, out rollbackErrorAction,
                out string? tomlUseCliToolAlias, out _, out _);

            useCliToolAlias = tomlUseCliToolAlias;

            // Track which keys were explicitly set in the file TOML
            foreach (var line in tomlContent!.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0) continue;
                var key = trimmed.Substring(0, eqIdx).Trim().ToLowerInvariant();
                switch (key)
                {
                    case "usetransaction": fileHasUseTransaction = true; break;
                    case "runalways": fileHasRunAlways = true; break;
                    case "requirerollbackfile": fileHasRequireRollbackFile = true; break;
                    case "environments": fileHasEnvironments = true; break;
                    case "targets": fileHasTargets = true; break;
                    case "migrationerroraction": fileHasMigrationErrorAction = true; break;
                    case "rollbackerroraction": fileHasRollbackErrorAction = true; break;
                    case "useclitoolalias": fileHasUseCliToolAlias = true; break;
                }
            }

            configJson = SerializeTomlAsJson(useTransaction, description, environments, targets, runAlways, requireRollbackFile, migrationErrorAction, rollbackErrorAction, useCliToolAlias);
        }

        useTransactionExplicitlySet = fileHasUseTransaction;

        // Apply migsettings defaults (file-level TOML overrides migsettings)
        if (migSettings != null && migSettings.Count > 0)
        {
            var fileDir = Path.GetDirectoryName(fullPath)!;
            var defaults = ResolveMigSettingsForFile(fileDir, rootDirectory, migSettings);
            if (defaults != null)
            {
                if (!fileHasUseTransaction && defaults.UseTransaction.HasValue)
                {
                    useTransaction = defaults.UseTransaction.Value;
                    useTransactionExplicitlySet = true;
                }
                if (!fileHasRunAlways && defaults.RunAlways.HasValue)
                    runAlways = defaults.RunAlways.Value;
                if (!fileHasRequireRollbackFile && defaults.RequireRollbackFile.HasValue)
                    requireRollbackFile = defaults.RequireRollbackFile;
                if (!fileHasEnvironments && defaults.Environments != null)
                    environments = defaults.Environments;
                if (!fileHasTargets && defaults.Targets != null)
                    targets = defaults.Targets;
                if (!fileHasMigrationErrorAction && defaults.MigrationErrorAction.HasValue)
                    migrationErrorAction = defaults.MigrationErrorAction;
                if (!fileHasRollbackErrorAction && defaults.RollbackErrorAction.HasValue)
                    rollbackErrorAction = defaults.RollbackErrorAction;
                if (!fileHasUseCliToolAlias && defaults.UseCliToolAlias != null)
                    useCliToolAlias = defaults.UseCliToolAlias;

                // Regenerate config JSON with merged values
                configJson = SerializeTomlAsJson(useTransaction, description, environments, targets, runAlways, requireRollbackFile, migrationErrorAction, rollbackErrorAction, useCliToolAlias);
            }
        }

        // Resolve effective RequireRollbackFile: TOML → migsettings → Product → default (true)
        bool effectiveRequireRollbackFile = requireRollbackFile ?? productOptions.RequireRollbackFile ?? true;

        // Extract release version and target group from path (moved up — needed for ShouldSkipBlockSplitting)
        relativePath.GetReleaseVersionAndTargetGroupAlias(
            _options.Value, _ctxAccessor.Current.RayMigratorConsoleOptions.Product,
            out string releaseVersion, out string targetGroupAlias);

        // Skip block splitting when CLI tool execution is configured.
        // CLI tools execute the entire file as a single unit — delimiter-based splitting
        // is only needed for .NET DAL execution where each block is executed individually.
        List<string> sqlBlocks;
        if (ShouldSkipBlockSplitting(useCliToolAlias, targetGroupAlias, productOptions))
        {
            sqlBlocks = string.IsNullOrWhiteSpace(sqlContent)
                ? new List<string>()
                : new List<string> { sqlContent.Trim() };
        }
        else
        {
            string blockDelimiter = GetBlockDelimiter(relativePath, productOptions);
            sqlBlocks = SplitSqlIntoBlocks(sqlContent, blockDelimiter);
        }

        // Compute hashes (uses pre-split sqlContent — unaffected by split/skip decision)
        string fileUpHash = fileContent.GenerateSha256();
        string? fileUpConfigHash = tomlContent?.GenerateSha256();
        string fileUpBlocksHash = sqlContent.GenerateSha256();

        // Check for rollback file
        string rollbackFilename = GetRollbackFilename(filename, rollbackPreExtension, fileExtension);
        string rollbackFilePath = Path.Combine(Path.GetDirectoryName(fullPath)!, rollbackFilename);
        bool rollbackExists = File.Exists(rollbackFilePath);

        return new MigrationFileInfo
        {
            Filename = filename,
            FilenameWithRelativePath = relativePath,
            FullPath = fullPath,
            ReleaseVersion = releaseVersion,
            TargetGroupAlias = targetGroupAlias,
            FileOrderId = fileOrderId,
            FileUpHash = fileUpHash,
            FileUpConfigHash = fileUpConfigHash,
            FileUpBlocksHash = fileUpBlocksHash,
            SqlBlocks = sqlBlocks,
            TomlConfigRaw = tomlContent,
            FileUpConfigJson = configJson,
            MigrateDownFileExists = rollbackExists,
            RollbackFilePath = rollbackExists ? rollbackFilePath : null,
            UseTransaction = useTransaction,
            UseTransactionExplicitlySet = useTransactionExplicitlySet,
            Description = description,
            Environments = environments,
            Targets = targets,
            RunAlways = runAlways,
            RequireRollbackFile = effectiveRequireRollbackFile,
            MigrationErrorActionOverride = migrationErrorAction,
            RollbackErrorActionOverride = rollbackErrorAction,
            UseCliToolAlias = useCliToolAlias
        };
    }

    /// <summary>
    /// Extracts the TOML metadata block and the SQL content from a migration file.
    /// TOML is enclosed in /* [RayMigrator] ... */ at the start of the file.
    /// </summary>
    internal static void ExtractTomlAndSql(string fileContent, out string? tomlContent, out string sqlContent)
    {
        tomlContent = null;
        sqlContent = fileContent;

        // Find the TOML block: /* ... [RayMigrator] ... */
        var match = Regex.Match(fileContent, @"/\*\s*\n?\s*\[RayMigrator\](.*?)\*/", RegexOptions.Singleline);
        if (match.Success)
        {
            tomlContent = match.Groups[1].Value.Trim();
            sqlContent = fileContent.Substring(match.Index + match.Length).Trim();
        }
    }

    /// <summary>
    /// Parses simple TOML key=value pairs from the RayMigrator config section.
    /// </summary>
    internal static void ParseTomlConfig(string tomlContent,
        out bool useTransaction, out string description,
        out List<string>? environments, out List<string>? targets, out bool runAlways,
        out bool? requireRollbackFile, out MigrationErrorAction? migrationErrorAction,
        out RollbackErrorAction? rollbackErrorAction,
        out string? useCliToolAlias,
        out List<string>? targetGroupMigrationOrder,
        out bool? stopRollbackOnMissingRollbackFile)
    {
        useTransaction = true;
        description = string.Empty;
        environments = null;
        targets = null;
        runAlways = false;
        requireRollbackFile = null;
        migrationErrorAction = null;
        rollbackErrorAction = null;
        useCliToolAlias = null;
        targetGroupMigrationOrder = null;
        stopRollbackOnMissingRollbackFile = null;

        foreach (var line in tomlContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0) continue;

            var keyOriginal = trimmed.Substring(0, equalsIndex).Trim();
            var key = keyOriginal.ToLowerInvariant();
            var value = trimmed.Substring(equalsIndex + 1).Trim();

            switch (key)
            {
                case "usetransaction":
                    useTransaction = ParseTomlBool(value, keyOriginal);
                    break;
                case "description":
                    description = ParseTomlString(value);
                    break;
                case "environments":
                    environments = ParseTomlStringArray(value, keyOriginal);
                    break;
                case "targets":
                    targets = ParseTomlStringArray(value, keyOriginal);
                    break;
                case "runalways":
                    runAlways = ParseTomlBool(value, keyOriginal);
                    break;
                case "requirerollbackfile":
                    requireRollbackFile = ParseTomlBool(value, keyOriginal);
                    break;
                case "migrationerroraction":
                    migrationErrorAction = ParseTomlEnum<MigrationErrorAction>(value, keyOriginal);
                    break;
                case "rollbackerroraction":
                    rollbackErrorAction = ParseTomlEnum<RollbackErrorAction>(value, keyOriginal);
                    break;
                case "useclitoolalias":
                    useCliToolAlias = ParseTomlString(value);
                    break;
                case "targetgroupmigrationorder":
                    targetGroupMigrationOrder = ParseTomlStringArray(value, keyOriginal);
                    break;
                case "stoprollbackonmissingrollbackfile":
                    stopRollbackOnMissingRollbackFile = ParseTomlBool(value, keyOriginal);
                    break;
                default:
                    throw new MigrationFileParsingException(
                        $"Unknown TOML key '{keyOriginal}' in migration file metadata. " +
                        $"Valid keys are: UseTransaction, Description, Environments, Targets, RunAlways, RequireRollbackFile, StopRollbackOnMissingRollbackFile, MigrationErrorAction, RollbackErrorAction, UseCliToolAlias, TargetGroupMigrationOrder.");
            }
        }
    }

    internal static bool ParseTomlBool(string value, string keyName)
    {
        var cleaned = value.Trim().ToLowerInvariant();
        return cleaned switch
        {
            "true" => true,
            "false" => false,
            _ => throw new MigrationFileParsingException(
                $"Invalid value '{value.Trim()}' for TOML property '{keyName}'. Expected: true or false.")
        };
    }

    internal static string ParseTomlString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }
        return trimmed;
    }

    internal static List<string> ParseTomlStringArray(string value, string keyName)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
            throw new MigrationFileParsingException(
                $"Invalid value '{trimmed}' for TOML property '{keyName}'. Expected: array format [\"value1\", \"value2\"].");

        var inner = trimmed.Substring(1, trimmed.Length - 2);
        return inner.Split(',')
            .Select(s => ParseTomlString(s.Trim()))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    internal static T ParseTomlEnum<T>(string value, string keyName) where T : struct, Enum
    {
        var cleaned = ParseTomlString(value).Trim();
        if (!Enum.TryParse<T>(cleaned, ignoreCase: true, out var result))
        {
            var validValues = GetValidEnumValues<T>();
            throw new MigrationFileParsingException(
                $"Invalid value '{cleaned}' for TOML property '{keyName}'. Valid values are: {string.Join(", ", validValues)}.");
        }

        // Reject Undefined (value 0) explicitly
        if (Convert.ToByte(result) == 0)
        {
            var validValues = GetValidEnumValues<T>();
            throw new MigrationFileParsingException(
                $"Invalid value '{cleaned}' for TOML property '{keyName}'. 'Undefined' is not allowed. Valid values are: {string.Join(", ", validValues)}.");
        }

        return result;
    }

    internal static string[] GetValidEnumValues<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>()
            .Where(v => Convert.ToByte(v) != 0)
            .Select(v => v.ToString())
            .ToArray();
    }

    private static string SerializeTomlAsJson(bool useTransaction, string description,
        List<string>? environments, List<string>? targets, bool runAlways, bool? requireRollbackFile,
        MigrationErrorAction? migrationErrorAction, RollbackErrorAction? rollbackErrorAction,
        string? useCliToolAlias)
    {
        var config = new Dictionary<string, object?>
        {
            ["UseTransaction"] = useTransaction,
            ["Description"] = description,
            ["Environments"] = environments,
            ["Targets"] = targets,
            ["RunAlways"] = runAlways,
            ["RequireRollbackFile"] = requireRollbackFile,
            ["MigrationErrorAction"] = migrationErrorAction?.ToString(),
            ["RollbackErrorAction"] = rollbackErrorAction?.ToString(),
            ["UseCliToolAlias"] = useCliToolAlias
        };
        return JsonSerializer.Serialize(config);
    }

    /// <summary>
    /// Builds a JSON snapshot of all RayMigrator settings at migration start.
    /// Connection strings are masked via SensitiveDataMasker.Mask() (respects RevealSensitiveData).
    /// </summary>
    internal static string BuildMigrationRunSettingsJson(MigrationContext ctx)
    {
        var consoleOpts = ctx.RayMigratorConsoleOptions;
        var rayOpts = ctx.RayMigratorOptions;
        var productOptions = rayOpts.Products!.First(p => p.Alias == consoleOpts.Product);

        var settings = new Dictionary<string, object?>
        {
            ["RayMigratorVersion"] = ctx.RayMigratorVersion,
            ["ConsoleOptions"] = new Dictionary<string, object?>
            {
                ["Command"] = consoleOpts.Command.ToString(),
                ["Product"] = consoleOpts.Product,
                ["Environment"] = consoleOpts.Environment,
                ["RunMode"] = consoleOpts.RunMode.ToString(),
                ["TargetReleaseVersion"] = consoleOpts.TargetReleaseVersion,
                ["HashValidationScope"] = consoleOpts.HashValidationScope?.ToString(),
                ["ShowStartupInfo"] = consoleOpts.ShowStartupInfo,
                ["RevealSensitiveData"] = consoleOpts.RevealSensitiveData,
                ["AllowOutOfOrder"] = consoleOpts.AllowOutOfOrder,
                ["FixIssues"] = consoleOpts.FixIssues?.ToString()
            },
            ["Repository"] = new Dictionary<string, object?>
            {
                ["DatabaseType"] = rayOpts.Repository?.DatabaseType,
                ["ConnectionString"] = SensitiveDataMasker.Mask(rayOpts.Repository?.ConnectionString),
                ["SchemaName"] = rayOpts.Repository?.SchemaName,
                ["TableBaseName"] = rayOpts.Repository?.TableBaseName,
                ["DbCommandTimeoutInSeconds"] = rayOpts.Repository?.DbCommandTimeoutInSeconds,
                ["DbCommandMaxRetries"] = rayOpts.Repository?.DbCommandMaxRetries,
                ["DbCommandWaitTimeInMsBeforeRetry"] = rayOpts.Repository?.DbCommandWaitTimeInMsBeforeRetry
            },
            ["ProductDefaults"] = new Dictionary<string, object?>
            {
                ["MigrationErrorAction"] = rayOpts.ProductDefaults?.MigrationErrorAction,
                ["RollbackErrorAction"] = rayOpts.ProductDefaults?.RollbackErrorAction,
                ["MigrationFilesExtension"] = rayOpts.ProductDefaults?.MigrationFilesExtension,
                ["MigrationRollbackFilesPreExtension"] = rayOpts.ProductDefaults?.MigrationRollbackFilesPreExtension,
                ["MigrationFilesEncoding"] = rayOpts.ProductDefaults?.MigrationFilesEncoding,
                ["RequireRollbackFile"] = rayOpts.ProductDefaults?.RequireRollbackFile,
                ["TargetGroupDefaults"] = rayOpts.ProductDefaults?.TargetGroupDefaults == null ? null : new Dictionary<string, object?>
                {
                    ["TargetMigrationOrder"] = rayOpts.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder,
                    ["HashValidationScope"] = rayOpts.ProductDefaults.TargetGroupDefaults.HashValidationScope,
                    ["TargetDefaults"] = rayOpts.ProductDefaults.TargetGroupDefaults.TargetDefaults == null ? null : new Dictionary<string, object?>
                    {
                        ["DbCommandTimeoutInSeconds"] = rayOpts.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds,
                        ["DbCommandMaxRetries"] = rayOpts.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries,
                        ["DbCommandWaitTimeInMsBeforeRetry"] = rayOpts.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry
                    }
                }
            },
            ["Product"] = new Dictionary<string, object?>
            {
                ["Alias"] = productOptions.Alias,
                ["MigrationFilesRootDirectory"] = productOptions.MigrationFilesRootDirectory,
                ["MigrationErrorAction"] = productOptions.MigrationErrorAction,
                ["RollbackErrorAction"] = productOptions.RollbackErrorAction,
                ["MigrationFilesExtension"] = productOptions.MigrationFilesExtension,
                ["MigrationRollbackFilesPreExtension"] = productOptions.MigrationRollbackFilesPreExtension,
                ["MigrationFilesEncoding"] = productOptions.MigrationFilesEncoding,
                ["RequireRollbackFile"] = productOptions.RequireRollbackFile,
                ["TargetGroups"] = productOptions.TargetGroups?.Select(tg => new Dictionary<string, object?>
                {
                    ["Alias"] = tg.Alias,
                    ["DatabaseType"] = tg.DatabaseType,
                    ["TargetMigrationOrder"] = tg.TargetMigrationOrder,
                    ["HashValidationScope"] = tg.HashValidationScope,
                    ["Targets"] = tg.Targets?.Select(t => new Dictionary<string, object?>
                    {
                        ["Alias"] = t.Alias,
                        ["ConnectionString"] = SensitiveDataMasker.Mask(t.ConnectionString),
                        ["DbCommandTimeoutInSeconds"] = t.DbCommandTimeoutInSeconds,
                        ["DbCommandMaxRetries"] = t.DbCommandMaxRetries,
                        ["DbCommandWaitTimeInMsBeforeRetry"] = t.DbCommandWaitTimeInMsBeforeRetry
                    }).ToList()
                }).ToList()
            }
        };

        return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Splits SQL content into blocks using the database-specific delimiter (e.g., GO for SQL Server).
    /// </summary>
    internal static List<string> SplitSqlIntoBlocks(string sqlContent, string blockDelimiter)
    {
        if (string.IsNullOrWhiteSpace(blockDelimiter))
        {
            // No delimiter - treat entire content as one block
            return string.IsNullOrWhiteSpace(sqlContent)
                ? new List<string>()
                : new List<string> { sqlContent.Trim() };
        }

        // Split by delimiter on its own line (case-insensitive)
        var pattern = $@"^\s*{Regex.Escape(blockDelimiter)}\s*$";
        var blocks = Regex.Split(sqlContent, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        return blocks.Count > 0 ? blocks : (string.IsNullOrWhiteSpace(sqlContent) ? new List<string>() : new List<string> { sqlContent.Trim() });
    }

    /// <summary>
    /// Determines whether block splitting should be skipped for a migration file.
    /// CLI tools execute the entire file as a single unit — splitting is unnecessary.
    /// Returns true when file-level UseCliToolAlias is set (TOML or migsettings),
    /// or when all targets in the file's target group use CLI tools (appsettings cascade).
    /// </summary>
    internal static bool ShouldSkipBlockSplitting(string? fileUseCliToolAlias, string targetGroupAlias, ProductOptions productOptions)
    {
        // Case 1: File-level UseCliToolAlias from TOML or migsettings — always CLI for all targets
        if (!string.IsNullOrWhiteSpace(fileUseCliToolAlias))
            return true;

        // Case 2: All targets in the target group use CLI tools (appsettings PostConfigure cascade)
        var targetGroup = productOptions.TargetGroups?
            .FirstOrDefault(tg => string.Equals(tg.Alias, targetGroupAlias, StringComparison.OrdinalIgnoreCase));

        if (targetGroup?.Targets != null && targetGroup.Targets.Count > 0)
        {
            return targetGroup.Targets.All(t => !string.IsNullOrWhiteSpace(t.UseCliToolAlias));
        }

        return false;
    }

    /// <summary>
    /// Determines the SQL block delimiter for a file based on its target group's database type.
    /// </summary>
    private string GetBlockDelimiter(string relativePath, ProductOptions productOptions)
    {
        try
        {
            relativePath.GetReleaseVersionAndTargetGroupAlias(
                _options.Value, _ctxAccessor.Current.RayMigratorConsoleOptions.Product,
                out _, out string targetGroupAlias);

            var targetGroup = productOptions.TargetGroups!
                .FirstOrDefault(tg => tg.Alias == targetGroupAlias);

            if (targetGroup?.DatabaseType != null &&
                _ctxAccessor.Current.DalSpecificPropertiesDictionary.TryGetValue(targetGroup.DatabaseType, out var dalProps))
            {
                return dalProps.SqlBlockDelimiter;
            }
        }
        catch
        {
            // If we can't determine the delimiter, fall back to default
        }

        return "GO"; // Default to SQL Server delimiter
    }

    #endregion File Discovery & Parsing

    #region SQL Execution

    #region CLI Tool Execution

    /// <summary>
    /// Resolves the effective UseCliToolAlias for a file+target combination.
    /// File-level (TOML/migsettings) wins over target-level (PostConfigure cascade).
    /// Returns null if no CLI tool should be used (DAL execution).
    /// </summary>
    internal static string? ResolveUseCliToolAlias(MigrationFileInfo file, TargetOptions targetOptions)
    {
        return file.UseCliToolAlias ?? targetOptions.UseCliToolAlias;
    }

    /// <summary>
    /// Looks up a CliToolOptions by alias from the global CliTools configuration.
    /// </summary>
    private CliToolOptions GetCliToolByAlias(string alias)
    {
        var cliTools = _options.Value.CliTools;
        if (cliTools == null || cliTools.Count == 0)
        {
            throw new ConfigurationValidationException(
                $"UseCliToolAlias '{alias}' is specified but no CliTools are defined in configuration.");
        }

        var tool = cliTools.FirstOrDefault(t =>
            string.Equals(t.Alias, alias, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
        {
            throw new ConfigurationValidationException(
                $"UseCliToolAlias '{alias}' references a CLI tool that is not defined in CliTools[]. " +
                $"Available tools: {string.Join(", ", cliTools.Where(t => t.Alias != null).Select(t => t.Alias))}.");
        }

        return tool;
    }

    /// <summary>
    /// Builds the resolved argument string by substituting {FilePath} and CliToolParameters placeholders.
    /// Values are substituted verbatim — no quoting or escaping is applied.
    /// If file paths or parameter values contain spaces, the user must handle quoting
    /// in the ArgumentTemplate (e.g., <c>-i "{FilePath}"</c>).
    /// </summary>
    internal static string ResolveCliToolArguments(CliToolOptions tool, TargetOptions target, string filePath)
    {
        var args = tool.ArgumentTemplate!;

        // Replace {FilePath} placeholder
        args = args.Replace("{FilePath}", filePath);

        // Replace custom placeholders from CliToolParameters
        if (target.CliToolParameters != null)
        {
            foreach (var param in target.CliToolParameters)
            {
                args = args.Replace($"{{{param.Key}}}", param.Value ?? string.Empty);
            }
        }

        return args;
    }

    /// <summary>
    /// Executes a migration file using an external CLI tool instead of the DAL.
    /// The entire file is executed as a single unit (no block-wise execution).
    /// </summary>
    internal async Task<(int succeededBlocks, int failedBlocks)> ExecuteWithCliTool(
        MigrationFileInfo file,
        TargetGroupOptions targetGroupOptions,
        TargetOptions targetOptions,
        int migrationId,
        MigrationRunMode runMode,
        CliToolOptions cliToolOptions)
    {
        if (!runMode.ShouldExecuteSql())
        {
            _logger.LogInformation(
                "[{RunMode}] Would execute CLI tool '{CliTool}' | Product: {Product} | Env: {Environment} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId}",
                runMode, cliToolOptions.Alias,
                _ctxAccessor.Current.RayMigratorConsoleOptions.Product,
                _ctxAccessor.Current.RayMigratorConsoleOptions.Environment,
                targetGroupOptions.Alias, targetOptions.Alias, file.Filename, migrationId);

            if (runMode.ShouldWriteRepository())
            {
                await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                    migrationId, MigrationStatus.Executing, 1));
            }

            return (file.FileUpBlocksTotal, 0);
        }

        // Build arguments with placeholder substitution
        var resolvedArguments = ResolveCliToolArguments(cliToolOptions, targetOptions, file.FullPath);

        // Read file content for Stdin mode
        string? fileContent = null;
        if (cliToolOptions.InputModeEnum == CliToolInputMode.Stdin)
        {
            fileContent = await File.ReadAllTextAsync(file.FullPath);
        }

        var request = new CliToolExecutionRequest
        {
            ExecutablePath = cliToolOptions.ExecutablePath!,
            Arguments = resolvedArguments,
            InputMode = cliToolOptions.InputModeEnum,
            FileContent = fileContent,
            FilePath = file.FullPath,
            Filename = file.Filename,
            TimeoutInSeconds = cliToolOptions.CliToolTimeoutInSeconds ?? 120,
            ExitCodeMatcher = cliToolOptions.ExitCodeMatcherInstance
        };

        // Update repository: mark as executing
        await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
            migrationId, MigrationStatus.Executing, 1));

        var result = await _cliToolExecutor.ExecuteAsync(request);

        if (result.Success)
        {
            return (file.FileUpBlocksTotal, 0);
        }

        // CLI tool failed
        throw new MigrationExecutionException(
            $"CLI tool '{cliToolOptions.Alias}' failed for file '{file.Filename}' on target '{targetOptions.Alias}': " +
            $"ExitCode={result.ExitCode}. {result.ErrorMessage}" +
            (string.IsNullOrWhiteSpace(result.StandardError)
                ? string.Empty
                : $" stderr: {result.StandardError.Substring(0, Math.Min(result.StandardError.Length, 500))}"));
    }

    #endregion CLI Tool Execution

    /// <summary>
    /// Executes SQL blocks of a migration file against a target database.
    /// When <paramref name="ignoreBlockErrors"/> is true, failed blocks are logged and skipped
    /// instead of throwing. Returns the count of succeeded and failed blocks.
    /// </summary>
    internal async Task<(int succeededBlocks, int failedBlocks, bool atomicCommitCompleted)> ExecuteSqlBlocks(
        MigrationFileInfo file,
        TargetGroupOptions targetGroupOptions,
        TargetOptions targetOptions,
        int migrationId,
        MigrationRunMode runMode,
        bool ignoreBlockErrors = false,
        int startFromBlock = 0)
    {
        int succeededBlocks = 0;
        int failedBlocks = 0;

        if (startFromBlock > 0)
        {
            _logger.LogInformation(
                "Skipping blocks 1-{SkippedCount}, resuming from block {ResumeBlock}/{Total} in {Filename} on target {Target}",
                startFromBlock, startFromBlock + 1, file.FileUpBlocksTotal, file.Filename, targetOptions.Alias);
        }

        if (!runMode.ShouldExecuteSql())
        {
            _logger.LogInformation(
                "[{RunMode}] Would execute | Product: {Product} | Env: {Environment} | TargetGroup: {TargetGroup} | Target: {Target} | File: {Filename} | MigrationId: {MigrationId} | SqlBlocks: {SqlBlocksTotal}",
                runMode, _ctxAccessor.Current.RayMigratorConsoleOptions.Product, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, targetGroupOptions.Alias, targetOptions.Alias, file.Filename, migrationId, file.FileUpBlocksTotal);

            for (int i = startFromBlock; i < file.SqlBlocks.Count; i++)
            {
                string sqlBlock = ReplaceEnvironmentVariablesInSqlBlock(
                    file.SqlBlocks[i], file.Filename, i + 1, file.FileUpBlocksTotal);

                _logger.LogTrace("[{RunMode}] SQL block {Block}/{Total} from {Filename}:\n{SqlContent}",
                    runMode, i + 1, file.FileUpBlocksTotal, file.Filename,
                    SensitiveDataMasker.Mask(sqlBlock));

                _ctxAccessor.Current.MigrationState.FileBlockId = i + 1;

                if (runMode.ShouldWriteRepository())
                {
                    await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                        migrationId, MigrationStatus.Executing, i + 1));
                }

                succeededBlocks++;
            }
            return (succeededBlocks, failedBlocks, false);
        }

        // Get DAL for the target database
        if (!DalFactory.TryGetDal(targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!, out var targetDal))
        {
            throw new TemplateExecutionException(
                $"Cannot create DAL for database type [{targetGroupOptions.DatabaseType}]");
        }

        var dalSettings = new DalSettings
        {
            UseTransaction = file.UseTransaction,
            DbCommandTimeoutInSeconds = targetOptions.DbCommandTimeoutInSeconds ?? 20,
            MaxRetries = targetOptions.DbCommandMaxRetries ?? 0,
            RetryDelayMs = targetOptions.DbCommandWaitTimeInMsBeforeRetry ?? 250
        };
        _logger.LogTrace("DalSettings for {Filename} on target {Target}: UseTransaction={UseTransaction}, Timeout={Timeout}s, MaxRetries={MaxRetries}, RetryDelayMs={RetryDelayMs}",
            file.Filename, targetOptions.Alias, dalSettings.UseTransaction, dalSettings.DbCommandTimeoutInSeconds, dalSettings.MaxRetries, dalSettings.RetryDelayMs);

        bool useSharedConnection = CanUseSharedConnection(
            file, targetOptions, _ctxAccessor.Current.RayMigratorOptions.Repository!,
            targetGroupOptions.DatabaseType!, ignoreBlockErrors);

        if (useSharedConnection)
        {
            return await ExecuteSqlBlocksAtomic(file, targetDal!, dalSettings, migrationId, runMode, startFromBlock);
        }

        for (int blockIndex = startFromBlock; blockIndex < file.SqlBlocks.Count; blockIndex++)
        {
            string sqlBlock = ReplaceEnvironmentVariablesInSqlBlock(
                file.SqlBlocks[blockIndex], file.Filename, blockIndex + 1, file.FileUpBlocksTotal);
            _ctxAccessor.Current.MigrationState.FileBlockId = blockIndex + 1;

            _logger.LogDebug("Executing block {Block}/{Total} from {Filename} against target {Target}",
                blockIndex + 1, file.FileUpBlocksTotal, file.Filename, targetOptions.Alias);

            _logger.LogTrace("SQL block {Block}/{Total} from {Filename}:\n{SqlContent}",
                blockIndex + 1, file.FileUpBlocksTotal, file.Filename,
                SensitiveDataMasker.Mask(sqlBlock));

            if (ignoreBlockErrors)
            {
                try
                {
                    await targetDal!.ExecuteNonQueryAsync(sqlBlock, dalSettings);
                    succeededBlocks++;
                }
                catch (Exception blockEx)
                {
                    failedBlocks++;
                    _logger.LogWarning(blockEx,
                        "MigrationErrorAction=Ignore: Block {Block}/{Total} from {Filename} failed on target {Target}. Continuing with next block.",
                        blockIndex + 1, file.FileUpBlocksTotal, file.Filename, targetOptions.Alias);
                }
            }
            else
            {
                await targetDal!.ExecuteNonQueryAsync(sqlBlock, dalSettings);
                succeededBlocks++;
            }

            // Update block progress in repository
            await Task.Run(() => _templateExecutor.RepositoryMigrationUpdate(
                migrationId, MigrationStatus.Executing, blockIndex + 1));
        }

        return (succeededBlocks, failedBlocks, false);
    }

    /// <summary>
    /// Executes SQL blocks and the final repository status update in a single atomic transaction
    /// on a shared connection. Used when target and repository share the same ConnectionString
    /// and DatabaseType, ensuring that either all blocks + repo update are committed, or nothing is.
    /// </summary>
    private async Task<(int succeededBlocks, int failedBlocks, bool atomicCommitCompleted)> ExecuteSqlBlocksAtomic(
        MigrationFileInfo file,
        IDal targetDal,
        DalSettings dalSettings,
        int migrationId,
        MigrationRunMode runMode,
        int startFromBlock)
    {
        int repoTimeout = (int)_ctxAccessor.Current.RayMigratorOptions.Repository!.DbCommandTimeoutInSeconds!;
        int maxRetries = dalSettings.MaxRetries;
        int attempt = 0;

        _logger.LogDebug(
            "Using atomic shared connection for {Filename} (target and repository share the same database)",
            file.Filename);

        while (true)
        {
            int succeededBlocks = 0;

            await using var connection = targetDal.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                for (int blockIndex = startFromBlock; blockIndex < file.SqlBlocks.Count; blockIndex++)
                {
                    string sqlBlock = ReplaceEnvironmentVariablesInSqlBlock(
                        file.SqlBlocks[blockIndex], file.Filename, blockIndex + 1, file.FileUpBlocksTotal);
                    _ctxAccessor.Current.MigrationState.FileBlockId = blockIndex + 1;

                    _logger.LogDebug("Executing block {Block}/{Total} from {Filename} (atomic mode)",
                        blockIndex + 1, file.FileUpBlocksTotal, file.Filename);

                    _logger.LogTrace("SQL block {Block}/{Total} from {Filename}:\n{SqlContent}",
                        blockIndex + 1, file.FileUpBlocksTotal, file.Filename,
                        SensitiveDataMasker.Mask(sqlBlock));

                    await targetDal.ExecuteNonQueryAsync(
                        sqlBlock, connection, transaction,
                        dalSettings.DbCommandTimeoutInSeconds);
                    succeededBlocks++;

                    // Repository UPDATE on SAME connection+transaction
                    _templateExecutor.RepositoryMigrationUpdate(
                        migrationId, MigrationStatus.Executing, blockIndex + 1,
                        connection, transaction, repoTimeout);
                }

                // Final status update INSIDE the transaction (this is what closes the atomicity gap)
                if (runMode.ShouldWriteRepository())
                {
                    _templateExecutor.RepositoryMigrationUpdate(
                        migrationId, MigrationStatus.Migrated, file.FileUpBlocksTotal,
                        connection, transaction, repoTimeout);
                }

                await transaction.CommitAsync();
                return (succeededBlocks, 0, true);
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* connection may already be broken */ }

                var (isTransient, errorCode) = ((DalBase)targetDal).IsTransient(ex);
                if (isTransient && attempt < maxRetries)
                {
                    attempt++;
                    _logger.LogWarning(ex,
                        "Transient error (code: {ErrorCode}) in atomic execution of {Filename}, attempt {Attempt}/{MaxRetries}. Rolling back and retrying entire file.",
                        errorCode, file.Filename, attempt, maxRetries);
                    await Task.Delay(dalSettings.RetryDelayMs);
                    continue;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Executes rollback SQL blocks and the final repository status update in a single atomic
    /// transaction on a shared connection. Used when target and repository share the same
    /// ConnectionString and DatabaseType for rollback operations.
    /// </summary>
    private async Task ExecuteRollbackBlocksAtomic(
        MigrationFileInfo rollbackFileInfo,
        IDal targetDal,
        DalSettings dalSettings,
        MigrationRecord record,
        MigrationRunMode runMode,
        int rollbackStartBlock)
    {
        int repoTimeout = (int)_ctxAccessor.Current.RayMigratorOptions.Repository!.DbCommandTimeoutInSeconds!;
        int maxRetries = dalSettings.MaxRetries;
        int attempt = 0;

        _logger.LogDebug(
            "Using atomic shared connection for rollback of {Filename} (target and repository share the same database)",
            record.Filename);

        while (true)
        {
            await using var connection = targetDal.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                for (int blockIndex = rollbackStartBlock; blockIndex < rollbackFileInfo.SqlBlocks.Count; blockIndex++)
                {
                    string sqlBlock = ReplaceEnvironmentVariablesInSqlBlock(
                        rollbackFileInfo.SqlBlocks[blockIndex], rollbackFileInfo.Filename, blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal);

                    _logger.LogDebug(
                        "Executing rollback block {Block}/{Total} for migration {MigrationId} ({Filename}) (atomic mode)",
                        blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Id, record.Filename);

                    _logger.LogTrace("Rollback SQL block {Block}/{Total} for {Filename}:\n{SqlContent}",
                        blockIndex + 1, rollbackFileInfo.FileUpBlocksTotal, record.Filename,
                        SensitiveDataMasker.Mask(sqlBlock));

                    await targetDal.ExecuteNonQueryAsync(
                        sqlBlock, connection, transaction,
                        dalSettings.DbCommandTimeoutInSeconds);

                    // Repository UPDATE on SAME connection+transaction
                    _templateExecutor.RepositoryMigrationUpdateRollback(
                        record.Id,
                        MigrationStatus.Executing,
                        rollbackFileInfo.FileUpHash,
                        rollbackFileInfo.FileUpConfigHash,
                        rollbackFileInfo.FileUpBlocksHash,
                        blockIndex + 1,
                        rollbackFileInfo.FileUpBlocksTotal,
                        rollbackFileInfo.FileUpConfigJson,
                        connection, transaction, repoTimeout);
                }

                // Final status update INSIDE the transaction (this is what closes the atomicity gap)
                if (runMode.ShouldWriteRepository())
                {
                    _templateExecutor.RepositoryMigrationUpdateRollback(
                        record.Id,
                        MigrationStatus.NotMigrated,
                        rollbackFileInfo.FileUpHash,
                        rollbackFileInfo.FileUpConfigHash,
                        rollbackFileInfo.FileUpBlocksHash,
                        rollbackFileInfo.FileUpBlocksTotal,
                        rollbackFileInfo.FileUpBlocksTotal,
                        rollbackFileInfo.FileUpConfigJson,
                        connection, transaction, repoTimeout);
                }

                await transaction.CommitAsync();
                return;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* connection may already be broken */ }

                var (isTransient, errorCode) = ((DalBase)targetDal).IsTransient(ex);
                if (isTransient && attempt < maxRetries)
                {
                    attempt++;
                    _logger.LogWarning(ex,
                        "Transient error (code: {ErrorCode}) in atomic rollback of {Filename}, attempt {Attempt}/{MaxRetries}. Rolling back and retrying entire file.",
                        errorCode, record.Filename, attempt, maxRetries);
                    await Task.Delay(dalSettings.RetryDelayMs);
                    continue;
                }

                throw;
            }
        }
    }

    #endregion SQL Execution

    #region Helper Methods

    /// <summary>
    /// Regex patterns for common DDL keywords used to detect DDL statements in migration files.
    /// Used by LogMigrationSafetyWarnings for implicit commit detection on databases without transactional DDL support.
    /// </summary>
    private static readonly Regex DdlPattern = new(
        @"^\s*(CREATE|ALTER|DROP|TRUNCATE|RENAME)\s",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Logs warnings for migration file configurations that may cause transactional safety issues.
    /// Checks for: UseTransaction=false with multi-block files, UseTransaction=false with retries enabled,
    /// DDL statements on databases without transactional DDL support where UseTransaction provides limited protection,
    /// UseTransaction explicitly set when CLI tool execution bypasses transaction control,
    /// rollback actions without transaction wrapping, rollback actions without rollback files,
    /// RunAlways files with hash validation enabled, and Simultaneously mode with rollback actions.
    /// </summary>
    internal void LogMigrationSafetyWarnings(
        List<MigrationFileInfo> filesToMigrate,
        ProductOptions productOptions)
    {
        int warningCount = 0;

        // Rule 2.12 — SIMULTANEOUSLY_WITH_ROLLBACK
        if (productOptions.TargetGroups != null)
        {
            foreach (var tg in productOptions.TargetGroups)
            {
                if (tg.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously)
                {
                    if (IsRollbackAction(productOptions.MigrationErrorActionEnum))
                    {
                        _logger.LogWarning(
                            "[Rule 2.12 SIMULTANEOUSLY_WITH_ROLLBACK] TargetGroup '{TargetGroupAlias}' uses TargetMigrationOrder=Simultaneously " +
                            "with MigrationErrorAction={MigrationErrorAction}. Rollback in Simultaneously mode affects targets in " +
                            "interleaved order, which may produce inconsistent state across targets",
                            tg.Alias, productOptions.MigrationErrorActionEnum);
                        warningCount++;
                    }
                }
            }
        }

        foreach (var file in filesToMigrate)
        {
            // Rule 2.7 — USE_TRANSACTION_IRRELEVANT_WITH_CLI: UseTransaction explicitly set but CLI tool bypasses transaction control
            if (file.UseTransactionExplicitlySet)
            {
                if (!string.IsNullOrWhiteSpace(file.UseCliToolAlias))
                {
                    // File-level CLI alias — always applies to all targets
                    _logger.LogWarning(
                        "[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI] {Filename} has UseTransaction explicitly set but UseCliToolAlias='{CliAlias}' is configured. " +
                        "UseTransaction has no effect when a CLI tool executes the migration.",
                        file.Filename, file.UseCliToolAlias);
                    warningCount++;
                }
                else
                {
                    // Check target-level CLI aliases within the file's target group
                    foreach (var targetGroup in productOptions.TargetGroups!)
                    {
                        if (!string.Equals(targetGroup.Alias, file.TargetGroupAlias, StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (var target in targetGroup.Targets!)
                        {
                            if (!string.IsNullOrWhiteSpace(target.UseCliToolAlias))
                            {
                                _logger.LogWarning(
                                    "[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI] {Filename} has UseTransaction explicitly set but target '{Target}' uses " +
                                    "UseCliToolAlias='{CliAlias}'. UseTransaction has no effect when a CLI tool executes the migration.",
                                    file.Filename, target.Alias, target.UseCliToolAlias);
                                warningCount++;
                            }
                        }
                    }
                }
            }

            // Rule 2.9 — NO_TRANSACTION_MULTI_BLOCK: UseTransaction=false with multiple SQL blocks
            if (!file.UseTransaction && file.SqlBlocks.Count > 1)
            {
                _logger.LogWarning(
                    "[Rule 2.9 NO_TRANSACTION_MULTI_BLOCK] {Filename} has UseTransaction=false with {BlockCount} SQL blocks. " +
                    "Partial failures cannot be atomically rolled back by the database.",
                    file.Filename, file.SqlBlocks.Count);
                warningCount++;
            }

            // Rule 2.10 — NO_TRANSACTION_WITH_RETRIES: UseTransaction=false combined with retries
            foreach (var targetGroup in productOptions.TargetGroups!)
            {
                foreach (var target in targetGroup.Targets!)
                {
                    if (!file.UseTransaction && (target.DbCommandMaxRetries ?? 0) > 0)
                    {
                        _logger.LogWarning(
                            "[Rule 2.10 NO_TRANSACTION_WITH_RETRIES] {Filename} has UseTransaction=false with MaxRetries={Retries} on target {Target}. " +
                            "Retries may cause duplicate execution of non-idempotent statements.",
                            file.Filename, target.DbCommandMaxRetries, target.Alias);
                        warningCount++;
                    }
                }

                // Rule 2.8 — DDL_ON_NON_TRANSACTIONAL_DB: DDL on databases without transactional DDL support with UseTransaction=true
                if (file.UseTransaction
                    && file.SqlBlocks.Any(block => DdlPattern.IsMatch(block))
                    && targetGroup.DatabaseType != null
                    && _ctxAccessor.Current.DalSpecificPropertiesDictionary.TryGetValue(
                        targetGroup.DatabaseType, out var ddlDalProps)
                    && !ddlDalProps.SupportsTransactionalDdl)
                {
                    _logger.LogWarning(
                        "[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB] {Filename} contains DDL statements targeting {DatabaseType}. " +
                        "DDL causes implicit COMMIT — transaction protection is limited.",
                        file.Filename, targetGroup.DatabaseType);
                    warningCount++;
                }
            }

            // Rule 2.1 — ROLLBACK_ACTION_WITHOUT_TRANSACTION
            var effectiveErrorAction = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;
            if (IsRollbackAction(effectiveErrorAction) && file.UseTransaction == false)
            {
                _logger.LogWarning(
                    "[Rule 2.1 ROLLBACK_ACTION_WITHOUT_TRANSACTION] {Filename} uses MigrationErrorAction={MigrationErrorAction} " +
                    "but UseTransaction=false. Rollback semantics are weakened without transaction wrapping — " +
                    "partial changes from failed SQL blocks cannot be automatically reverted",
                    file.Filename, effectiveErrorAction);
                warningCount++;
            }

            // Rule 2.2 — ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE
            if (IsRollbackAction(effectiveErrorAction) && file.RequireRollbackFile == false && file.MigrateDownFileExists == false)
            {
                _logger.LogWarning(
                    "[Rule 2.2 ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE] {Filename} uses MigrationErrorAction={MigrationErrorAction} " +
                    "but RequireRollbackFile=false and no rollback file exists. " +
                    "Rollback for this file will be skipped if an error occurs",
                    file.Filename, effectiveErrorAction);
                warningCount++;
            }

            // Rule 2.6 — RUN_ALWAYS_WITH_HASH_VALIDATION
            if (file.RunAlways == true)
            {
                var targetGroup = productOptions.TargetGroups?.FirstOrDefault(tg =>
                    string.Equals(tg.Alias, file.TargetGroupAlias, StringComparison.OrdinalIgnoreCase));

                if (targetGroup != null &&
                    targetGroup.HashValidationScopeEnum is HashValidationScope.File or HashValidationScope.SqlBlocks)
                {
                    _logger.LogWarning(
                        "[Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION] {Filename} has RunAlways=true but " +
                        "HashValidationScope={HashValidationScope} on TargetGroup '{TargetGroupAlias}'. " +
                        "Hash validation may report false positives for RunAlways files whose content changes between runs",
                        file.Filename, targetGroup.HashValidationScopeEnum, targetGroup.Alias);
                    warningCount++;
                }
            }
        }

        if (warningCount > 0)
        {
            _logger.LogWarning("Safety check: {Count} warning(s) detected. Review migration files before proceeding.",
                warningCount);
        }
    }

    /// <summary>
    /// Returns true if the given MigrationErrorAction is any of the rollback variants.
    /// </summary>
    private static bool IsRollbackAction(MigrationErrorAction action) =>
        action is MigrationErrorAction.Rollback
            or MigrationErrorAction.RollbackErrorOnly
            or MigrationErrorAction.RollbackRelease;

    /// <summary>
    /// Minimum age (in minutes) for an orphaned MigrationRun to be eligible for auto-fix.
    /// Runs newer than this threshold are assumed to be genuinely running and not auto-fixed.
    /// </summary>
    internal const int AutoFixOrphanedRunsThresholdMinutes = 10;

    /// <summary>
    /// Attempts to insert a MigrationRun, auto-fixing orphaned runs if a parallel-run lock is detected.
    /// When RepositoryMigrationRunInsert fails with MigrationAlreadyRunningException, this method
    /// checks for orphaned runs older than AutoFixOrphanedRunsThresholdMinutes and fixes them
    /// before retrying the insert once. If no orphaned runs are found, the original exception is rethrown.
    /// </summary>
    internal async Task RepositoryMigrationRunInsertWithAutoFix(string settingsJson)
    {
        try
        {
            await Task.Run(() => _templateExecutor.RepositoryMigrationRunInsert(settingsJson));
        }
        catch (MigrationAlreadyRunningException)
        {
            int productId = _ctxAccessor.Current.MigrationState.ProductId;
            int environmentId = _ctxAccessor.Current.MigrationState.EnvironmentId;

            var orphanedRows = await Task.Run(() =>
                _templateExecutor.RepositoryMigrationRunSelectOrphaned(productId, environmentId));

            var autoFixable = orphanedRows
                .Where(r => Convert.ToDouble(r["MinutesRunning"]) >= AutoFixOrphanedRunsThresholdMinutes)
                .ToList();

            if (autoFixable.Count == 0)
            {
                _logger.LogError(
                    "Another migration is currently running for product {ProductId} with environment {Environment} ({EnvironmentId}). " +
                    "No orphaned runs found older than {Threshold} minutes. Use 'Fix' command if the previous run has crashed.",
                    productId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, environmentId, AutoFixOrphanedRunsThresholdMinutes);
                throw;
            }

            _logger.LogWarning(
                "Parallel migration detected but {Count} orphaned run(s) found older than {Threshold} minutes. Auto-fixing.",
                autoFixable.Count, AutoFixOrphanedRunsThresholdMinutes);

            foreach (var orphanRow in autoFixable)
            {
                int orphanRunId = Convert.ToInt32(orphanRow["MigrationRunId"]);
                double minutesRunning = Convert.ToDouble(orphanRow["MinutesRunning"]);

                await Task.Run(() =>
                    _templateExecutor.RepositoryMigrationFixOrphaned(orphanRunId, MigrationStatus.NotMigrated));
                await Task.Run(() =>
                    _templateExecutor.RepositoryMigrationRunFixOrphaned(orphanRunId));

                _logger.LogWarning(
                    "Auto-fixed orphaned MigrationRun {RunId} (running for {Minutes:F0} minutes, marked as Error)",
                    orphanRunId, minutesRunning);
            }

            // Retry the insert after fixing orphaned runs
            await Task.Run(() => _templateExecutor.RepositoryMigrationRunInsert(settingsJson));
            _logger.LogInformation("MigrationRun created successfully after auto-fixing orphaned run(s)");
        }
    }

    /// <summary>
    /// Filters out migration files that have already been successfully applied.
    /// Hash comparison respects the per-TargetGroup HashValidationScope setting.
    /// </summary>
    internal List<MigrationFileInfo> FilterAlreadyMigratedFiles(
        List<MigrationFileInfo> migrationFiles, List<MigrationRecord> existingRecords,
        ProductOptions productOptions)
    {
        var result = new List<MigrationFileInfo>();

        foreach (var file in migrationFiles)
        {
            // Check if this file was already successfully migrated
            var existingRecord = existingRecords.FirstOrDefault(r =>
                r.Filename == file.Filename &&
                r.ReleaseVersion == file.ReleaseVersion &&
                r.TargetGroupAlias == file.TargetGroupAlias &&
                r.MigrationStatusId == MigrationStatus.Migrated);

            if (existingRecord != null && !file.RunAlways)
            {
                var scope = ResolveHashValidationScope(file.TargetGroupAlias, productOptions);

                bool hashMatch = scope switch
                {
                    HashValidationScope.Disabled => true,
                    HashValidationScope.SqlBlocks => existingRecord.FileUpBlocksHash == file.FileUpBlocksHash,
                    _ => existingRecord.FileUpHash == file.FileUpHash
                };

                if (hashMatch)
                {
                    _logger.LogDebug("Skipping already-migrated file {Filename} (hash unchanged, scope: {Scope})",
                        file.Filename, scope);
                    continue;
                }

                _logger.LogWarning(
                    "Migration file {Filename} has changed since last execution (hash mismatch, scope: {Scope}). Re-executing.",
                    file.Filename, scope);
            }

            result.Add(file);
        }

        return result;
    }

    /// <summary>
    /// Resolves the effective HashValidationScope for a TargetGroup.
    /// PostConfigure has already merged defaults, so we just read the final value.
    /// </summary>
    internal static HashValidationScope ResolveHashValidationScope(
        string targetGroupAlias, ProductOptions productOptions)
    {
        var targetGroup = productOptions.TargetGroups?
            .FirstOrDefault(tg => string.Equals(tg.Alias, targetGroupAlias, StringComparison.OrdinalIgnoreCase));

        var scope = targetGroup?.HashValidationScopeEnum ?? HashValidationScope.Undefined;

        return scope == HashValidationScope.Undefined
            ? HashValidationScope.File
            : scope;
    }

    /// <summary>
    /// Checks if a previous migration attempt completed all SQL blocks but was not finalized
    /// (status stuck at Executing with BlocksMigrated == BlocksTotal, typically caused by a
    /// crash between target execution and the final Migrated status update).
    /// If found, updates the record to Migrated status in the repository.
    /// Returns the existing record ID if finalized, or -1 if no such record exists.
    /// </summary>
    internal int TryFinalizeCompletedMigration(
        MigrationFileInfo file,
        string targetAlias,
        List<MigrationRecord> existingRecords)
    {
        var completedRecord = existingRecords
            .Where(r => r.Filename == file.Filename
                && r.ReleaseVersion == file.ReleaseVersion
                && r.TargetGroupAlias == file.TargetGroupAlias
                && r.TargetAlias == targetAlias
                && r.MigrationStatusId == MigrationStatus.Executing
                && r.FileUpBlocksMigrated > 0
                && r.FileUpBlocksMigrated >= r.FileUpBlocksTotal
                && r.FileUpBlocksHash == file.FileUpBlocksHash
                && r.FileDownHash == null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();

        if (completedRecord == null)
            return -1;

        _logger.LogWarning(
            "Recovery: Migration {Filename} on target {Target} was fully executed " +
            "({BlocksMigrated}/{BlocksTotal} blocks) but not finalized (status=Executing). " +
            "Finalizing as Migrated now (MigrationId={MigrationId}).",
            file.Filename, targetAlias, completedRecord.FileUpBlocksMigrated,
            completedRecord.FileUpBlocksTotal, completedRecord.Id);

        _templateExecutor.RepositoryMigrationUpdate(
            completedRecord.Id, MigrationStatus.Migrated, completedRecord.FileUpBlocksTotal);

        return completedRecord.Id;
    }

    /// <summary>
    /// Checks if a file+target combination can be resumed from a previous partial execution.
    /// Returns the 0-based block index to start from, or 0 if no resume is possible.
    /// Resume is possible when:
    ///   1. A Failed or Executing record exists for this file+target combination
    ///   2. Some blocks were migrated but not all (partial execution)
    ///   3. No rollback was attempted (FileDownHash is null)
    ///   4. The file's SQL blocks hash has not changed since the partial execution
    /// </summary>
    internal int FindResumableBlock(
        MigrationFileInfo file,
        string targetAlias,
        List<MigrationRecord> existingRecords)
    {
        // Find the most recent Failed/Executing record for this file+target
        var partialRecord = existingRecords
            .Where(r => r.Filename == file.Filename
                && r.ReleaseVersion == file.ReleaseVersion
                && r.TargetGroupAlias == file.TargetGroupAlias
                && r.TargetAlias == targetAlias
                && r.MigrationStatusId is MigrationStatus.Failed or MigrationStatus.Executing
                && r.FileUpBlocksMigrated > 0
                && r.FileUpBlocksMigrated < r.FileUpBlocksTotal
                && r.FileDownHash == null)  // No rollback attempted
            .OrderByDescending(r => r.Id)   // Most recent record
            .FirstOrDefault();

        if (partialRecord == null)
            return 0;

        // Hash comparison: file must not have changed since partial execution
        if (partialRecord.FileUpBlocksHash != file.FileUpBlocksHash)
        {
            _logger.LogWarning(
                "File {Filename} has changed since last partial execution (hash mismatch). Re-executing from block 1.",
                file.Filename);
            return 0;
        }

        _logger.LogInformation(
            "Resuming {Filename} on target {Target} from block {NextBlock}/{Total} (previous run executed {Done} blocks successfully)",
            file.Filename, targetAlias, partialRecord.FileUpBlocksMigrated + 1,
            partialRecord.FileUpBlocksTotal, partialRecord.FileUpBlocksMigrated);

        return partialRecord.FileUpBlocksMigrated; // 0-based: skip first N blocks
    }

    /// <summary>
    /// Constructs the rollback filename from a forward migration filename.
    /// E.g., "20_InsertMasterData.sql" -> "20_InsertMasterData.rollback.sql"
    /// </summary>
    internal static string GetRollbackFilename(string filename, string rollbackPreExtension, string fileExtension)
    {
        string baseName = Path.GetFileNameWithoutExtension(filename);
        return $"{baseName}.{rollbackPreExtension}.{fileExtension}";
    }

    /// <summary>
    /// Checks whether a filename is a rollback file based on the pre-extension.
    /// </summary>
    internal static bool IsRollbackFile(string filename, string rollbackPreExtension, string fileExtension)
    {
        return filename.EndsWith($".{rollbackPreExtension}.{fileExtension}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a filename is environment-specific (e.g., "01_ooc_login.Docker.sql").
    /// </summary>
    internal static bool IsEnvironmentSpecificFile(string filename, string fileExtension)
    {
        // Pattern: name.Environment.ext -> more than 2 dot-separated parts
        string withoutExtension = filename;
        if (filename.EndsWith($".{fileExtension}", StringComparison.OrdinalIgnoreCase))
        {
            withoutExtension = filename.Substring(0, filename.Length - fileExtension.Length - 1);
        }

        return withoutExtension.Contains('.');
    }

    /// <summary>
    /// Checks whether an environment-specific file matches the given environment.
    /// </summary>
    internal static bool IsForEnvironment(string filename, string environment, string fileExtension)
    {
        // Extract the environment part: "01_ooc_login.Docker.sql" -> "Docker"
        string withoutExtension = filename;
        if (filename.EndsWith($".{fileExtension}", StringComparison.OrdinalIgnoreCase))
        {
            withoutExtension = filename.Substring(0, filename.Length - fileExtension.Length - 1);
        }

        int lastDot = withoutExtension.LastIndexOf('.');
        if (lastDot < 0) return true;

        string envPart = withoutExtension.Substring(lastDot + 1);
        return string.Equals(envPart, environment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the encoding for reading migration files.
    /// </summary>
    internal static Encoding GetFileEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (Exception ex)
        {
            throw new ConfigurationValidationException(
                $"The configured MigrationFilesEncoding '{encodingName}' is not a valid encoding name. " +
                $"Some encodings (e.g. 'windows-1252') require System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) on .NET Core. " +
                $"Please use a valid encoding name like 'UTF-8' or 'iso-8859-1'.", ex);
        }
    }

    /// <summary>
    /// Detects migration files that would be executed out of order
    /// (files from releases older than the highest already-migrated release).
    /// </summary>
    internal static List<MigrationFileInfo> DetectOutOfOrderFiles(
        List<MigrationFileInfo> filesToMigrate, List<MigrationRecord> existingRecords)
    {
        if (filesToMigrate.Count == 0 || existingRecords.Count == 0)
            return new List<MigrationFileInfo>();

        var migratedRecords = existingRecords
            .Where(r => r.MigrationStatusId == MigrationStatus.Migrated)
            .ToList();

        if (migratedRecords.Count == 0)
            return new List<MigrationFileInfo>();

        string highestMigratedRelease = migratedRecords
            .OrderByDescending(r => r.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.ReleaseVersion)
            .First();

        return filesToMigrate
            .Where(f => string.Compare(f.ReleaseVersion, highestMigratedRelease, StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();
    }

    /// <summary>
    /// Filters migration files up to and including the target release.
    /// Used by Migrate-Up and Baseline to include only releases &lt;= target.
    /// If targetReleaseVersion is null or whitespace, all files are returned.
    /// </summary>
    internal static List<MigrationFileInfo> FilterByTargetRelease(
        List<MigrationFileInfo> files, string? targetReleaseVersion)
    {
        if (string.IsNullOrWhiteSpace(targetReleaseVersion))
            return files;

        return files
            .Where(f => string.Compare(f.ReleaseVersion, targetReleaseVersion,
                StringComparison.OrdinalIgnoreCase) <= 0)
            .ToList();
    }

    /// <summary>
    /// Filters migration files to only those from releases after the target.
    /// Used by Migrate-Down to identify files that need to be rolled back (releases &gt; target).
    /// </summary>
    internal static List<MigrationFileInfo> FilterReleasesAfterTarget(
        List<MigrationFileInfo> files, string targetReleaseVersion)
    {
        return files
            .Where(f => string.Compare(f.ReleaseVersion, targetReleaseVersion,
                StringComparison.OrdinalIgnoreCase) > 0)
            .ToList();
    }

    /// <summary>
    /// Filters migration files to only those belonging to the specified target groups.
    /// If targetGroupAliases is null or empty, all files are returned (no filtering).
    /// </summary>
    internal static List<MigrationFileInfo> FilterByTargetGroups(
        List<MigrationFileInfo> files, string[]? targetGroupAliases)
    {
        if (targetGroupAliases == null || targetGroupAliases.Length == 0)
            return files;

        return files
            .Where(f => targetGroupAliases.Any(alias =>
                string.Equals(f.TargetGroupAlias, alias, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Validates that all specified target group aliases exist in the product configuration.
    /// If targetGroupAliases is null or empty, validation is skipped.
    /// Throws InvalidOperationException on first non-matching alias.
    /// </summary>
    internal static void ValidateTargetGroupAliases(
        string[]? targetGroupAliases, IEnumerable<TargetGroupOptions> targetGroups)
    {
        if (targetGroupAliases == null || targetGroupAliases.Length == 0)
            return;

        var targetGroupList = targetGroups.ToList();

        foreach (var alias in targetGroupAliases)
        {
            bool exists = targetGroupList.Any(tg =>
                string.Equals(tg.Alias, alias, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                var availableGroups = string.Join(", ", targetGroupList.Select(tg => $"'{tg.Alias}'"));
                throw new InvalidOperationException(
                    $"Target group '{alias}' not found in product configuration. " +
                    $"Available target groups: {availableGroups}");
            }
        }
    }

    /// <summary>
    /// Validates that no release contains a mix of flat-layout files (directly under the release dir)
    /// and traditional-layout files (under a target group subdirectory).
    /// Only relevant for products with a single TargetGroup.
    /// Throws <see cref="ConfigurationValidationException"/> when ambiguity is detected.
    /// </summary>
    internal static void ValidateTargetGroupAliasCasing(string rootDirectory, ProductOptions productOptions)
    {
        if (productOptions.TargetGroups == null || productOptions.TargetGroups.Count == 0)
            return;

        var tgAliases = productOptions.TargetGroups.Select(tg => tg.Alias!).ToList();

        foreach (var releaseDir in Directory.EnumerateDirectories(rootDirectory))
        {
            foreach (var subDir in Directory.EnumerateDirectories(releaseDir))
            {
                string subDirName = Path.GetFileName(subDir);
                foreach (var tgAlias in tgAliases)
                {
                    if (subDirName.Equals(tgAlias, StringComparison.OrdinalIgnoreCase)
                        && !subDirName.Equals(tgAlias, StringComparison.Ordinal))
                    {
                        throw new ConfigurationValidationException(
                            $"Directory [{subDirName}] in release [{Path.GetFileName(releaseDir)}] matches " +
                            $"TargetGroup alias [{tgAlias}] case-insensitively but differs in case. " +
                            $"Rename the directory to [{tgAlias}].");
                    }
                }
            }
        }
    }

    internal static void ValidateFlatLayoutAmbiguity(
        List<MigrationFileInfo> migrationFiles, string singleTgAlias, string productAlias)
    {
        var byRelease = migrationFiles.GroupBy(f => f.ReleaseVersion);
        foreach (var releaseGroup in byRelease)
        {
            bool hasFlat = false;
            bool hasTraditional = false;
            foreach (var file in releaseGroup)
            {
                var segments = file.FilenameWithRelativePath.GetPathSegments();
                if (segments.Length >= 3 && segments[1].Equals(singleTgAlias, StringComparison.OrdinalIgnoreCase))
                {
                    hasTraditional = true;
                }
                else
                {
                    hasFlat = true;
                }
                if (hasFlat && hasTraditional) break;
            }
            if (hasFlat && hasTraditional)
            {
                throw new ConfigurationValidationException(
                    $"Ambiguous directory layout in release [{releaseGroup.Key}] for product [{productAlias}]: " +
                    $"migration files found both directly in the release directory (flat layout) " +
                    $"and in the [{singleTgAlias}] subdirectory. Use one layout per release.");
            }
        }
    }

    /// <summary>
    /// Loads and merges migsettings.txt defaults from all directories in the migration root.
    /// For each directory, the base migsettings.txt is merged with the environment-specific variant.
    /// </summary>
    internal Dictionary<string, MigSettingsEntry> LoadMigSettingsDefaults(
        string rootDirectory, string environment, string fileExtension)
    {
        var result = new Dictionary<string, MigSettingsEntry>(StringComparer.OrdinalIgnoreCase);

        // Find all migsettings files
        var settingsFiles = Directory.EnumerateFiles(rootDirectory, "migsettings*.txt", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                // Match migsettings.txt or migsettings.{Environment}.txt
                return string.Equals(name, "migsettings.txt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, $"migsettings.{environment}.txt", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // Group by directory
        var byDirectory = settingsFiles
            .GroupBy(f => Path.GetDirectoryName(f)!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byDirectory)
        {
            var dirPath = group.Key;
            MigSettingsEntry? baseEntry = null;
            MigSettingsEntry? envEntry = null;

            foreach (var filePath in group)
            {
                var fileName = Path.GetFileName(filePath);
                var entry = ParseMigSettingsFile(filePath);

                if (string.Equals(fileName, "migsettings.txt", StringComparison.OrdinalIgnoreCase))
                    baseEntry = entry;
                else
                    envEntry = entry;
            }

            // Merge: environment-specific overrides base
            var merged = new MigSettingsEntry();
            if (baseEntry != null)
            {
                merged.UseTransaction = baseEntry.UseTransaction;
                merged.RunAlways = baseEntry.RunAlways;
                merged.RequireRollbackFile = baseEntry.RequireRollbackFile;
                merged.StopRollbackOnMissingRollbackFile = baseEntry.StopRollbackOnMissingRollbackFile;
                merged.Environments = baseEntry.Environments;
                merged.Targets = baseEntry.Targets;
                merged.MigrationErrorAction = baseEntry.MigrationErrorAction;
                merged.RollbackErrorAction = baseEntry.RollbackErrorAction;
                merged.UseCliToolAlias = baseEntry.UseCliToolAlias;
                merged.TargetGroupMigrationOrder = baseEntry.TargetGroupMigrationOrder;
            }
            if (envEntry != null)
            {
                if (envEntry.UseTransaction.HasValue) merged.UseTransaction = envEntry.UseTransaction;
                if (envEntry.RunAlways.HasValue) merged.RunAlways = envEntry.RunAlways;
                if (envEntry.RequireRollbackFile.HasValue) merged.RequireRollbackFile = envEntry.RequireRollbackFile;
                if (envEntry.StopRollbackOnMissingRollbackFile.HasValue) merged.StopRollbackOnMissingRollbackFile = envEntry.StopRollbackOnMissingRollbackFile;
                if (envEntry.Environments != null) merged.Environments = envEntry.Environments;
                if (envEntry.Targets != null) merged.Targets = envEntry.Targets;
                if (envEntry.MigrationErrorAction.HasValue) merged.MigrationErrorAction = envEntry.MigrationErrorAction;
                if (envEntry.RollbackErrorAction.HasValue) merged.RollbackErrorAction = envEntry.RollbackErrorAction;
                if (envEntry.UseCliToolAlias != null) merged.UseCliToolAlias = envEntry.UseCliToolAlias;
                if (envEntry.TargetGroupMigrationOrder != null) merged.TargetGroupMigrationOrder = envEntry.TargetGroupMigrationOrder;
            }

            result[dirPath] = merged;
        }

        return result;
    }

    /// <summary>
    /// Parses a migsettings.txt file. These files use [RayMigrator] section header directly (no /* */ wrapper).
    /// </summary>
    internal MigSettingsEntry ParseMigSettingsFile(string filePath)
    {
        var entry = new MigSettingsEntry();
        var content = File.ReadAllText(filePath);

        // Strip the [RayMigrator] header
        var sectionMatch = Regex.Match(content, @"\[RayMigrator\]\s*\n?(.*)", RegexOptions.Singleline);
        if (!sectionMatch.Success)
            return entry;

        var tomlContent = sectionMatch.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(tomlContent))
            return entry;

        ParseTomlConfig(tomlContent, out bool useTransaction, out string description,
            out List<string>? environments, out List<string>? targets, out bool runAlways,
            out bool? requireRollbackFile, out MigrationErrorAction? migrationErrorAction,
            out RollbackErrorAction? rollbackErrorAction,
            out string? useCliToolAlias, out List<string>? tgeo,
            out bool? stopRollbackOnMissingRollbackFile);

        // Only set values that were explicitly present in the file
        foreach (var line in tomlContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0) continue;
            var key = trimmed.Substring(0, equalsIndex).Trim().ToLowerInvariant();

            switch (key)
            {
                case "usetransaction": entry.UseTransaction = useTransaction; break;
                case "runalways": entry.RunAlways = runAlways; break;
                case "requirerollbackfile": entry.RequireRollbackFile = requireRollbackFile; break;
                case "stoprollbackonmissingrollbackfile": entry.StopRollbackOnMissingRollbackFile = stopRollbackOnMissingRollbackFile; break;
                case "environments": entry.Environments = environments; break;
                case "targets": entry.Targets = targets; break;
                case "migrationerroraction": entry.MigrationErrorAction = migrationErrorAction; break;
                case "rollbackerroraction": entry.RollbackErrorAction = rollbackErrorAction; break;
                case "useclitoolalias": entry.UseCliToolAlias = useCliToolAlias; break;
                case "targetgroupmigrationorder": entry.TargetGroupMigrationOrder = tgeo; break;
            }
        }

        return entry;
    }

    /// <summary>
    /// Resolves the effective MigSettings defaults for a file by walking up from its directory to the root.
    /// More specific directories override less specific ones.
    /// </summary>
    internal MigSettingsEntry? ResolveMigSettingsForFile(
        string fileDirectory, string rootDirectory, Dictionary<string, MigSettingsEntry> migSettings)
    {
        if (migSettings.Count == 0)
            return null;

        // Normalize path separators — Path.GetDirectoryName (used in the walk loop)
        // returns OS-native separators, so all inputs must use the same format
        fileDirectory = fileDirectory.NormalizePath();
        rootDirectory = rootDirectory.NormalizePath();

        var normalizedSettings = new Dictionary<string, MigSettingsEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in migSettings)
            normalizedSettings[kvp.Key.NormalizePath()] = kvp.Value;

        // Collect applicable entries from root down to file directory
        var applicableEntries = new List<MigSettingsEntry>();
        var currentDir = fileDirectory;

        // Walk from file directory upward to root, collecting entries
        var dirStack = new Stack<string>();
        while (true)
        {
            if (normalizedSettings.TryGetValue(currentDir, out var entry))
                dirStack.Push(currentDir);

            if (string.Equals(currentDir, rootDirectory, StringComparison.OrdinalIgnoreCase))
                break;

            var parent = Path.GetDirectoryName(currentDir);
            if (parent == null || string.Equals(parent, currentDir, StringComparison.OrdinalIgnoreCase))
                break;

            currentDir = parent;
        }

        // Now pop from stack: root first, most specific last
        while (dirStack.Count > 0)
        {
            var dir = dirStack.Pop();
            applicableEntries.Add(normalizedSettings[dir]);
        }

        if (applicableEntries.Count == 0)
            return null;

        // Merge: root → release → targetGroup (more specific overrides less specific)
        var merged = new MigSettingsEntry();
        foreach (var entry in applicableEntries)
        {
            if (entry.UseTransaction.HasValue) merged.UseTransaction = entry.UseTransaction;
            if (entry.RunAlways.HasValue) merged.RunAlways = entry.RunAlways;
            if (entry.RequireRollbackFile.HasValue) merged.RequireRollbackFile = entry.RequireRollbackFile;
            if (entry.StopRollbackOnMissingRollbackFile.HasValue) merged.StopRollbackOnMissingRollbackFile = entry.StopRollbackOnMissingRollbackFile;
            if (entry.Environments != null) merged.Environments = entry.Environments;
            if (entry.Targets != null) merged.Targets = entry.Targets;
            if (entry.MigrationErrorAction.HasValue) merged.MigrationErrorAction = entry.MigrationErrorAction;
            if (entry.RollbackErrorAction.HasValue) merged.RollbackErrorAction = entry.RollbackErrorAction;
            if (entry.UseCliToolAlias != null) merged.UseCliToolAlias = entry.UseCliToolAlias;
            if (entry.TargetGroupMigrationOrder != null) merged.TargetGroupMigrationOrder = entry.TargetGroupMigrationOrder;
        }

        return merged;
    }

    /// <summary>
    /// Replaces {ENV:VARIABLE_NAME} placeholders in a SQL block with their corresponding
    /// environment variable values. Replacement happens at execution time to preserve
    /// hash integrity (hashes are computed on the original SQL content).
    /// </summary>
    internal string ReplaceEnvironmentVariablesInSqlBlock(string sqlBlock, string filename, int blockIndex, int totalBlocks)
    {
        if (EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
                sqlBlock, out var replacedBlock, out var replacements))
        {
            foreach (var replacement in replacements)
            {
                if (replacement.VariableValue == null)
                {
                    _logger.LogWarning(
                        "Environment variable '{VariableName}' in SQL block {Block}/{Total} of {Filename} is not set (replaced with empty string)",
                        replacement.VariableName, blockIndex, totalBlocks, filename);
                }
                else
                {
                    _logger.LogTrace(
                        "Replaced environment variable '{VariableName}' in SQL block {Block}/{Total} of {Filename}",
                        replacement.VariableName, blockIndex, totalBlocks, filename);
                }
            }
            return replacedBlock!;
        }
        return sqlBlock;
    }

    #region TargetGroupMigrationOrder

    /// <summary>
    /// Parses a comma-separated string of TargetGroup aliases into a string array.
    /// Returns null if input is null or whitespace-only.
    /// </summary>
    internal static string[]? ParseTargetGroupMigrationOrder(string? commaSeparated)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated))
            return null;

        var result = commaSeparated.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        return result.Length > 0 ? result : null;
    }

    /// <summary>
    /// Validates the execution order against the configured TargetGroups and returns a reordered list.
    /// All TargetGroup aliases must be specified exactly once (case-sensitive), and the product must have more than one TargetGroup.
    /// </summary>
    internal static List<TargetGroupOptions> ValidateAndReorderTargetGroups(
        string[] executionOrder, List<TargetGroupOptions> targetGroups, string source)
    {
        // 1. Not allowed when product has only one TargetGroup
        if (targetGroups.Count <= 1)
        {
            throw new ConfigurationValidationException(
                $"TargetGroupMigrationOrder (source: {source}) is not allowed when product has only {targetGroups.Count} TargetGroup(s).");
        }

        // 2. Count must match
        if (executionOrder.Length != targetGroups.Count)
        {
            throw new ConfigurationValidationException(
                $"TargetGroupMigrationOrder (source: {source}) specifies {executionOrder.Length} aliases but product has {targetGroups.Count} TargetGroups. All TargetGroup aliases must be specified.");
        }

        // 3. Check for duplicates
        var seen = new HashSet<string>();
        foreach (var alias in executionOrder)
        {
            if (!seen.Add(alias))
            {
                throw new ConfigurationValidationException(
                    $"TargetGroupMigrationOrder (source: {source}) contains duplicate alias '{alias}'.");
            }
        }

        // 4. Match each alias to a TargetGroup
        var result = new List<TargetGroupOptions>(executionOrder.Length);
        foreach (var alias in executionOrder)
        {
            // Exact case match
            var match = targetGroups.FirstOrDefault(tg => tg.Alias == alias);
            if (match != null)
            {
                result.Add(match);
                continue;
            }

            // Case-insensitive match
            var caseInsensitiveMatch = targetGroups.FirstOrDefault(tg =>
                string.Equals(tg.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (caseInsensitiveMatch != null)
            {
                throw new ConfigurationValidationException(
                    $"TargetGroupMigrationOrder (source: {source}) alias '{alias}' matches TargetGroup '{caseInsensitiveMatch.Alias}' " +
                    $"case-insensitively but not case-sensitively. Use the exact alias '{caseInsensitiveMatch.Alias}'.");
            }

            // No match at all
            var availableAliases = string.Join(", ", targetGroups.Select(tg => tg.Alias));
            throw new ConfigurationValidationException(
                $"TargetGroupMigrationOrder (source: {source}) contains unknown alias '{alias}'. Available TargetGroups: {availableAliases}.");
        }

        return result;
    }

    /// <summary>
    /// Resolves the effective TargetGroup execution order using the override chain: CLI > migsettings > appsettings > null (config array order).
    /// </summary>
    internal string[]? ResolveTargetGroupMigrationOrder(
        string releaseDirectory, ProductOptions productOptions,
        Dictionary<string, MigSettingsEntry> migSettings, string[]? cliOrder)
    {
        // 1. CLI override (highest priority)
        if (cliOrder is { Length: > 0 })
        {
            _logger.LogDebug("TargetGroupMigrationOrder resolved from CLI: [{Order}]", string.Join(", ", cliOrder));
            return cliOrder;
        }

        // 2. migsettings for the release directory
        var normalizedDir = releaseDirectory.NormalizePath();
        var normalizedSettings = new Dictionary<string, MigSettingsEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in migSettings)
            normalizedSettings[kvp.Key.NormalizePath()] = kvp.Value;

        if (normalizedSettings.TryGetValue(normalizedDir, out var entry) && entry.TargetGroupMigrationOrder is { Count: > 0 })
        {
            var order = entry.TargetGroupMigrationOrder.ToArray();
            _logger.LogDebug("TargetGroupMigrationOrder resolved from migsettings: [{Order}]", string.Join(", ", order));
            return order;
        }

        // 3. appsettings (product-level comma-separated)
        var parsed = ParseTargetGroupMigrationOrder(productOptions.TargetGroupMigrationOrder);
        if (parsed != null)
        {
            _logger.LogDebug("TargetGroupMigrationOrder resolved from appsettings: [{Order}]", string.Join(", ", parsed));
            return parsed;
        }

        // 4. null — use config array order
        return null;
    }

    #endregion TargetGroupMigrationOrder

    #endregion Helper Methods

    #region Stub Methods (not yet fully implemented)

    public async Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing Validate-Hash for product {Product} with scope {Scope}",
                request.ProductAlias, request.HashValidationScope?.ToString() ?? "per-TargetGroup config");

            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Phase 1: Initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            // --- Phase 2: File Discovery ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
            var migrationFiles = DiscoverAndPrepareMigrationFiles(
                productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment);

            // Filter by target group if specified
            ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
            migrationFiles = FilterByTargetGroups(migrationFiles, request.TargetGroupAliases);

            // --- Phase 3: Query existing records from repository ---
            var existingRecords = await Task.Run(() => _templateExecutor.RepositoryMigrationSelect());

            // --- Phase 4: Compare files with repository records ---
            var issues = new List<HashValidationIssue>();
            int validFiles = 0;
            int invalidFiles = 0;
            int missingFiles = 0;

            // Check each file on disk against repository
            foreach (var file in migrationFiles)
            {
                // Resolve effective scope: CLI override > TargetGroup config > File default
                var effectiveScope = request.HashValidationScope
                    ?? ResolveHashValidationScope(file.TargetGroupAlias, productOptions);

                if (effectiveScope == HashValidationScope.Disabled)
                {
                    _logger.LogDebug("Skipping hash validation for {Filename} (scope: Disabled, TargetGroup: {TargetGroup})",
                        file.Filename, file.TargetGroupAlias);
                    validFiles++;
                    continue;
                }

                var matchingRecord = existingRecords.FirstOrDefault(r =>
                    r.Filename == file.Filename &&
                    r.ReleaseVersion == file.ReleaseVersion &&
                    r.TargetGroupAlias == file.TargetGroupAlias &&
                    r.MigrationStatusId == MigrationStatus.Migrated);

                if (matchingRecord == null)
                {
                    // File exists on disk but not in repository (not yet migrated)
                    issues.Add(new HashValidationIssue
                    {
                        FileName = file.Filename,
                        IssueType = "New",
                        ActualHash = file.FileUpHash,
                        Details = $"File exists on disk but has not been migrated yet (Release: {file.ReleaseVersion}, TargetGroup: {file.TargetGroupAlias})"
                    });
                    continue;
                }

                // Compare hashes based on effective validation scope
                bool hashMatch = effectiveScope switch
                {
                    HashValidationScope.SqlBlocks => matchingRecord.FileUpBlocksHash == file.FileUpBlocksHash,
                    _ => matchingRecord.FileUpHash == file.FileUpHash
                };

                if (hashMatch)
                {
                    validFiles++;
                }
                else
                {
                    invalidFiles++;
                    string expectedHash = effectiveScope == HashValidationScope.SqlBlocks
                        ? matchingRecord.FileUpBlocksHash
                        : matchingRecord.FileUpHash;
                    string actualHash = effectiveScope == HashValidationScope.SqlBlocks
                        ? file.FileUpBlocksHash
                        : file.FileUpHash;

                    issues.Add(new HashValidationIssue
                    {
                        FileName = file.Filename,
                        IssueType = "Modified",
                        ExpectedHash = expectedHash,
                        ActualHash = actualHash,
                        Details = $"Hash mismatch detected for file in Release: {file.ReleaseVersion}, TargetGroup: {file.TargetGroupAlias} (Scope: {effectiveScope})"
                    });
                }
            }

            // Check each repository record for files that no longer exist on disk
            var migratedRecords = existingRecords
                .Where(r => r.MigrationStatusId == MigrationStatus.Migrated)
                .Where(r => request.TargetGroupAliases == null || request.TargetGroupAliases.Length == 0 ||
                    request.TargetGroupAliases.Any(alias =>
                        string.Equals(r.TargetGroupAlias, alias, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var record in migratedRecords)
            {
                bool fileExistsOnDisk = migrationFiles.Any(f =>
                    f.Filename == record.Filename &&
                    f.ReleaseVersion == record.ReleaseVersion &&
                    f.TargetGroupAlias == record.TargetGroupAlias);

                if (!fileExistsOnDisk)
                {
                    missingFiles++;
                    issues.Add(new HashValidationIssue
                    {
                        FileName = record.Filename,
                        IssueType = "Missing",
                        ExpectedHash = record.FileUpHash,
                        Details = $"File was migrated but no longer exists on disk (Release: {record.ReleaseVersion}, TargetGroup: {record.TargetGroupAlias})"
                    });
                }
            }

            // --- Phase 5: Build result ---
            bool hasIssues = invalidFiles > 0 || missingFiles > 0;
            var result = new ValidationResult
            {
                Success = !hasIssues,
                ProductAlias = request.ProductAlias,
                Duration = DateTime.UtcNow - startTime,
                TotalFiles = migrationFiles.Count,
                ValidFiles = validFiles,
                InvalidFiles = invalidFiles,
                MissingFiles = missingFiles,
                Issues = issues,
                Messages = new List<string>
                {
                    $"Validation completed: {validFiles} valid, {invalidFiles} modified, {missingFiles} missing, {issues.Count(i => i.IssueType == "New")} new (not yet migrated)"
                }
            };

            _logger.LogInformation(
                "Validate-Hash completed for product {Product}: {Valid} valid, {Invalid} modified, {Missing} missing",
                request.ProductAlias, validFiles, invalidFiles, missingFiles);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Validate-Hash for product {Product}", request.ProductAlias);
            return new ValidationResult
            {
                Success = false,
                ProductAlias = request.ProductAlias,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                Messages = new List<string> { $"Validation failed: {ex.Message}" }
            };
        }
    }

    public async Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing Update-Hash for product {Product}", request.ProductAlias);

            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Phase 1: Initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            // --- Phase 2: File Discovery ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == request.ProductAlias);
            var migrationFiles = DiscoverAndPrepareMigrationFiles(
                productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment);

            // Filter by target group if specified
            ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
            migrationFiles = FilterByTargetGroups(migrationFiles, request.TargetGroupAliases);

            // --- Phase 3: Query existing records from repository ---
            var existingRecords = await Task.Run(() => _templateExecutor.RepositoryMigrationSelect());

            // --- Phase 4: Compare and update hashes ---
            var updatedFileNames = new List<string>();
            int updatedFiles = 0;
            int newFiles = 0;

            foreach (var file in migrationFiles)
            {
                var matchingRecord = existingRecords.FirstOrDefault(r =>
                    r.Filename == file.Filename &&
                    r.ReleaseVersion == file.ReleaseVersion &&
                    r.TargetGroupAlias == file.TargetGroupAlias &&
                    r.MigrationStatusId == MigrationStatus.Migrated);

                if (matchingRecord == null)
                {
                    // File not in repository - count as new
                    newFiles++;
                    continue;
                }

                // Check if hashes differ
                bool hashChanged = matchingRecord.FileUpHash != file.FileUpHash ||
                                   matchingRecord.FileUpConfigHash != file.FileUpConfigHash ||
                                   matchingRecord.FileUpBlocksHash != file.FileUpBlocksHash;

                if (hashChanged)
                {
                    _logger.LogInformation(
                        "Updating hashes for migration {Filename} (Release: {Release}, TargetGroup: {TargetGroup})",
                        file.Filename, file.ReleaseVersion, file.TargetGroupAlias);

                    await Task.Run(() => _templateExecutor.RepositoryMigrationUpdateHash(
                        matchingRecord.Id,
                        file.FileUpHash,
                        file.FileUpConfigHash,
                        file.FileUpBlocksHash));

                    updatedFiles++;
                    updatedFileNames.Add(file.Filename);
                }
            }

            // Count removed files (in repository but not on disk)
            int removedFiles = 0;
            var migratedRecords = existingRecords
                .Where(r => r.MigrationStatusId == MigrationStatus.Migrated)
                .Where(r => request.TargetGroupAliases == null || request.TargetGroupAliases.Length == 0 ||
                    request.TargetGroupAliases.Any(alias =>
                        string.Equals(r.TargetGroupAlias, alias, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var record in migratedRecords)
            {
                bool fileExistsOnDisk = migrationFiles.Any(f =>
                    f.Filename == record.Filename &&
                    f.ReleaseVersion == record.ReleaseVersion &&
                    f.TargetGroupAlias == record.TargetGroupAlias);

                if (!fileExistsOnDisk)
                {
                    removedFiles++;
                }
            }

            // --- Phase 5: Build result ---
            var result = new HashUpdateResult
            {
                Success = true,
                ProductAlias = request.ProductAlias,
                Duration = DateTime.UtcNow - startTime,
                UpdatedFiles = updatedFiles,
                NewFiles = newFiles,
                RemovedFiles = removedFiles,
                UpdatedFileNames = updatedFileNames,
                Messages = new List<string>
                {
                    $"Hash update completed: {updatedFiles} updated, {newFiles} new (not yet migrated), {removedFiles} missing from disk"
                }
            };

            _logger.LogInformation(
                "Update-Hash completed for product {Product}: {Updated} updated, {New} new, {Removed} missing",
                request.ProductAlias, updatedFiles, newFiles, removedFiles);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Update-Hash for product {Product}", request.ProductAlias);
            return new HashUpdateResult
            {
                Success = false,
                ProductAlias = request.ProductAlias,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                Messages = new List<string> { $"Update failed: {ex.Message}" }
            };
        }
    }

    public async Task<MigrationStatusInfo> GetStatusAsync(string productAlias)
    {
        try
        {
            _logger.LogDebug("Getting migration status for product {Product}", productAlias);

            // --- Phase 1: Initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            // --- Phase 2: Query repository ---
            var existingRecords = await Task.Run(() => _templateExecutor.RepositoryMigrationSelect());

            // --- Phase 3: File discovery ---
            var productOptions = _options.Value.Products!.First(p => p.Alias == productAlias);
            var migrationFiles = DiscoverAndPrepareMigrationFiles(
                productOptions, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment);

            // --- Phase 4: Compute status ---
            var migratedRecords = existingRecords
                .Where(r => r.MigrationStatusId == MigrationStatus.Migrated)
                .ToList();

            // Determine current release (highest release version among migrated records)
            string currentRelease = migratedRecords
                .OrderByDescending(r => r.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.ReleaseVersion)
                .FirstOrDefault() ?? "None";

            // Count pending migrations (files on disk that are not yet successfully migrated)
            int pendingMigrations = 0;
            foreach (var file in migrationFiles)
            {
                bool alreadyMigrated = migratedRecords.Any(r =>
                    r.Filename == file.Filename &&
                    r.ReleaseVersion == file.ReleaseVersion &&
                    r.TargetGroupAlias == file.TargetGroupAlias);

                if (!alreadyMigrated || file.RunAlways)
                {
                    pendingMigrations++;
                }
            }

            // Last migration date
            DateTime? lastMigrationDate = null;
            if (existingRecords.Count > 0)
            {
                // Use the most recent record's MigrationRunId to approximate via the run
                var lastRunId = existingRecords.Max(r => r.MigrationRunId);
                lastMigrationDate = DateTime.UtcNow; // Approximation since Migration table doesn't expose FinishedAt via select
            }

            // Last run result (derive from MigrationStatus: Migrated→Ok, Failed→Error)
            MigrationRunResult? lastRunResult = existingRecords
                .OrderByDescending(r => r.MigrationRunId)
                .Select(r => r.MigrationStatusId == MigrationStatus.Migrated ? MigrationRunResult.Ok : MigrationRunResult.Error)
                .Cast<MigrationRunResult?>()
                .FirstOrDefault();

            // Build target group status
            var targetGroups = new Dictionary<string, TargetGroupStatus>();

            if (productOptions.TargetGroups != null)
            {
                foreach (var tg in productOptions.TargetGroups)
                {
                    var tgRecords = migratedRecords
                        .Where(r => r.TargetGroupAlias == tg.Alias)
                        .ToList();

                    string tgCurrentRelease = tgRecords
                        .OrderByDescending(r => r.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
                        .Select(r => r.ReleaseVersion)
                        .FirstOrDefault() ?? "None";

                    var targets = tg.Targets?.Select(t => t.Alias ?? string.Empty).ToList() ?? new List<string>();

                    targetGroups[tg.Alias!] = new TargetGroupStatus
                    {
                        Alias = tg.Alias!,
                        DatabaseType = tg.DatabaseType ?? string.Empty,
                        CurrentRelease = tgCurrentRelease,
                        ExecutedMigrations = tgRecords.Count,
                        Targets = targets
                    };
                }
            }

            _logger.LogDebug("Status query completed for product {Product}: {Executed} executed, {Pending} pending",
                productAlias, migratedRecords.Count, pendingMigrations);

            return new MigrationStatusInfo
            {
                ProductAlias = productAlias,
                CurrentRelease = currentRelease,
                LastMigrationDate = lastMigrationDate,
                TotalMigrationsExecuted = migratedRecords.Count,
                PendingMigrations = pendingMigrations,
                LastRunResult = lastRunResult,
                TargetGroups = targetGroups
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for product {Product}", productAlias);
            throw;
        }
    }

    public async Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100)
    {
        try
        {
            _logger.LogDebug("Getting migration history for product {Product} with limit {Limit}",
                productAlias, limit);

            // --- Phase 1: Initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            // --- Phase 2: Query MigrationRun records ---
            var rows = await Task.Run(() => _templateExecutor.RepositoryMigrationRunSelect(limit));

            // --- Phase 3: Build history ---
            var runs = new List<MigrationRunInfo>();

            foreach (var row in rows)
            {
                int runId = Convert.ToInt32(row["Id"]);
                var migrationRunResultId = (MigrationRunResult)Convert.ToByte(row["MigrationRunResultId"]);
                var migrationRunModeId = Convert.ToByte(row["MigrationRunModeId"]);
                var startedAt = Convert.ToDateTime(row["StartedAt"]);
                DateTime? finishedAt = row["FinishedAt"] != null && row["FinishedAt"] != DBNull.Value
                    ? Convert.ToDateTime(row["FinishedAt"])
                    : null;
                string? toRelease = row["ToReleaseVersion"]?.ToString();

                // Determine operation from run mode (MigrateUp is default for Migrate run mode)
                var operation = MigrationOperation.MigrateUp;

                // Count migrations for this run from the Migration table
                var existingRecords = await Task.Run(() => _templateExecutor.RepositoryMigrationSelect());
                var runMigrations = existingRecords.Where(r => r.MigrationRunId == runId).ToList();

                int totalMigrations = runMigrations.Count;
                int successfulMigrations = runMigrations.Count(r => r.MigrationStatusId == MigrationStatus.Migrated);
                int failedMigrations = runMigrations.Count(r => r.MigrationStatusId == MigrationStatus.Failed);

                // Detect MigrateDown from migration records
                if (runMigrations.Any(r => r.MigrationOperationId == MigrationOperation.MigrateDown))
                {
                    operation = MigrationOperation.MigrateDown;
                }

                runs.Add(new MigrationRunInfo
                {
                    MigrationRunId = runId,
                    RunId = Guid.Empty, // MigrationRun uses INT Id, not Guid
                    StartedAt = startedAt,
                    CompletedAt = finishedAt,
                    Operation = operation,
                    Result = migrationRunResultId,
                    RunMode = (MigrationRunMode)migrationRunModeId,
                    TotalMigrations = totalMigrations,
                    SuccessfulMigrations = successfulMigrations,
                    FailedMigrations = failedMigrations,
                    ToRelease = toRelease
                });
            }

            _logger.LogDebug("History query completed for product {Product}: {Count} run(s) returned",
                productAlias, runs.Count);

            return new MigrationHistory
            {
                ProductAlias = productAlias,
                Runs = runs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for product {Product}", productAlias);
            throw;
        }
    }

    #endregion Stub Methods

    #region Fix Command

    public async Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing Fix command for product {Product} in environment {Environment} with scope {Scope}",
                request.ProductAlias, request.Environment, request.Scope);

            if (_ctxAccessor.Current.RayMigratorConsoleOptions.Product != request.ProductAlias)
            {
                throw new InvalidOperationException(
                    $"Product mismatch: context has {_ctxAccessor.Current.RayMigratorConsoleOptions.Product} but request has {request.ProductAlias}");
            }

            // --- Phase 1: Repository initialization ---
            await Task.Run(() => _templateExecutor.RepositoryCheckCreate());
            await Task.Run(() => _templateExecutor.RepositoryProductCheckInsert());
            await Task.Run(() => _templateExecutor.RepositoryEnvironmentCheckInsert());

            int productId = _ctxAccessor.Current.MigrationState.ProductId;
            int environmentId = _ctxAccessor.Current.MigrationState.EnvironmentId;

            // --- Phase 2: Query orphaned runs ---
            var orphanedRows = await Task.Run(() =>
                _templateExecutor.RepositoryMigrationRunSelectOrphaned(productId, environmentId));

            // Environment text is run-constant (WHERE constraint filters by EnvironmentId);
            // fill from request rather than a redundant JOIN to the Environment table.
            var orphanedRuns = new List<OrphanedRunInfo>();
            foreach (var row in orphanedRows)
            {
                orphanedRuns.Add(new OrphanedRunInfo
                {
                    MigrationRunId = Convert.ToInt32(row["MigrationRunId"]),
                    EnvironmentId = Convert.ToInt32(row["EnvironmentId"]),
                    Environment = request.Environment,
                    StartedAt = Convert.ToDateTime(row["StartedAt"]),
                    MinutesRunning = Convert.ToDouble(row["MinutesRunning"]),
                    MigrationRunModeId = Convert.ToInt32(row["MigrationRunModeId"]),
                });
            }

            // --- Phase 3: Filter by OlderThanMinutes ---
            var filteredRuns = orphanedRuns
                .Where(r => r.MinutesRunning >= request.OlderThanMinutes)
                .ToList();

            _logger.LogInformation("Found {Total} orphaned run(s), {Filtered} matching --older-than {Minutes} minutes",
                orphanedRuns.Count, filteredRuns.Count, request.OlderThanMinutes);

            // --- Phase 4: Log found orphans ---
            foreach (var run in filteredRuns)
            {
                _logger.LogInformation("  Orphaned MigrationRun Id={RunId}, started at {StartedAt:yyyy-MM-dd HH:mm:ss} UTC, running for {Minutes:F0} minutes, RunMode={RunMode}",
                    run.MigrationRunId, run.StartedAt, run.MinutesRunning, run.MigrationRunModeId);
            }

            // --- Phase 5: Fix if not DryRun ---
            int fixedCount = 0;
            if (!request.DryRun)
            {
                foreach (var run in filteredRuns)
                {
                    // Determine target status for orphaned migrations
                    var targetStatus = request.AssumedMigrationStatus;

                    // Fix orphaned Migration entries first (before closing the run)
                    int migrationsFixed = await Task.Run(() =>
                        _templateExecutor.RepositoryMigrationFixOrphaned(run.MigrationRunId, targetStatus));

                    if (migrationsFixed > 0)
                    {
                        _logger.LogInformation("  Fixed {Count} orphaned Migration entry for MigrationRunId {RunId} (Status={Status})",
                            migrationsFixed, run.MigrationRunId, targetStatus);
                    }

                    // Fix the MigrationRun itself (mark as Error, set FinishedAt)
                    await Task.Run(() => _templateExecutor.RepositoryMigrationRunFixOrphaned(run.MigrationRunId));
                    run.WasFixed = true;
                    fixedCount++;

                    _logger.LogInformation("  Fixed orphaned MigrationRun {RunId} (marked as Error)",
                        run.MigrationRunId);
                }
            }
            else
            {
                _logger.LogInformation("Dry-run mode: no changes applied");
            }

            // --- Phase 6: Build result ---
            return new FixIssuesResult
            {
                Success = true,
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                WasDryRun = request.DryRun,
                OrphanedRunsFound = filteredRuns.Count,
                OrphanedRunsFixed = fixedCount,
                OrphanedRuns = filteredRuns,
                Duration = DateTime.UtcNow - startTime,
                Messages = new List<string>
                {
                    request.DryRun
                        ? $"Dry-run: found {filteredRuns.Count} orphaned run(s) to fix"
                        : $"Fixed {fixedCount} orphaned run(s)"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Fix command for product {Product}: {Message}",
                request.ProductAlias, ex.Message);

            return new FixIssuesResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = ExtractErrorCode(ex),
                ProductAlias = request.ProductAlias,
                Environment = request.Environment,
                Duration = DateTime.UtcNow - startTime,
            };
        }
    }

    #endregion Fix Command

    #region Helpers

    /// <summary>
    /// Extracts a categorized ErrorCode from an exception.
    /// Negative = SQL template ResultCode, positive = C# backend ErrorCode, null = unclassified.
    /// </summary>
    private static int? ExtractErrorCode(Exception ex) => ex switch
    {
        MigrationAlreadyRunningException => TemplateResultCode.MigrationAlreadyRunning,
        UndefinedTemplateResultException utre => utre.ResultCode,
        TemplateResultException tre => tre.ResultCode,
        MigrationFileParsingException { ErrorCode: not null } mfpe => mfpe.ErrorCode,
        MigrationFileParsingException => TemplateResultCode.MigrationFileParsingFailed,
        ConfigurationValidationException => TemplateResultCode.ConfigurationValidationFailed,
        _ => null
    };

    #endregion Helpers

    #region Internal Types

    /// <summary>
    /// Result of executing a TargetGroup's migrations (either Simultaneously or Successively).
    /// </summary>
    internal class TargetGroupExecutionResult
    {
        public bool Success { get; set; } = true;
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public MigrationFileInfo? FailedFile { get; set; }
        public int FailedMigrationId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Internal result type for rollback operations.
    /// </summary>
    private class RollbackResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool AllSuccessful => FailCount == 0;
        public string? ErrorMessage { get; set; }
        public List<string> Messages { get; set; } = new();
        public List<MigrationFileResult> FileResults { get; set; } = new();

        public void AddWarning(string filename, string message)
        {
            Messages.Add($"WARNING [{filename}]: {message}");
        }

        public void AddFailure(string filename, string message)
        {
            FailCount++;
            Messages.Add($"ERROR [{filename}]: {message}");
            FileResults.Add(new MigrationFileResult
            {
                FileName = filename,
                Success = false,
                ErrorMessage = message,
                ExecutedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Holds parsed TOML defaults from migsettings.txt files.
    /// </summary>
    internal class MigSettingsEntry
    {
        public bool? UseTransaction { get; set; }
        public bool? RunAlways { get; set; }
        public bool? RequireRollbackFile { get; set; }
        public bool? StopRollbackOnMissingRollbackFile { get; set; }
        public List<string>? Environments { get; set; }
        public List<string>? Targets { get; set; }
        public MigrationErrorAction? MigrationErrorAction { get; set; }
        public RollbackErrorAction? RollbackErrorAction { get; set; }
        public string? UseCliToolAlias { get; set; }
        public List<string>? TargetGroupMigrationOrder { get; set; }
    }

    #endregion Internal Types
}
