using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Represents a fully configured scenario ready for migration execution and assertions.
/// Created by ScenarioBuilder.BuildAsync. Manages the DI host lifecycle and provides
/// assertion methods against the repository database.
/// </summary>
public class ScenarioContext : IAsyncDisposable
{
    private EngineTestHost _host;
    private readonly RepositoryQueryHelper _queryHelper;
    private readonly EngineConfig _engineConfig;
    private readonly string _workDir;
    private readonly string _configPath;
    private readonly string _productAlias;
    private OperationResult? _lastResult;
    private string _environment = "Docker";

    /// <summary>
    /// The temporary working directory containing the copied migration files.
    /// </summary>
    public string WorkDirectory => _workDir;

    /// <summary>
    /// Returns the host's migration service and binds the host's MigrationContext to the ambient
    /// <see cref="MigrationLoggingContext"/> of the current async flow. EngineTestHost sets the AsyncLocal inside
    /// <c>Build</c>, which runs in the builder's async flow and is therefore invisible to the test method; without this
    /// binding the enricher sees no context and the DatabaseLogging sink lets every event through (#6).
    /// </summary>
    private IMigrationService Service()
    {
        MigrationLoggingContext.Current = _host.MigrationContext;
        return _host.MigrationService;
    }

    internal ScenarioContext(
        EngineTestHost host,
        RepositoryQueryHelper queryHelper,
        EngineConfig engineConfig,
        string workDir,
        string configPath,
        string productAlias)
    {
        _host = host;
        _queryHelper = queryHelper;
        _engineConfig = engineConfig;
        _workDir = workDir;
        _configPath = configPath;
        _productAlias = productAlias;
    }

