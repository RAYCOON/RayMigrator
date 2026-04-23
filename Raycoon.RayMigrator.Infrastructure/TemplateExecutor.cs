// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.Data.Common;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Core.Recovery;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core;

public class TemplateExecutor
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger<TemplateExecutor> _logger;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private RepositoryOptions? _repositoryBacking;
    private IDal? _repositoryDalBacking;

    /// <summary>
    /// Lazily-initialized repository options. Defers context access to support API endpoints
    /// where MigrationContext is set after DI resolution.
    /// </summary>
    private RepositoryOptions _repository
    {
        get
        {
            if (_repositoryBacking == null) InitializeFromContext();
            return _repositoryBacking!;
        }
    }

    private IDal _repositoryDal
    {
        get
        {
            if (_repositoryBacking == null) InitializeFromContext();
            return _repositoryDalBacking!;
        }
    }

    /// <summary>
    /// Public constructor. Uses IMigrationContextAccessor for per-request context isolation.
    /// Context access is deferred to first use (not constructor time) to support API endpoints
    /// where the MigrationContext is set after DI resolution.
    /// </summary>
    public TemplateExecutor(TemplateCache templateCache, ILogger<TemplateExecutor> logger, IMigrationContextAccessor ctxAccessor)
    {
        _templateCache = templateCache;
        _logger = logger;
        _ctxAccessor = ctxAccessor;
    }

    private void InitializeFromContext()
    {
        _logger.LogDebug("Initializing TemplateExecutor from MigrationContext (deferred initialization)");
        _repositoryBacking = _ctxAccessor.Current.RayMigratorOptions.Repository!;
        if (DalFactory.TryGetDal(_repositoryBacking.DatabaseType!, _repositoryBacking.ConnectionString!, out var dal))
            _repositoryDalBacking = dal!;
    }

    /// <summary>
    /// Create RayMigrator infrastructure for all TargetGroups.Repository.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryCheckCreate()
    {
        var templateType = TemplateType.Repository_CheckCreate;
        var eventId = MigrationEvent.TemplateExecutionRepositoryCheckCreate;
        
        _logger.LogDebug(eventId, "Check and create Repository if it does not exist...{MigrationContext}", _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("RepositoryDatabaseType", _repositoryDal.DatabaseType, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("RayMigratorVersion", _ctxAccessor.Current.RayMigratorVersion, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.MigratorMetaId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for existing product-name and optionally creates a new Product-entry in the repository and returns the ProductId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryProductCheckInsert()
    {
        var templateType = TemplateType.Repository_Product_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryProductCheckInsert;
        string productName = _ctxAccessor.Current.RayMigratorConsoleOptions.Product;
        
        _logger.LogDebug(eventId, "Searching for product with name {ProductName}. Insert a new product-entry if name does not yet exist{MigrationContext}", productName , _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.ProductId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for existing environment name and optionally creates a new Environment entry in the repository and returns the EnvironmentId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryEnvironmentCheckInsert()
    {
        var templateType = TemplateType.Repository_Environment_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert;
        string environmentName = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment;

        _logger.LogDebug(eventId, "Searching for environment with name {EnvironmentName}. Insert a new environment entry if name does not yet exist{MigrationContext}", environmentName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", environmentName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", environmentName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.EnvironmentId = templateResponse.ResultCode;
    }

    /// <summary>
    /// OK: Creates a new MigrationRun in the database and inserts the settings JSON into MigrationRunMeta.
    /// </summary>
    /// <param name="migrationRunSettingsJson">JSON snapshot of all RayMigrator settings at migration start.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunInsert(string migrationRunSettingsJson)
    {
        var templateType = TemplateType.Repository_MigrationRun_Insert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunInsert;

        _logger.LogDebug(eventId, "Create MigrationRun-entry{MigrationContext}", _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte) _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigratorMetaId", _ctxAccessor.Current.MigrationState.MigratorMetaId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunResultId", (byte) _ctxAccessor.Current.MigrationState.MigrationRunResult, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FromReleaseVersion", "FromReleaseVersion", typeof(string)));
        dalParameterList.AddParameter(new DalParameter("ToReleaseVersion", _ctxAccessor.Current.RayMigratorConsoleOptions.TargetReleaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunSettingsJson", migrationRunSettingsJson, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        try
        {
            var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
            _ctxAccessor.Current.MigrationState.MigrationRunId = templateResponse.ResultCode;
        }
        catch (TemplateResultException ex) when (ex.ResultCode == -2)
        {
            throw new MigrationAlreadyRunningException(
                ex.Message, _ctxAccessor.Current.MigrationState.ProductId);
        }
    }

    /// <summary>
    /// Updates an existing MigrationRun with the final result status and completion time.
    /// </summary>
    /// <param name="runResult">The final result of the migration run.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunUpdate(MigrationRunResult runResult)
    {
        var templateType = TemplateType.Repository_MigrationRun_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunUpdate;

        _logger.LogDebug(eventId, "Update MigrationRun-entry with result {MigrationRunResult}{MigrationContext}",
            runResult, _ctxAccessor.Current.Clone);

        // Validate state
        if (_ctxAccessor.Current.MigrationState.MigrationRunId < 1)
        {
            throw new RayMigratorInternalException(
                $"Invalid MigrationRunId [{_ctxAccessor.Current.MigrationState.MigrationRunId}] when updating MigrationRun.");
        }

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunResultId", (byte)runResult, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.MigrationRunResult = runResult;
    }

    /// <summary>
    /// Selects orphaned MigrationRun entries (Running state, FinishedAt IS NULL).
    /// Used by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="productId">The product ID to query.</param>
    /// <param name="environmentId">The environment ID to query.</param>
    /// <returns>List of raw row dictionaries with orphaned run data.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelectOrphaned(int productId, int environmentId)
    {
        var templateType = TemplateType.Repository_MigrationRun_SelectOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunSelectOrphaned;

        _logger.LogDebug(eventId, "Selecting orphaned MigrationRun entries for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            productId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, environmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", productId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", environmentId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        _logger.LogDebug(eventId, "Found {Count} orphaned MigrationRun entries{MigrationContext}", rows.Count, _ctxAccessor.Current.Clone);
        return rows;
    }

    /// <summary>
    /// Marks a single orphaned MigrationRun as Error with FinishedAt set.
    /// Used by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="migrationRunId">The ID of the orphaned MigrationRun to fix.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunFixOrphaned(int migrationRunId)
    {
        var templateType = TemplateType.Repository_MigrationRun_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRun {MigrationRunId}{MigrationContext}",
            migrationRunId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates orphaned MigrationRecord entries (Running/Unclear) for a given MigrationRun.
    /// Used by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="migrationRunId">The MigrationRun ID whose orphaned migrations to fix.</param>
    /// <param name="status">Target status: NotMigrated (50) or Migrated (100).</param>
    /// <returns>Number of updated MigrationRecord entries (0 = none found, which is not an error).</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public int RepositoryMigrationFixOrphaned(int migrationRunId, MigrationStatus status)
    {
        var templateType = TemplateType.Repository_Migration_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRecord entries for MigrationRunId {MigrationRunId} with status {Status}{MigrationContext}",
            migrationRunId, status, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)status, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for interrupted migrations that can be resumed.
    /// </summary>
    /// <returns>An InterruptedMigrationInfo object if an interrupted migration is found, null otherwise.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public InterruptedMigrationInfo? RepositoryMigrationGetInterrupted()
    {
        var templateType = TemplateType.Repository_Migration_GetInterrupted;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationGetInterrupted;

        _logger.LogDebug(eventId, "Checking for interrupted migrations for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        // ResultCode 0 means no interrupted migration found
        if (templateResponse.ResultCode == 0)
        {
            return null;
        }

        // Parse the pipe-separated result message
        // Format: MigrationId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias
        var parts = (templateResponse.ResultMessage ?? string.Empty).Split('|');
        if (parts.Length < 9)
        {
            _logger.LogWarning("Unexpected format in interrupted migration response: {Response}", templateResponse.ResultMessage);
            return null;
        }

        // Environment text value is run-constant (WHERE constraint in SQL filters by EnvironmentId);
        // fill from console options rather than a redundant JOIN to the Environment table.
        return new InterruptedMigrationInfo
        {
            MigrationId = int.Parse(parts[0]),
            MigrationRunId = int.Parse(parts[1]),
            ReleaseVersion = parts[2],
            Filename = parts[3],
            BlocksMigrated = int.Parse(parts[4]),
            BlocksTotal = int.Parse(parts[5]),
            EnvironmentId = int.Parse(parts[6]),
            Environment = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment,
            TargetGroupAlias = parts[7],
            TargetAlias = parts[8]
        };
    }

    /// <summary>
    /// Creates a new MigrationRecord entry or resets an existing archived record
    /// with block-level tracking for recovery support.
    /// </summary>
    /// <param name="existingMigrationId">0 to INSERT a new record, or the existing Migration ID to UPDATE/reset.</param>
    /// <param name="filename">The migration filename.</param>
    /// <param name="releaseVersion">The release version from file path.</param>
    /// <param name="targetGroupAlias">The target group alias.</param>
    /// <param name="targetAlias">The target alias.</param>
    /// <param name="fileOrderId">The file order ID.</param>
    /// <param name="fileUpHash">Hash of the entire file.</param>
    /// <param name="fileUpConfigHash">Hash of the TOML configuration section.</param>
    /// <param name="fileUpBlocksHash">Hash of the SQL content blocks.</param>
    /// <param name="fileUpBlocksTotal">Total number of blocks in the file.</param>
    /// <param name="fileUpConfigJson">JSON representation of file configuration.</param>
    /// <param name="migrateDownFileExists">Whether a down-migration file exists.</param>
    /// <returns>The MigrationId (new or existing).</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public int RepositoryMigrationInsert(
        int existingMigrationId,
        string filename,
        string releaseVersion,
        string targetGroupAlias,
        string targetAlias,
        int fileOrderId,
        string fileUpHash,
        string? fileUpConfigHash,
        string fileUpBlocksHash,
        int fileUpBlocksTotal,
        string? fileUpConfigJson,
        bool migrateDownFileExists)
    {
        var templateType = TemplateType.Repository_Migration_Insert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationInsert;

        _logger.LogDebug(eventId, "Create MigrationRecord-entry for file {Filename}{MigrationContext}", filename, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ExistingMigrationId", existingMigrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte)_ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationOperationId", (byte)_ctxAccessor.Current.MigrationState.MigrationOperation, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)MigrationStatus.Pending, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("ReleaseVersion", releaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetGroupAlias", targetGroupAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetAlias", targetAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("Filename", filename, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileOrderId", fileOrderId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpHash", fileUpHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigHash", fileUpConfigHash ?? string.Empty, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksHash", fileUpBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksTotal", fileUpBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigJson", fileUpConfigJson ?? "{}", typeof(string)));
        dalParameterList.AddParameter(new DalParameter("MigrateDownFileExists", migrateDownFileExists, typeof(bool)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.MigrationId = templateResponse.ResultCode;
        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Updates a MigrationRecord entry with block progress or final status.
    /// </summary>
    /// <param name="migrationId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileUpBlocksMigrated">Number of blocks successfully migrated.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdate(int migrationId, MigrationStatus migrationStatus, int fileUpBlocksMigrated)
    {
        var templateType = TemplateType.Repository_Migration_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdate;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationId} - Block {Block} - Status {Status}{MigrationContext}",
            migrationId, fileUpBlocksMigrated, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationId", migrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksMigrated", fileUpBlocksMigrated, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with rollback (FileDown*) fields and status.
    /// Used during rollback execution to track rollback file metadata and block progress.
    /// </summary>
    /// <param name="migrationId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileDownHash">SHA256 hash of the rollback file.</param>
    /// <param name="fileDownConfigHash">SHA256 hash of the TOML config in the rollback file.</param>
    /// <param name="fileDownBlocksHash">SHA256 hash of the SQL blocks in the rollback file.</param>
    /// <param name="fileDownBlocksMigrated">Number of rollback blocks successfully executed.</param>
    /// <param name="fileDownBlocksTotal">Total number of rollback blocks.</param>
    /// <param name="fileDownConfigJson">JSON of parsed TOML configuration from the rollback file.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateRollback(
        int migrationId,
        MigrationStatus migrationStatus,
        string fileDownHash,
        string? fileDownConfigHash,
        string fileDownBlocksHash,
        int fileDownBlocksMigrated,
        int fileDownBlocksTotal,
        string? fileDownConfigJson)
    {
        var templateType = TemplateType.Repository_Migration_UpdateRollback;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateRollback;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationId} rollback - Block {Block}/{Total} - Status {Status}{MigrationContext}",
            migrationId, fileDownBlocksMigrated, fileDownBlocksTotal, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationId", migrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileDownHash", fileDownHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigHash", fileDownConfigHash ?? string.Empty, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksHash", fileDownBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksMigrated", fileDownBlocksMigrated, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksTotal", fileDownBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigJson", fileDownConfigJson ?? "{}", typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with block progress or final status using a caller-provided
    /// shared connection and transaction for atomic execution with target SQL blocks.
    /// </summary>
    /// <param name="migrationId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileUpBlocksMigrated">Number of blocks successfully migrated.</param>
    /// <param name="connection">The shared database connection.</param>
    /// <param name="transaction">The shared database transaction.</param>
    /// <param name="repoCommandTimeoutInSeconds">Command timeout for the repository update.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdate(
        int migrationId,
        MigrationStatus migrationStatus,
        int fileUpBlocksMigrated,
        DbConnection connection,
        DbTransaction transaction,
        int repoCommandTimeoutInSeconds)
    {
        var templateType = TemplateType.Repository_Migration_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdate;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationId} - Block {Block} - Status {Status} (atomic shared connection){MigrationContext}",
            migrationId, fileUpBlocksMigrated, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationId", migrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksMigrated", fileUpBlocksMigrated, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with rollback (FileDown*) fields and status using a caller-provided
    /// shared connection and transaction for atomic execution with target rollback blocks.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateRollback(
        int migrationId,
        MigrationStatus migrationStatus,
        string fileDownHash,
        string? fileDownConfigHash,
        string fileDownBlocksHash,
        int fileDownBlocksMigrated,
        int fileDownBlocksTotal,
        string? fileDownConfigJson,
        DbConnection connection,
        DbTransaction transaction,
        int repoCommandTimeoutInSeconds)
    {
        var templateType = TemplateType.Repository_Migration_UpdateRollback;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateRollback;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationId} rollback - Block {Block}/{Total} - Status {Status} (atomic shared connection){MigrationContext}",
            migrationId, fileDownBlocksMigrated, fileDownBlocksTotal, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationId", migrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileDownHash", fileDownHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigHash", fileDownConfigHash ?? string.Empty, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksHash", fileDownBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksMigrated", fileDownBlocksMigrated, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksTotal", fileDownBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigJson", fileDownConfigJson ?? "{}", typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Selects all MigrationRecord entries for the current product, environment, and run mode.
    /// Returns a list of MigrationRecord objects for comparison with files on disk.
    /// </summary>
    /// <param name="overrideRunMode">
    /// Optional run mode override. When set, this value is used for the MigrationRunModeId parameter
    /// instead of reading from the current MigrationContext. This allows Simulate mode to query
    /// records that were written by Migrate mode.
    /// </param>
    /// <returns>List of MigrationRecord objects from the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<MigrationRecord> RepositoryMigrationSelect(MigrationRunMode? overrideRunMode = null)
    {
        var effectiveRunMode = overrideRunMode ?? _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode;
        var templateType = TemplateType.Repository_Migration_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationSelect;

        _logger.LogDebug(eventId, "Selecting migrations for product {ProductId} with environment {Environment} ({EnvironmentId}) with run mode {RunMode}{MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, effectiveRunMode, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte)effectiveRunMode, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        var records = new List<MigrationRecord>();
        foreach (var row in rows)
        {
            records.Add(new MigrationRecord
            {
                Id = Convert.ToInt32(row["Id"]),
                ProductId = Convert.ToInt32(row["ProductId"]),
                MigrationRunId = Convert.ToInt32(row["MigrationRunId"]),
                MigrationOperationId = (MigrationOperation)Convert.ToByte(row["MigrationOperationId"]),
                MigrationStatusId = (MigrationStatus)Convert.ToByte(row["MigrationStatusId"]),
                ReleaseVersion = row["ReleaseVersion"]?.ToString() ?? string.Empty,
                TargetGroupAlias = row["TargetGroupAlias"]?.ToString() ?? string.Empty,
                TargetAlias = row["TargetAlias"]?.ToString() ?? string.Empty,
                Filename = row["Filename"]?.ToString() ?? string.Empty,
                FileOrderId = Convert.ToInt32(row["FileOrderId"]),
                FileUpHash = row["FileUpHash"]?.ToString() ?? string.Empty,
                FileUpConfigHash = row["FileUpConfigHash"]?.ToString(),
                FileUpBlocksHash = row["FileUpBlocksHash"]?.ToString() ?? string.Empty,
                FileUpBlocksMigrated = Convert.ToInt32(row["FileUpBlocksMigrated"]),
                FileUpBlocksTotal = Convert.ToInt32(row["FileUpBlocksTotal"]),
                MigrateDownFileExists = Convert.ToBoolean(row["MigrateDownFileExists"]),
                FileDownHash = row["FileDownHash"]?.ToString(),
                FileDownConfigHash = row["FileDownConfigHash"]?.ToString(),
                FileDownBlocksHash = row["FileDownBlocksHash"]?.ToString(),
                FileDownBlocksMigrated = row["FileDownBlocksMigrated"] != null ? Convert.ToInt32(row["FileDownBlocksMigrated"]) : null,
                FileDownBlocksTotal = row["FileDownBlocksTotal"] != null ? Convert.ToInt32(row["FileDownBlocksTotal"]) : null,
            });
        }

        _logger.LogDebug(eventId, "Found {Count} migration records in repository{MigrationContext}", records.Count, _ctxAccessor.Current.Clone);
        return records;
    }

    /// <summary>
    /// Updates the hash fields of an existing MigrationRecord entry.
    /// Used by the Update-Hash command to synchronize repository hashes with changed files on disk.
    /// </summary>
    /// <param name="migrationId">The migration record ID to update.</param>
    /// <param name="fileUpHash">New SHA256 hash of the entire file.</param>
    /// <param name="fileUpConfigHash">New SHA256 hash of the TOML config section.</param>
    /// <param name="fileUpBlocksHash">New SHA256 hash of the SQL blocks.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateHash(
        int migrationId,
        string fileUpHash,
        string? fileUpConfigHash,
        string fileUpBlocksHash)
    {
        var templateType = TemplateType.Repository_Migration_UpdateHash;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateHash;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationId} hashes{MigrationContext}",
            migrationId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationId", migrationId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpHash", fileUpHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigHash", fileUpConfigHash ?? string.Empty, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksHash", fileUpBlocksHash, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Selects MigrationRun records for the current product.
    /// Returns a list of MigrationRunInfo-compatible data for history display.
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <returns>List of dictionaries with MigrationRun data from the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelect(int limit)
    {
        var templateType = TemplateType.Repository_MigrationRun_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunSelect;

        _logger.LogDebug(eventId, "Selecting MigrationRun records for product {ProductId} with limit {Limit}{MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, limit, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("Limit", limit, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        _logger.LogDebug(eventId, "Found {Count} MigrationRun records{MigrationContext}", rows.Count, _ctxAccessor.Current.Clone);
        return rows;
    }

    /// <summary>
    /// Executes a template as ExecuteScalar and validates the result.
    /// Throws TemplateExecutionException on negative result codes.
    /// </summary>
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null)
    {
        object? returnValue;
        
        try
        {
            returnValue = dal.ExecuteScalarAsync(template.Content, dalSettings, dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{dal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }        
        
        TemplateResponse templateResponse = GetValidatedTemplateResponseFromExecuteScalar(returnValue, template);

        if (logger != null)
        {
            logger.LogDebug((EventId) eventId!, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}", template, templateResponse.ResultCode, templateResponse.ResultMessage, _ctxAccessor.Current.Clone);
        }
        
        return templateResponse;
    }

    /// <summary>
    /// Executes a template as ExecuteScalar on a caller-provided shared connection and transaction,
    /// then validates the result. Used for atomic shared-connection execution where the caller
    /// controls the connection/transaction lifecycle.
    /// Throws TemplateExecutionException on negative result codes.
    /// </summary>
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(
        Template template, IDal dal, DbConnection connection, DbTransaction transaction,
        int commandTimeoutInSeconds, DalParameterList? dalParameterList,
        ILogger? logger = null, EventId? eventId = null)
    {
        object? returnValue;

        try
        {
            returnValue = dal.ExecuteScalarAsync(template.Content, connection, transaction, commandTimeoutInSeconds, dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{dal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        TemplateResponse templateResponse = GetValidatedTemplateResponseFromExecuteScalar(returnValue, template);

        if (logger != null)
        {
            logger.LogDebug((EventId) eventId!, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}", template, templateResponse.ResultCode, templateResponse.ResultMessage, _ctxAccessor.Current.Clone);
        }

        return templateResponse;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="inputObject"></param>
    /// <param name="template"></param>
    /// <returns></returns>
    internal static TemplateResponse GetValidatedTemplateResponseFromExecuteScalar(object? inputObject, Template template)
    {
        if (inputObject == null || inputObject.ToString() == null)
        {
            throw new TemplateResultException($"Error executing template {template}: Execution of template returned [null] as a result");
        }

        string resultString = inputObject.ToString()!.Trim();
        if (string.IsNullOrEmpty(resultString))
        {
            throw new TemplateResultException($"Error executing template {template}: Execution returned empty string as result");
        }

        TemplateResponse templateResponse = new();
        templateResponse.ResultMessage = string.Empty;
        string[] resultSplit;

        int commaIndex = resultString.IndexOf(',');
        if (commaIndex == -1)
        {
            resultSplit = new string[] { resultString, string.Empty };
        }
        else
        {
            string firstPart = resultString.Substring(0, commaIndex);
            string secondPart = resultString.Substring(commaIndex + 1);
            resultSplit = new string[] { firstPart, secondPart };
        }

        string intValueString = resultSplit[0].Trim();
        if (!int.TryParse(intValueString, out int resultCode))
        {
            throw new TemplateResultException($"Error executing template {template}: Execution returned incorrect result. " +
                                                $"The first (or only) part of [{resultString}], which is [{intValueString}], needs to be converted into an integer-value. This was NOT possible!");
        }

        // Set TemplateResponse
        templateResponse.ResultCode = resultCode;
        if (resultSplit.Length == 2)
        {
            templateResponse.ResultMessage = resultSplit[1].Trim();
        }
        
        if (resultCode < 0)
        {
            // Template returns a negative result => error
            string errorMessage = $"Error executing template {template}. Template-execution returned a negative ResultCode [{templateResponse.ResultCode}] with ErrorMessage: {templateResponse.ResultMessage}.";

            if (TemplateResultCode.IsKnown(resultCode))
                throw new TemplateResultException(errorMessage, templateResponse.ResultCode);
            else
                throw new UndefinedTemplateResultException(errorMessage, templateResponse.ResultCode);
        }
        
        // Everything OK
        return templateResponse;
    }

}