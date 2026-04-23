# RayMigratorService

The `RayMigratorService` bridges the CLI layer to the service layer. It is the final step in the console pipeline, invoked by `Program.cs` after all bootstrapping, configuration loading, and DI setup is complete.

## Purpose

- Convert CLI arguments to service requests
- Execute service methods
- Convert results to exit codes
- Handle top-level logging

## Location

- `Raycoon.RayMigrator.Pipeline/RayMigratorService.cs` -- Command dispatch bridge
- `Raycoon.RayMigrator.Console/Program.cs` -- Entry point, CLI parsing, environment resolution, early exits
- `Raycoon.RayMigrator.Pipeline/DirectModePipeline.cs` -- Unified execution pipeline for Standalone mode
- `Raycoon.RayMigrator.Pipeline/JsonOptionsSource.cs` -- Configuration loading from JSON files (Standalone mode)
- `Raycoon.RayMigrator.Pipeline/SerilogFactory.cs` -- Serilog logger creation
- `Raycoon.RayMigrator.Core/Configuration/EnvironmentResolver.cs` -- Environment resolution logic (called from Program.cs)

## Program.cs Orchestration

Before `RayMigratorService.DoWorkAsync` is called, `Program.Main()` performs a multi-stage pipeline:

### Stage 1: Command-Line Parsing

1. Parse CLI arguments via `CommandLineConfiguration` into `RayMigratorConsoleOptions`
2. Initialize `SensitiveDataMasker` based on the `--reveal-sensitive-data` flag

### Stage 2: Environment Resolution

Resolves the environment from either the `--environment` CLI argument or the `DOTNET_ENVIRONMENT` variable. If both are set but differ, exits with code 2. If neither is set, exits with code 3.

### Stage 3: Configuration Loading and Pipeline Execution

After environment resolution, `Program.Main()` runs in Standalone mode, loading all configuration from JSON files via `JsonOptionsSource`:

```
Main()
  |
  +-- Environment resolution
  |
  +-- RunDirectMode(JsonOptionsSource)
       |
       +-- JsonOptionsSource.LoadAsync() loads appsettings.json hierarchy
       |   (up to 4 files: base, environment, product, product+environment)
       |
       +-- DirectModePipeline.ExecuteAsync() handles the full lifecycle
```

`RunDirectMode` delegates to `DirectModePipeline.ExecuteAsync()`, which handles the complete lifecycle:
1. Validate Serilog configuration exists in the loaded config
2. Create Serilog logger (with optional database sink) and log environment variable replacements
3. Build the DI host and register all services (including `TemplateCache`, `MigrationContext`, `RayMigratorService`)
4. Resolve `DatabaseLogWriter` (triggers options validation in Standalone mode)
5. Register sensitive configuration values for masking in TRACE logs
6. Validate the `--product` alias exists in the loaded configuration (with case-sensitive matching and helpful suggestions)
7. Resolve `MigrationContext` and set `MigrationLoggingContext.Current` (enables log enrichment with migration properties)
8. Initialize database logging infrastructure (wire database sink to `DatabaseLogWriter`)
9. Populate `DalSpecificPropertiesDictionary` for all configured database types and validate schema names
10. Validate all target connection strings
11. Resolve `RayMigratorService` from DI and call `DoWorkAsync`
12. Wait for queued database logs to flush, then stop the host

## Dependencies

```csharp
public class RayMigratorService
{
    private readonly ILogger<RayMigratorService> _logger;
    private readonly RayMigratorConsoleOptions _consoleOptions;
    private readonly IMigrationService _migrationService;

    public RayMigratorService(
        ILogger<RayMigratorService> logger,
        RayMigratorConsoleOptions consoleOptions,
        IMigrationService migrationService)
    {
        _logger = logger;
        _consoleOptions = consoleOptions;
        _migrationService = migrationService;
    }
}
```

## Methods

### DoWorkAsync (Entry Point)

The central dispatch method uses a switch on `_consoleOptions.Command`:

```csharp
public async Task<int> DoWorkAsync(IHost host)
{
    try
    {
        _logger.LogDebug("Executing command: {Command}", _consoleOptions.Command);

        switch (_consoleOptions.Command)
        {
            case MigrationCommand.MigrateUp:
                return await ExecuteMigrateUpAsync();
            case MigrationCommand.MigrateDown:
                return await ExecuteMigrateDownAsync();
            case MigrationCommand.ValidateHash:
                return await ExecuteValidateHashAsync();
            case MigrationCommand.UpdateHash:
                return await ExecuteUpdateHashAsync();
            case MigrationCommand.Info:
                return await ExecuteInfoAsync();
            case MigrationCommand.Baseline:
                return await ExecuteBaselineAsync();
            case MigrationCommand.FixIssues:
                return await ExecuteFixIssuesAsync();
            default:
                throw new ConfigurationValidationException(
                    $"Unknown command: {_consoleOptions.Command}. Valid commands: {string.Join(", ", Enum.GetNames<MigrationCommand>())}");
        }
    }
    catch (MigrationAlreadyRunningException ex)
    {
        _logger.LogError(ex, "Another migration is already running for this product");
        _logger.LogInformation(
            "To resolve this issue, either wait for the running migration to complete, " +
            "or use the Fix command to clean up orphaned runs: " +
            "RayMigrator Fix --product {Product} --environment {Environment} --scope OrphanedRuns",
            _consoleOptions.Product, _consoleOptions.Environment);
        return 1;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error executing command {Command}", _consoleOptions.Command);
        return 1;
    }
}
```