    /// <summary>
    /// Executes Migrate-Up, optionally limited to a specific release.
    /// <paramref name="runMode"/> defaults to the run mode the host was built with; an explicit value that differs
    /// from it rebuilds the host first, so the request and the context never disagree about the run mode (#6).
    /// </summary>
    public async Task<MigrationOperationResult> MigrateUpAsync(
        string? toRelease = null, bool allowOutOfOrder = false,
        string[]? targetGroupAliases = null, MigrationRunMode? runMode = null,
        string[]? targetGroupMigrationOrder = null)
    {
        var effectiveRunMode = await AlignHostRunModeAsync(MigrationCommand.MigrateUp, runMode, toRelease);

        using var maskScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveData(_host.RayMigratorOptions);

        var request = new MigrateUpRequest
        {
            ProductAlias = _productAlias,
            Environment = _environment,
            TargetReleaseVersion = toRelease,
            RunMode = effectiveRunMode,
            ShowInfo = false,
            RevealSensitiveData = false,
            AllowOutOfOrder = allowOutOfOrder,
            TargetGroupAliases = targetGroupAliases,
            TargetGroupMigrationOrder = targetGroupMigrationOrder
        };
        var result = await Service().MigrateUpAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Executes Migrate-Down to the specified release version.
    /// <paramref name="runMode"/> defaults to the run mode the host was built with; an explicit value that differs
    /// from it rebuilds the host first, so the request and the context never disagree about the run mode (#6).
    /// </summary>
    public async Task<MigrationOperationResult> MigrateDownAsync(
        string toRelease, string[]? targetGroupAliases = null, MigrationRunMode? runMode = null,
        bool revealSensitiveData = false)
    {
        var effectiveRunMode = await AlignHostRunModeAsync(MigrationCommand.MigrateDown, runMode, toRelease);

        using var maskScope = SensitiveDataMasker.BeginScope(revealSensitiveData);
        SensitiveDataMasker.RegisterSensitiveData(_host.RayMigratorOptions);

        var request = new MigrateDownRequest
        {
            ProductAlias = _productAlias,
            Environment = _environment,
            TargetReleaseVersion = toRelease,
            RunMode = effectiveRunMode,
            ShowInfo = false,
            RevealSensitiveData = revealSensitiveData,
            TargetGroupAliases = targetGroupAliases
        };
        var result = await Service().MigrateDownAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Executes Baseline, optionally limited to a specific release and/or target groups.
    /// </summary>
    public async Task<BaselineResult> BaselineAsync(string? toRelease = null, string[]? targetGroupAliases = null,
        string[]? targetGroupMigrationOrder = null, bool revealSensitiveData = false)
    {
        using var maskScope = SensitiveDataMasker.BeginScope(revealSensitiveData);
        SensitiveDataMasker.RegisterSensitiveData(_host.RayMigratorOptions);

        var request = new BaselineRequest
        {
            ProductAlias = _productAlias,
            Environment = _environment,
            TargetReleaseVersion = toRelease,
            ShowInfo = false,
            RevealSensitiveData = revealSensitiveData,
            TargetGroupAliases = targetGroupAliases,
            TargetGroupMigrationOrder = targetGroupMigrationOrder
        };
        var result = await Service().BaselineAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Executes hash validation against the repository.
    /// </summary>
    public async Task<ValidationResult> ValidateHashAsync(
        HashValidationScope? scope = null, string[]? targetGroupAliases = null)
    {
        var request = new ValidateHashRequest
        {
            ProductAlias = _productAlias,
            HashValidationScope = scope ?? HashValidationScope.File,
            ShowInfo = false,
            RevealSensitiveData = false,
            TargetGroupAliases = targetGroupAliases
        };
        var result = await Service().ValidateHashAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Updates stored hashes for migration files in the repository.
    /// </summary>
    public async Task<HashUpdateResult> UpdateHashAsync(string[]? targetGroupAliases = null)
    {
        var request = new UpdateHashRequest
        {
            ProductAlias = _productAlias,
            ShowInfo = false,
            RevealSensitiveData = false,
            TargetGroupAliases = targetGroupAliases
        };
        var result = await Service().UpdateHashAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Executes GetStatusAsync (Info command) for the configured product.
    /// Note: Does not set _lastResult because MigrationStatusInfo is not an OperationResult.
    /// </summary>
    public async Task<MigrationStatusInfo> InfoAsync()
    {
        var result = await Service().GetStatusAsync(_productAlias);
        return result;
    }

    /// <summary>
    /// Executes GetHistoryAsync for the configured product.
    /// </summary>
    public async Task<MigrationHistory> GetHistoryAsync(int limit = 100)
    {
        var result = await Service().GetHistoryAsync(_productAlias, limit);
        return result;
    }

    /// <summary>
    /// Executes FixIssuesAsync for the configured product.
    /// Sets _lastResult because FixIssuesResult extends OperationResult.
    /// </summary>
    public async Task<FixIssuesResult> FixIssuesAsync(
        FixIssues scope = FixIssues.OrphanedRuns,
        int olderThanMinutes = 0,
        bool dryRun = false,
        MigrationStatus assumedMigrationStatus = MigrationStatus.NotMigrated)
    {
        var request = new FixIssuesRequest
        {
            ProductAlias = _productAlias,
            Environment = _environment,
            Scope = scope,
            OlderThanMinutes = olderThanMinutes,
            DryRun = dryRun,
            AssumedMigrationStatus = assumedMigrationStatus,
            ShowInfo = false,
            RevealSensitiveData = false
        };
        var result = await Service().FixIssuesAsync(request);
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Inserts a fake MigrationRun record with Running status and a StartedAt in the past,
    /// simulating an orphaned migration run.
    /// </summary>
    public void InsertOrphanedMigrationRun(int minutesOld = 120)
    {
        int productId = _queryHelper.GetProductId(_productAlias);
        _queryHelper.InsertOrphanedMigrationRun(productId, minutesOld, "Docker");
    }

    /// <summary>
    /// Returns the ProductId of the scenario's product from the repository, or -1 when no product row exists.
    /// </summary>
    public int GetProductId() => _queryHelper.GetProductId(_productAlias);

    /// <summary>
    /// The console options of the current host, i.e. the command, run mode and flags the MigrationContext was built with.
    /// </summary>
    public Core.Configuration.Options.RayMigratorConsoleOptions HostConsoleOptions => _host.MigrationContext.RayMigratorConsoleOptions;

    /// <summary>
    /// Rebuilds the DI container for a different command/mode without cleaning databases.
    /// Used for multi-step test scenarios (e.g., Migrate-Up then Migrate-Down).
    /// </summary>
    public Task RebuildForAsync(MigrationCommand command, MigrationRunMode mode, string? toRelease = null, string? environment = null, bool? fixDryRun = null)
    {
        _environment = environment ?? "Docker";
        _host.Dispose();
        _host = new EngineTestHost();
        _host.Build(_configPath, _productAlias, command, mode, toRelease, _environment, fixDryRun);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the run mode a migrate request runs in and keeps the host's MigrationContext in sync with it.
    /// Without an explicit <paramref name="runMode"/> the host's run mode is used. An explicit run mode that differs
    /// from the host's rebuilds the host for <paramref name="command"/>, because the execution path reads the run
    /// mode from the request while every stamp and log gate reads it from the context (#6).
    /// </summary>
    private async Task<MigrationRunMode> AlignHostRunModeAsync(MigrationCommand command, MigrationRunMode? runMode, string? toRelease)
    {
        var hostOptions = HostConsoleOptions;
        var effective = runMode ?? hostOptions.RunMode;

        if (effective != hostOptions.RunMode || hostOptions.Command != command)
        {
            await RebuildForAsync(command, effective, toRelease ?? hostOptions.TargetReleaseVersion, _environment);
        }

        return effective;
    }

    /// <summary>
    /// Asserts that the last migration operation succeeded or failed.
    /// </summary>
    public void AssertSuccess(bool expected)
    {
        _lastResult.Should().NotBeNull("No migration has been executed yet");
        _lastResult!.Success.Should().Be(expected,
            $"Expected Success={expected} but was {_lastResult.Success}. Error: {_lastResult.ErrorMessage}");
    }

    /// <summary>
    /// Asserts the MigrationRunResult of the latest MigrationRun record.
    /// </summary>
    public void AssertRunResult(MigrationRunResult expected)
    {
        int actual = _queryHelper.GetLatestMigrationRunResultId();
        actual.Should().Be((int)expected,
            $"MigrationRunResult should be {expected}({(int)expected})");
    }

    /// <summary>
    /// Asserts the total count of MigrationRun records in the repository.
    /// </summary>
    public void AssertRunCount(int expected)
    {
        _queryHelper.CountMigrationRuns().Should().Be(expected);
    }

    /// <summary>
    /// Asserts the MigrationStatus of a specific migration file.
    /// Use MigrationStatus.Undefined (0) to assert that no record exists (returns -1 from query).
    /// </summary>
    public void AssertFileStatus(string filename, MigrationStatus expected)
    {
        int actual = _queryHelper.GetMigrationStatusByFilename(filename);
        int expectedValue = (int)expected;

        actual.Should().Be(expectedValue,
            $"MigrationStatus for '{filename}' should be {expected}({expectedValue}) but was {actual}");
    }

    /// <summary>
    /// Asserts the MigrationStatus of multiple migration files at once.
    /// </summary>
    public void AssertFileStatuses(params (string Filename, MigrationStatus Status)[] expected)
    {
        foreach (var (filename, status) in expected)
        {
            AssertFileStatus(filename, status);
        }
    }

    /// <summary>
    /// Asserts the MigrationStatus of a file for a specific target alias.
    /// Used in multi-target scenarios.
    /// </summary>
    public void AssertFileStatusForTarget(string filename, string target, MigrationStatus expected)
    {
        int actual = _queryHelper.GetMigrationStatusByFilenameAndTarget(filename, target);
        int expectedValue = (int)expected;

        actual.Should().Be(expectedValue,
            $"MigrationStatus for '{filename}' on target '{target}' should be {expected}({expectedValue}) but was {actual}");
    }

    /// <summary>
    /// Asserts detailed fields of a Migration record matching the given filename.
    /// Only non-null fields in the expectation are checked.
    /// </summary>
    public void AssertMigrationRecord(string filename, MigrationRecordExpectation expected)
    {
        var record = _queryHelper.GetMigrationRecordByFilename(filename);
        record.Should().NotBeNull($"No Migration record found for '{filename}'");

        if (expected.MigrationStatusId.HasValue)
            record!.MigrationStatusId.Should().Be(expected.MigrationStatusId.Value,
                $"MigrationStatusId mismatch for '{filename}'");

        if (expected.MigrationOperationId.HasValue)
            record!.MigrationOperationId.Should().Be(expected.MigrationOperationId.Value,
                $"MigrationOperationId mismatch for '{filename}'");

        if (expected.EnvironmentId.HasValue)
            record!.EnvironmentId.Should().Be(expected.EnvironmentId.Value,
                $"EnvironmentId mismatch for '{filename}'");

        if (expected.ReleaseVersion != null)
            record!.ReleaseVersion.Should().Be(expected.ReleaseVersion,
                $"ReleaseVersion mismatch for '{filename}'");

        if (expected.TargetGroupAlias != null)
            record!.TargetGroupAlias.Should().Be(expected.TargetGroupAlias,
                $"TargetGroupAlias mismatch for '{filename}'");

        if (expected.TargetAlias != null)
            record!.TargetAlias.Should().Be(expected.TargetAlias,
                $"TargetAlias mismatch for '{filename}'");

        if (expected.FileOrderId.HasValue)
            record!.FileOrderId.Should().Be(expected.FileOrderId.Value,
                $"FileOrderId mismatch for '{filename}'");

        if (expected.FileUpBlocksMigrated.HasValue)
            record!.FileUpBlocksMigrated.Should().Be(expected.FileUpBlocksMigrated.Value,
                $"FileUpBlocksMigrated mismatch for '{filename}'");

        if (expected.FileUpBlocksTotal.HasValue)
            record!.FileUpBlocksTotal.Should().Be(expected.FileUpBlocksTotal.Value,
                $"FileUpBlocksTotal mismatch for '{filename}'");

        if (expected.MigrateDownFileExists.HasValue)
            record!.MigrateDownFileExists.Should().Be(expected.MigrateDownFileExists.Value,
                $"MigrateDownFileExists mismatch for '{filename}'");

        if (expected.FileDownBlocksMigrated.HasValue)
            record!.FileDownBlocksMigrated.Should().Be(expected.FileDownBlocksMigrated.Value,
                $"FileDownBlocksMigrated mismatch for '{filename}'");

        if (expected.FileDownBlocksTotal.HasValue)
            record!.FileDownBlocksTotal.Should().Be(expected.FileDownBlocksTotal.Value,
                $"FileDownBlocksTotal mismatch for '{filename}'");
    }

    /// <summary>
    /// Asserts detailed fields of a MigrationRun record by index (1-based, oldest first).
    /// Only non-null fields in the expectation are checked.
    /// </summary>
    public void AssertMigrationRun(int runIndex, MigrationRunExpectation expected)
    {
        var run = _queryHelper.GetMigrationRunByIndex(runIndex);
        run.Should().NotBeNull($"No MigrationRun found at index {runIndex}");

        if (expected.MigrationRunResultId.HasValue)
            run!.MigrationRunResultId.Should().Be(expected.MigrationRunResultId.Value,
                $"MigrationRunResultId mismatch at run index {runIndex}");

        if (expected.EnvironmentId.HasValue)
            run!.EnvironmentId.Should().Be(expected.EnvironmentId.Value,
                $"EnvironmentId mismatch at run index {runIndex}");

        if (expected.FromReleaseVersion != null)
            run!.FromReleaseVersion.Should().Be(expected.FromReleaseVersion,
                $"FromReleaseVersion mismatch at run index {runIndex}");

        if (expected.ToReleaseVersion != null)
            run!.ToReleaseVersion.Should().Be(expected.ToReleaseVersion,
                $"ToReleaseVersion mismatch at run index {runIndex}");
    }

    /// <summary>
    /// Asserts whether a user-created table exists in the target database.
    /// </summary>
    public void AssertTableExists(string tableName, bool expected)
    {
        bool exists = _queryHelper.TableExists(_engineConfig.ConnectionString, tableName, useRepositorySchema: false);
        exists.Should().Be(expected,
            $"Table '{tableName}' should {(expected ? "exist" : "not exist")}");
    }

    /// <summary>
    /// Asserts the row count of a user-created table in the target database.
    /// </summary>
    public void AssertRowCount(string tableName, int expected)
    {
        int count = _queryHelper.CountRows(_engineConfig.ConnectionString, tableName, useRepositorySchema: false);
        count.Should().Be(expected,
            $"Row count for '{tableName}'");
    }

    /// <summary>
    /// Asserts whether a user-created table exists on a specific connection string.
    /// Used for multi-database scenarios (e.g., Frontend on a different database).
    /// </summary>
    public void AssertTableExistsOnConnection(string connectionString, string tableName, bool expected)
    {
        bool exists = _queryHelper.TableExists(connectionString, tableName, useRepositorySchema: false);
        exists.Should().Be(expected,
            $"Table '{tableName}' should {(expected ? "exist" : "not exist")} on the specified connection");
    }

    /// <summary>
    /// Executes a non-query SQL statement on an arbitrary connection of the scenario's database type,
    /// e.g. to prepare a conflicting object on a second target so that a migration fails on that target only.
    /// </summary>
    public void ExecuteOnConnection(string connectionString, string sql)
        => _queryHelper.ExecuteOnConnection(connectionString, sql);

    /// <summary>
    /// Asserts that Migration records in the repository were written in the specified
    /// TargetGroup order. The order is inferred from the minimum Id per TargetGroup alias,
    /// which reflects actual execution order.
    /// </summary>
    public void AssertTargetGroupMigrationOrder(params string[] expectedOrder)
    {
        var actual = _queryHelper.GetTargetGroupAliasesInExecutionOrder();
        actual.Should().Equal(expectedOrder,
            $"TargetGroups should have been executed in order [{string.Join(", ", expectedOrder)}] " +
            $"but were [{string.Join(", ", actual)}]");
    }

    // ──── Query methods exposed for test inspection ────

    /// <summary>
    /// Gets the MigrationRunSettingsJson from the latest MigrationRunMeta entry.
    /// </summary>
    public string? GetMigrationRunSettingsJson() => _queryHelper.GetLatestMigrationRunSettingsJson();

    /// <summary>
    /// Gets the FileUpConfigJson for a Migration record matching the given filename.
    /// </summary>
    public string? GetMigrationConfigJson(string filename) => _queryHelper.GetMigrationConfigJson(filename);

    /// <summary>
    /// Counts all MigrationHistory records in the repository.
    /// </summary>
    public int CountMigrationHistory() => _queryHelper.CountMigrationHistory();

    /// <summary>
    /// Counts all MigrationLog entries in the repository.
    /// </summary>
    public int CountLogEntries() => _queryHelper.CountLogEntries();

    /// <summary>
    /// Flushes the host's DatabaseLogWriter queue; call before asserting on <see cref="CountLogEntries"/>.
    /// </summary>
    public void FlushDatabaseLog() => _host.FlushDatabaseLog();

    /// <summary>
    /// Counts MigrationLog entries at a specific log level.
    /// </summary>
    public int CountLogEntriesAtLevel(int logLevelId) => _queryHelper.CountLogEntriesAtLevel(logLevelId);

    /// <summary>
    /// Counts Migration records belonging to a specific target group.
    /// </summary>
    public int CountMigrationsForTargetGroup(string tgAlias) => _queryHelper.CountMigrationsForTargetGroup(tgAlias);

    /// <summary>
    /// Asserts whether a repository table exists (uses repository schema).
    /// </summary>
    public void AssertRepositoryTableExists(string tableName, bool expected)
    {
        bool exists = _queryHelper.TableExists(tableName);
        exists.Should().Be(expected,
            $"Repository table '{tableName}' should {(expected ? "exist" : "not exist")}");
    }

    /// <summary>
    /// Asserts whether the product record exists in the repository.
    /// </summary>
    public void AssertProductExists(bool expected)
    {
        bool exists = _queryHelper.ProductExists(_productAlias);
        exists.Should().Be(expected,
            $"Product '{_productAlias}' should {(expected ? "exist" : "not exist")}");
    }

    /// <summary>
    /// Asserts whether an environment record exists in the repository (case-insensitive via NameLower).
    /// </summary>
    public void AssertEnvironmentExists(string environmentName, bool expected)
    {
        bool exists = _queryHelper.EnvironmentExists(environmentName);
        exists.Should().Be(expected,
            $"Environment '{environmentName}' should {(expected ? "exist" : "not exist")}");
    }

    /// <summary>
    /// Counts MigrationRecord entries in the repository.
    /// </summary>
    public int CountMigrations() => _queryHelper.CountRows("MigrationRecord");

    /// <summary>
    /// Gets the count of MigrationRun records in the repository.
    /// </summary>
    public int CountMigrationRuns() => _queryHelper.CountMigrationRuns();

    /// <summary>
    /// Gets the latest MigrationRunResultId from the repository.
    /// </summary>
    public int GetLatestRunResultId() => _queryHelper.GetLatestMigrationRunResultId();

    /// <summary>
    /// Counts rows in a repository table by name.
    /// </summary>
    public int CountRepoRows(string tableName) => _queryHelper.CountRows(tableName);

    /// <summary>
    /// Counts Migration records matching the given filename.
    /// </summary>
    public int CountMigrationsByFilename(string filename) => _queryHelper.CountMigrationsByFilename(filename);

    /// <summary>
    /// Counts Migration records with a specific MigrationStatus.
    /// </summary>
    public int CountMigrationsWithStatus(int statusId) => _queryHelper.CountMigrationsWithStatus(statusId);

    /// <summary>
    /// Inserts a fake MigrationRun record with Running status to simulate a running migration.
    /// </summary>
    public void InsertRunningMigrationRun()
    {
        int productId = _queryHelper.GetProductId(_productAlias);
        _queryHelper.InsertRunningMigrationRun(productId, "Docker");
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();

        try
        {
            if (Directory.Exists(_workDir))
                Directory.Delete(_workDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors for temp directories
        }
    }
}
