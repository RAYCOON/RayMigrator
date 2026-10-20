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
- `Raycoon.RayMigrator.Pipeline/ConsoleCancellation.cs` -- Ctrl+C and SIGTERM while the command runs
- `Raycoon.RayMigrator.Pipeline/JsonOptionsSource.cs` -- Configuration loading from JSON files (Standalone mode)
- `Raycoon.RayMigrator.Pipeline/SerilogFactory.cs` -- Serilog logger creation
- `Raycoon.RayMigrator.Core/Configuration/EnvironmentResolver.cs` -- Environment resolution logic (called from Program.cs)

## Program.cs Orchestration

Before `RayMigratorService.DoWorkAsync` is called, `Program.Main()` performs a multi-stage pipeline:

### Stage 1: Command-Line Parsing

1. Parse CLI arguments via `CommandLineConfiguration` into `RayMigratorConsoleOptions`
2. Initialize `SensitiveDataMasker` based on the `--reveal-sensitive-data` flag

### Stage 2: Environment Resolution

Resolves the environment from the `--environment` CLI argument, cross-checked against the `DOTNET_ENVIRONMENT` variable: if both are set but differ, the process exits with code 2. A blank `--environment` exits with code 3 whether or not the variable is set; the variable never replaces the option (#32). The parser requires the option, so omitting it is a parse error.

### Stage 3: Configuration Loading and Pipeline Execution

After environment resolution, `Program.Main()` runs in Standalone mode, loading all configuration from JSON files via `JsonOptionsSource`:

```
Main()
  |
  +-- Environment resolution
  |
  +-- One RunCancellation for the process
  |
  +-- RunDirectMode(JsonOptionsSource, cancellation)
       |
       +-- JsonOptionsSource.LoadAsync() loads appsettings.json hierarchy
       |   (up to 4 files: base, environment, product, product+environment),
       |   merges it by alias and keeps the shared sections and the product
       |   of the run (ADR-026); an alias the files do not name fails here
       |
       +-- DirectModePipeline.ExecuteAsync() handles the full lifecycle
```

`RunDirectMode` delegates to `DirectModePipeline.ExecuteAsync()`, which handles the complete lifecycle:
1. Validate Serilog configuration exists in the loaded config (returns exit code 4 if missing; the message names the missing section and lists the loaded files, and names the files of the hierarchy that were not found only when no loaded file carries a `RayMigrator` section, #31)
2. Create Serilog logger (with optional database sink) and log environment variable replacements
3. Build the DI host and register all services (including `RayMigratorOptions`, `RayMigratorConsoleOptions`, `DatabaseLogWriter`, the Service Layer via `AddRayMigratorServices`, `TemplateCache`, `TemplateExecutor`, `RayMigratorService`, and `MigrationContext`)
4. Resolve `DatabaseLogWriter` (triggers options validation in Standalone mode: the data annotations, naming a failed element by alias, and the rule catalog, both on the shared sections and the run's product)
5. Register sensitive configuration values for masking in TRACE logs
6. Resolve `MigrationContext`: the product alias, which the loader already found among the files' aliases, is matched case-sensitively here (a near miss in another case gets a hint), and `MigrationLoggingContext.Current` is set (enables log enrichment with migration properties)
7. Initialize database logging infrastructure (`InitDatabaseLoggerAsync` runs `DatabaseLogging_CheckCreate`, then the database sink is wired to `DatabaseLogWriter`)
8. Populate `DalSpecificPropertiesDictionary` for all configured database types and validate schema names (Repository and DatabaseLogging)
9. Validate all target connection strings via `ConnectionValidator.ValidateTargetConnections`
10. Resolve `RayMigratorService` from DI and call `DoWorkAsync(host, cancellation)` inside `ConsoleCancellation.Listen`: while the command runs, the first Ctrl+C or SIGTERM (SIGTERM on Linux and macOS) cancels the `RunCancellation` of the process with the `--cancelled-action` of the command and keeps the process alive, a second one keeps the default and ends the process at once; before and after the command both signals keep their default
11. Flush queued database logs (`dbLogWriter.Flush()`), then stop the host

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

The central dispatch method uses a switch on `_consoleOptions.Command`. It takes the `RunCancellation` of the process: migrate-up, migrate-down and baseline carry it in their request, so that a cancelled migrate-up takes the action of its `Cancel` (migrate-down and baseline stop and ignore it), and every service call observes its token. It returns 0 for success, 130 (`CancelledExitCode`, 128 + SIGINT) when the cancel stopped the command, and 1 for a failure:

```csharp
public async Task<int> DoWorkAsync(IHost host, RunCancellation cancellation)
{
    ArgumentNullException.ThrowIfNull(cancellation);

    try
    {
        _logger.LogDebug("Executing command: {Command}", _consoleOptions.Command);

        switch (_consoleOptions.Command)
        {
            case MigrationCommand.MigrateUp:
                return await ExecuteMigrateUpAsync(cancellation);
            case MigrationCommand.MigrateDown:
                return await ExecuteMigrateDownAsync(cancellation);
            case MigrationCommand.ValidateHash:
                return await ExecuteValidateHashAsync(cancellation);
            case MigrationCommand.UpdateHash:
                return await ExecuteUpdateHashAsync(cancellation);
            case MigrationCommand.Info:
                return await ExecuteInfoAsync(cancellation);
            case MigrationCommand.Baseline:
                return await ExecuteBaselineAsync(cancellation);
            case MigrationCommand.FixIssues:
                return await ExecuteFixIssuesAsync(cancellation);
            default:
                throw new ConfigurationValidationException(
                    $"Unknown command: {_consoleOptions.Command}. Valid commands: {string.Join(", ", Enum.GetNames<MigrationCommand>())}");
        }
    }
    catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
    {
        // The read commands (validate-hash, info) throw when they are cancelled; the others return their result
        _logger.LogWarning("Command {Command} for product {Product} cancelled on request ({Reason})",
            _consoleOptions.Command, _consoleOptions.Product, cancellation.Reason ?? "no reason given");
        return CancelledExitCode;
    }
    catch (MigrationAlreadyRunningException ex)
    {
        _logger.LogError(ex, "Another migration is already running for this product");
        _logger.LogInformation(
            "To resolve this issue, either wait for the running migration to complete, " +
            "or use the Fix command to clean up orphaned runs: " +
            "raymigrator fix --product {Product} --environment {Environment} --scope orphanedruns",
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
private async Task<int> ExecuteMigrateUpAsync(RunCancellation cancellation)
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
        Cancellation = cancellation,
    };

    // Note: StopRollbackOnMissingRollbackFile is not part of the request — MigrationService
    // reads it directly from the MigrationContext (which carries RayMigratorConsoleOptions).

    var result = await _migrationService.MigrateUpAsync(request, cancellation.Token);

    if (!result.Success)
    {
        if (result.Result == MigrationRunResult.Cancelled)
        {
            _logger.LogWarning("Migrate-Up cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;
        }

        _logger.LogError("Migrate-Up failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    return 0;
}
```

### ExecuteMigrateDownAsync

```csharp
private async Task<int> ExecuteMigrateDownAsync(RunCancellation cancellation)
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
        Cancellation = cancellation,
    };

    var result = await _migrationService.MigrateDownAsync(request, cancellation.Token);

    if (!result.Success)
    {
        if (result.Result == MigrationRunResult.Cancelled)
        {
            _logger.LogWarning("Migrate-Down cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;
        }

        _logger.LogError("Migrate-Down failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    return 0;
}
```

### ExecuteValidateHashAsync

```csharp
private async Task<int> ExecuteValidateHashAsync(RunCancellation cancellation)
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

    var result = await _migrationService.ValidateHashAsync(request, cancellation.Token);

    // ValidationResult.Success means "no hash issues found", not "the command ran".
    // A command failure (exception inside the service) carries an ErrorMessage; hash issues do not.
    if (!result.Success && result.ErrorMessage != null)
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
private async Task<int> ExecuteUpdateHashAsync(RunCancellation cancellation)
{
    _logger.LogDebug("Executing Update-Hash command for product {Product}", _consoleOptions.Product);

    var request = new UpdateHashRequest
    {
        ProductAlias = _consoleOptions.Product!,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
    };

    var result = await _migrationService.UpdateHashAsync(request, cancellation.Token);

    if (!result.Success)
    {
        // The engine maps every OperationCanceledException to Cancelled; only the cancel of the process exits with 130
        if (result.ErrorCode == TemplateResultCode.Cancelled && cancellation.Token.IsCancellationRequested)
        {
            _logger.LogWarning("Update-Hash cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;
        }

        _logger.LogError("Update-Hash failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Update-Hash completed. Updated: {Updated} file(s) / {UpdatedRecords} record(s), New: {New}, Removed: {Removed}",
        result.UpdatedFiles, result.UpdatedRecords, result.NewFiles, result.RemovedFiles);

    return 0;
}
```

### ExecuteBaselineAsync

`--to-release` is optional. When omitted, all releases are baselined. The `releaseLabel` variable formats log messages accordingly.

```csharp
private async Task<int> ExecuteBaselineAsync(RunCancellation cancellation)
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
        Cancellation = cancellation,
    };

    var result = await _migrationService.BaselineAsync(request, cancellation.Token);

    if (!result.Success)
    {
        if (result.Result == MigrationRunResult.Cancelled)
        {
            _logger.LogWarning("Baseline cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;
        }

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

Calls both `GetStatusAsync` and `GetHistoryAsync` to display migration status information including current release, pending migrations, target group details, and recent migration run history. The pending count comes last in the summary, so that the list of pending migrations follows it (`MigrationStatusInfo.PendingFiles`, in the order of the next migrate-up, cut after `PendingListLimit` = 50 entries); every target group line names its pending count and its last migration (`TargetGroupStatus.LastMigrationDate`, `—` without one). The history is rendered as a formatted table using `StringBuilder`:

```csharp
private async Task<int> ExecuteInfoAsync(RunCancellation cancellation)
{
    _logger.LogDebug("Executing Info command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var status = await _migrationService.GetStatusAsync(_consoleOptions.Product!, cancellation.Token);
    var history = await _migrationService.GetHistoryAsync(_consoleOptions.Product!, 10, cancellation.Token);

    // Display status summary; the pending migrations come last, so that their list follows their count
    _logger.LogInformation("--- Migration Status for product {Product}, environment {Environment} ---", status.ProductAlias, _consoleOptions.Environment);
    _logger.LogInformation("  Current Release:     {Release}", status.CurrentRelease);
    _logger.LogInformation("  Total Executed:      {Total}", status.TotalMigrationsExecuted);
    _logger.LogInformation("  Pending Migrations:  {Pending}", status.PendingMigrations);
    if (status.PendingFiles.Count > 0)
    {
        _logger.LogInformation("\n{PendingMigrationList}", FormatPendingMigrations(status.PendingFiles, PendingListLimit));
    }

    // Display target groups
    if (status.TargetGroups.Count > 0)
    {
        _logger.LogInformation("--- Target Groups ---");
        foreach (var (alias, tg) in status.TargetGroups)
        {
            _logger.LogInformation("  [{Alias}] Type={DbType}, Release={Release}, Executed={Count}, Pending={Pending}, LastMigration={LastMigration}, Targets=[{Targets}]",
                tg.Alias, tg.DatabaseType, tg.CurrentRelease, tg.ExecutedMigrations, tg.PendingMigrations,
                tg.LastMigrationDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—",
                string.Join(", ", tg.Targets));
        }
    }

    // Display recent history as formatted table
    if (history.Runs.Count > 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Last {history.Runs.Count} Migration Runs ---");
        sb.AppendLine($"  {"RunId",7}  {"Operation",-12}  {"RunMode",-10}  {"Result",-9}  {"# Migrations",12}  {"StartedAt",-19}  {"FinishedAt",-19}  {"DurationInMs",12}");
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

The pending list is a table of release, target group, target, file and reason, indented by four spaces, with a `─` rule under the header; a resumed file shows `Resumable after block n`, the last block that committed, and a file whose rollback stopped in the middle `RollbackIncomplete after k of n rollback blocks, run migrate-down`:

```csharp
internal static string FormatPendingMigrations(IReadOnlyList<PendingMigrationInfo> pending, int limit)
{
    var shown = pending.Take(limit).ToList();
    string[] headers = ["Release", "Target group", "Target", "File", "Reason"];
    var rows = shown
        .Select(p => new[] { p.ReleaseVersion, p.TargetGroupAlias, p.TargetAlias ?? "—", p.Filename, DescribeReason(p) })
        .ToList();
    int[] widths = headers.Select((header, column) => rows.Select(row => row[column].Length).Append(header.Length).Max()).ToArray();

    var sb = new StringBuilder();
    AppendRow(headers);
    AppendRow(widths.Select(width => new string('─', width)).ToArray());
    foreach (var row in rows)
    {
        AppendRow(row);
    }
    if (pending.Count > shown.Count)
    {
        sb.AppendLine($"    … and {pending.Count - shown.Count} more");
    }
    return sb.ToString().TrimEnd();

    void AppendRow(string[] cells)
        => sb.AppendLine(("    " + string.Join("  ", cells.Select((cell, column) => cell.PadRight(widths[column])))).TrimEnd());
}

internal static string DescribeReason(PendingMigrationInfo pending) => pending.Reason switch
{
    PendingMigrationReason.Resumable => $"Resumable after block {pending.ResumeFromBlock ?? 0}",
    PendingMigrationReason.RollbackIncomplete =>
        $"RollbackIncomplete after {pending.RollbackBlocksMigrated ?? 0} of {pending.RollbackBlocksTotal ?? 0} rollback blocks, run migrate-down",
    _ => pending.Reason.ToString()
};
```

### ExecuteFixIssuesAsync

Fixes repository inconsistencies such as orphaned migration runs. With `--run-mode simulate` it previews what would be fixed without making changes:

```csharp
private async Task<int> ExecuteFixIssuesAsync(RunCancellation cancellation)
{
    _logger.LogDebug("Executing Fix command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var request = new FixIssuesRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        Scope = _consoleOptions.FixScope ?? FixScope.OrphanedRuns,
        OlderThanMinutes = _consoleOptions.FixOlderThanMinutes ?? 2,
        RunMode = _consoleOptions.RunMode,
        AssumedMigrationStatus = _consoleOptions.FixAssumedMigrationStatus,   // null without --last-migration-status: the progress rule
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData
    };

    var result = await _migrationService.FixIssuesAsync(request, cancellation.Token);

    if (!result.Success)
    {
        // The engine maps every OperationCanceledException to Cancelled; only the cancel of the process exits with 130
        if (result.ErrorCode == TemplateResultCode.Cancelled && cancellation.Token.IsCancellationRequested)
        {
            _logger.LogWarning("Fix command cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;
        }

        _logger.LogError("Fix command failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    if (result.WasSimulated)
    {
        _logger.LogInformation("Fix simulation completed for product {Product} (repairs: {Repairs}): {Found} orphaned run(s) found",
            _consoleOptions.Product, string.Join(", ", result.Repairs), result.OrphanedRunsFound);
    }
    else
    {
        _logger.LogInformation("Fix completed for product {Product} (repairs: {Repairs}): {Fixed} orphaned run(s) fixed",
            _consoleOptions.Product, string.Join(", ", result.Repairs), result.OrphanedRunsFixed);
    }

    return 0;
}
```

`--older-than` defaults to 2 minutes, the default of `Repository.OrphanedRunThresholdInMinutes`: a run counts as orphaned when its last heartbeat (or, without one, its start) is older.

## Integration with CLI

CLI argument parsing is handled by `CommandLineConfiguration` (in the Core project), which populates `RayMigratorConsoleOptions`. `RayMigratorConsoleOptions` is registered as a singleton in DI via `DirectModePipeline` (in the Pipeline project), while `RayMigratorService` itself is registered as a scoped service. The `RayMigratorService` receives these options via constructor injection and dispatches to the appropriate method via `DoWorkAsync`.

## Error Handling Pattern

All command methods follow the same pattern:

1. Build a request object from `_consoleOptions` (migrate-up, migrate-down and baseline with the `RunCancellation` of the process)
2. Call the appropriate `_migrationService` method with the token of the process
3. Check `result.Success` and log `result.ErrorMessage` on failure, as a warning when the cancel ended the command
4. Return `0` for success, `130` when the cancel ended the command (`MigrationRunResult.Cancelled` for migrate-up, migrate-down and baseline, `TemplateResultCode.Cancelled` (1004) for update-hash and fix while the token of the process is cancelled), `1` for failure

A cancelled migrate-up whose rollback did not complete cleanly is an `Error` (code 1005) and exits with `1`, and so does a migrate-up that failed at a file before the cancel stopped its error recovery (`Error` with the code of the failure): the result decides, not the signal. The engine maps every `OperationCanceledException` of a failure to 1004, so an update-hash or fix that returns 1004 without the signal is logged as an error and exits with `1` as well. Top-level exception handling in `DoWorkAsync` catches an `OperationCanceledException` while the token of the process is cancelled (validate-hash and info throw it) and returns `130` with a warning; it catches `MigrationAlreadyRunningException` specifically (with guidance to use the Fix command) and all other exceptions generically, including an `OperationCanceledException` that did not come from the cancel of the process, returning exit code `1` in both cases. See [Cancellation](../02-core-concepts/error-handling.md#cancellation).

## Exit Codes

See [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes) for the complete exit code table.

## Related Documentation

- [Command Structure](command-structure.md) - CLI setup and `CommandLineConfiguration`
- [Migration Service](../04-service-layer/migration-service.md) - Service interface
- [Launch Profiles](launch-profiles.md) - IDE configuration
- [Configuration Reference](../06-configuration-reference/) - Bootstrap and migration configuration