### ExecuteMigrateUpAsync

```csharp
private async Task<int> ExecuteMigrateUpAsync()
{
    _logger.LogDebug("Executing Migrate-Up command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var request = new MigrateUpRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        TargetReleaseVersion = _consoleOptions.TargetReleaseVersion,
        RunMode = _consoleOptions.RunMode,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        AllowOutOfOrder = _consoleOptions.AllowOutOfOrder == true,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
        TargetGroupMigrationOrder = _consoleOptions.TargetGroupMigrationOrder,
    };

    // Note: StopRollbackOnMissingRollbackFile is not part of the request — MigrationService
    // reads it directly from the MigrationContext (which carries RayMigratorConsoleOptions).

    var result = await _migrationService.MigrateUpAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Migrate-Up failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    return 0;
}
```

### ExecuteMigrateDownAsync

```csharp
private async Task<int> ExecuteMigrateDownAsync()
{
    _logger.LogDebug("Executing Migrate-Down command for product {Product} to release {TargetRelease}",
        _consoleOptions.Product, _consoleOptions.TargetReleaseVersion);

    var request = new MigrateDownRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        TargetReleaseVersion = _consoleOptions.TargetReleaseVersion!,
        RunMode = _consoleOptions.RunMode,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
    };

    var result = await _migrationService.MigrateDownAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Migrate-Down failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    return 0;
}
```

### ExecuteValidateHashAsync

```csharp
private async Task<int> ExecuteValidateHashAsync()
{
    _logger.LogDebug("Executing Validate-Hash command for product {Product} with scope {Scope}",
        _consoleOptions.Product, _consoleOptions.HashValidationScope);

    var request = new ValidateHashRequest
    {
        ProductAlias = _consoleOptions.Product!,
        HashValidationScope = _consoleOptions.HashValidationScope,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
    };

    var result = await _migrationService.ValidateHashAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Validate-Hash failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Validate-Hash completed. Total: {Total}, Valid: {Valid}, Invalid: {Invalid}, Missing: {Missing}",
        result.TotalFiles, result.ValidFiles, result.InvalidFiles, result.MissingFiles);

    foreach (var issue in result.Issues)
    {
        _logger.LogWarning("Hash issue: {File} - {Type}: {Details}",
            issue.FileName, issue.IssueType, issue.Details);
    }

    return result.InvalidFiles > 0 || result.MissingFiles > 0 ? 1 : 0;
}
```

### ExecuteUpdateHashAsync

```csharp
private async Task<int> ExecuteUpdateHashAsync()
{
    _logger.LogDebug("Executing Update-Hash command for product {Product}", _consoleOptions.Product);

    var request = new UpdateHashRequest
    {
        ProductAlias = _consoleOptions.Product!,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
    };

    var result = await _migrationService.UpdateHashAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Update-Hash failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Update-Hash completed. Updated: {Updated}, New: {New}, Removed: {Removed}",
        result.UpdatedFiles, result.NewFiles, result.RemovedFiles);

    return 0;
}
```

### ExecuteBaselineAsync

`--to-release` is optional. When omitted, all releases are baselined. The `releaseLabel` variable formats log messages accordingly.

```csharp
private async Task<int> ExecuteBaselineAsync()
{
    var releaseLabel = string.IsNullOrWhiteSpace(_consoleOptions.TargetReleaseVersion)
        ? "all releases"
        : $"up to release {_consoleOptions.TargetReleaseVersion}";

    _logger.LogDebug("Executing Baseline command for product {Product} ({ReleaseScope})",
        _consoleOptions.Product, releaseLabel);

    var request = new BaselineRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        TargetReleaseVersion = _consoleOptions.TargetReleaseVersion,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
        TargetGroupMigrationOrder = _consoleOptions.TargetGroupMigrationOrder,
    };

    var result = await _migrationService.BaselineAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Baseline failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Baseline completed for product {Product}: {Count} file(s) baselined ({ReleaseScope})",
        _consoleOptions.Product, result.BaselinedFiles, releaseLabel);
    return 0;
}
```

### ExecuteInfoAsync

Calls both `GetStatusAsync` and `GetHistoryAsync` to display migration status information including current release, pending migrations, target group details, and recent migration run history. The history is rendered as a formatted table using `StringBuilder`:

```csharp
private async Task<int> ExecuteInfoAsync()
{
    _logger.LogDebug("Executing Info command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var status = await _migrationService.GetStatusAsync(_consoleOptions.Product!);
    var history = await _migrationService.GetHistoryAsync(_consoleOptions.Product!, 10);

    // Display status summary
    _logger.LogInformation("--- Migration Status for product {Product}, environment {Environment} ---", status.ProductAlias, _consoleOptions.Environment);
    _logger.LogInformation("  Current Release:     {Release}", status.CurrentRelease);
    _logger.LogInformation("  Pending Migrations:  {Pending}", status.PendingMigrations);
    _logger.LogInformation("  Total Executed:      {Total}", status.TotalMigrationsExecuted);

    // Display target groups
    if (status.TargetGroups.Count > 0)
    {
        _logger.LogInformation("--- Target Groups ---");
        foreach (var (alias, tg) in status.TargetGroups)
        {
            _logger.LogInformation("  [{Alias}] Type={DbType}, Release={Release}, Executed={Count}, Targets=[{Targets}]",
                tg.Alias, tg.DatabaseType, tg.CurrentRelease, tg.ExecutedMigrations,
                string.Join(", ", tg.Targets));
        }
    }

    // Display recent history as formatted table
    if (history.Runs.Count > 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Last {history.Runs.Count} Migration Runs ---");
        sb.AppendLine($"  {"RunId",7}  {"Command",-12}  {"RunMode",-10}  {"Result",-9}  {"# Migrations",12}  {"StartedAt",-19}  {"FinishedAt",-19}  {"DurationInMs",12}");
        sb.AppendLine($"  {new string('─', 7)}  {new string('─', 12)}  {new string('─', 10)}  {new string('─', 9)}  {new string('─', 12)}  {new string('─', 19)}  {new string('─', 19)}  {new string('─', 12)}");
        foreach (var run in history.Runs)
        {
            var finishedAt = run.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
            var durationInMs = (run.CompletedAt - run.StartedAt)?.TotalMilliseconds is { } ms
                ? $"{ms,12:F0}"
                : $"{"—",12}";
            sb.AppendLine($"  {run.MigrationRunId,7}  {run.Operation,-12}  {run.RunMode,-10}  {run.Result,-9}  {run.TotalMigrations,12}  {run.StartedAt:yyyy-MM-dd HH:mm:ss}  {finishedAt,-19}  {durationInMs}");
        }
        _logger.LogInformation("\n{MigrationRunHistory}", sb.ToString().TrimEnd());
    }
    else
    {
        _logger.LogDebug("No migration runs found.");
    }

    return 0;
}
```

### ExecuteFixIssuesAsync

Fixes repository inconsistencies such as orphaned migration runs. Supports a dry-run mode to preview what would be fixed without making changes:

```csharp
private async Task<int> ExecuteFixIssuesAsync()
{
    _logger.LogDebug("Executing Fix command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var request = new FixIssuesRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        Scope = _consoleOptions.FixIssues ?? FixIssues.OrphanedRuns,
        OlderThanMinutes = _consoleOptions.FixOlderThanMinutes ?? 60,
        DryRun = _consoleOptions.FixDryRun ?? false,
        AssumedMigrationStatus = _consoleOptions.FixAssumedMigrationStatus ?? MigrationStatus.NotMigrated,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData
    };

    var result = await _migrationService.FixIssuesAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Fix command failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    if (result.WasDryRun)
    {
        _logger.LogInformation("Fix dry-run completed for product {Product}: {Found} orphaned run(s) found",
            _consoleOptions.Product, result.OrphanedRunsFound);
    }
    else
    {
        _logger.LogInformation("Fix completed for product {Product}: {Fixed} orphaned run(s) fixed",
            _consoleOptions.Product, result.OrphanedRunsFixed);
    }

    return 0;
}
```

## Integration with CLI

CLI argument parsing is handled by `CommandLineConfiguration` (in the Core project), which populates `RayMigratorConsoleOptions`. `RayMigratorConsoleOptions` is registered as a singleton in DI via `DirectModePipeline` (in the Pipeline project). The `RayMigratorService` receives these options via constructor injection and dispatches to the appropriate method via `DoWorkAsync`.

## Error Handling Pattern

All command methods follow the same pattern:

1. Build a request object from `_consoleOptions`
2. Call the appropriate `_migrationService` method
3. Check `result.Success` and log `result.ErrorMessage` on failure
4. Return `0` for success, `1` for failure

Top-level exception handling in `DoWorkAsync` catches `MigrationAlreadyRunningException` specifically (with guidance to use the Fix command) and all other exceptions generically, returning exit code `1` in both cases.

## Exit Codes

See [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes) for the complete exit code table.

## Related Documentation

- [Command Structure](command-structure.md) - CLI setup and `CommandLineConfiguration`
- [Migration Service](../04-service-layer/migration-service.md) - Service interface
- [Launch Profiles](launch-profiles.md) - IDE configuration
- [Configuration Reference](../06-configuration-reference/) - Bootstrap and migration configuration
