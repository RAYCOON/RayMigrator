# Component Responsibilities

Detailed breakdown of each component's responsibilities and key classes.

## Console Layer Components

### `Program.cs`
**Location**: `Raycoon.RayMigrator.Console/Program.cs`

**Responsibilities**:
- Parse and validate command-line arguments via `CommandLineConfiguration` (System.CommandLine)
- Initialize `SensitiveDataMasker` with the `RevealSensitiveData` flag from the parsed options
- Resolve the environment from the `--environment` argument, cross-checked against `DOTNET_ENVIRONMENT`, via `EnvironmentResolver`
- Dispatch to `RunDirectMode()` with `JsonOptionsSource` (constructed from `rayMigratorConsoleOptions.ConfigDir`)
- `RunDirectMode()` loads the configuration via `IOptionsSource.LoadAsync()` and then delegates to `DirectModePipeline.ExecuteAsync()` for host build and service execution, with the one `RunCancellation` of the process, which Ctrl+C and SIGTERM cancel while the command runs
- Converts exceptions into exit codes (parser failure = 5, `ApplicationStartupException` = 1, other exceptions = 100)

### `AssemblyInfoHelper` (Console)
**Location**: `Raycoon.RayMigrator.Console/AssemblyInfoHelper.cs`

**Purpose**: Provides `GetAssemblyInfo()` (delegates to `Shared.AssemblyInfoHelper.GetAsciiHeader()`) and `GetRayMigratorVersion()` (delegates to `Shared.AssemblyInfoHelper.GetRayMigratorVersion()`).

### Launch Profiles
**Location**: `Raycoon.RayMigrator.Console/Properties/launchSettings.json`

**Purpose**: IDE integration for development/debugging. Defines profiles per engine (SQL Server, PostgreSQL, MariaDB, MySQL, SQLite) and platform (Mac/Win) with the CLI arguments and `ConnectionString_*` environment variables required by the referenced appsettings files.

**Required Environment Variables**:
- `DOTNET_ENVIRONMENT` - Target environment of the profile, passed to the CLI through `-env {ENV:DOTNET_ENVIRONMENT}` (`Docker` in the Docker profiles)
- `ConnectionString_*` - Database connection strings consumed via `{ENV:ConnectionString_*}` placeholders in appsettings

## Pipeline Layer Components

### `DirectModePipeline`
**Location**: `Raycoon.RayMigrator.Pipeline/DirectModePipeline.cs`

**Purpose**: Execution pipeline of the CLI after the options have been loaded from the appsettings hierarchy. Handles the complete lifecycle: Serilog creation, DI host build, DatabaseLogWriter init, DAL properties, connection validation, `RayMigratorService` execution, and shutdown. The options are always bound from the configuration section; a host that builds options in memory goes through `IMigrationContextFactory` instead.

**Key Steps**:
1. Early configuration validation (check for Serilog section)
2. Create Serilog logger via `SerilogFactory.Create()`
3. Log environment variable replacements
4. Build DI host with `Host.CreateDefaultBuilder(args)`
5. Resolve `DatabaseLogWriter` (triggers options validation)
6. Register sensitive data for masking
7. Validate product alias against configured products (with case-sensitivity hints)
8. Resolve `MigrationContext`, set `MigrationLoggingContext.Current`
9. Initialize `DatabaseLogWriter` with template infrastructure (`InitDatabaseLoggerAsync` runs `DatabaseLogging_CheckCreate`)
10. Initialize `DalSpecificPropertiesDictionary` for all configured database types
11. Validate schema names via `SchemaNameValidator`
12. Validate connections via `ConnectionValidator`
13. Execute `RayMigratorService.DoWorkAsync(host, cancellation)` inside `ConsoleCancellation.Listen`: while the command runs, the first Ctrl+C or SIGTERM cancels the `RunCancellation` of the process with the `--cancelled-action` of the command, and a second one ends the process at once
14. Shutdown (flush log queue, stop host)

### `JsonOptionsSource`
**Location**: `Raycoon.RayMigrator.Pipeline/JsonOptionsSource.cs`

**Purpose**: `IOptionsSource` implementation that loads configuration from JSON files (appsettings.json hierarchy), performs `{ENV:...}` replacement, and returns `OptionsSourceResult` with `HostConfiguration` for DI binding. Accepts an optional `configDir` parameter (from the `--config-dir` global CLI option) that overrides the base path for locating configuration files; defaults to the current working directory when null or empty.

### `RayMigratorService`
**Location**: `Raycoon.RayMigrator.Pipeline/RayMigratorService.cs`

**Responsibilities**:
- Bridge between CLI commands and `IMigrationService`
- Create request objects from CLI parameters
- Execute service methods asynchronously
- Put the `RunCancellation` of the process into the requests of migrate-up, migrate-down and baseline and pass its token to every service call
- Convert service results to exit codes (0 = success, 1 = error, 130 = the command ended because Ctrl+C or SIGTERM cancelled it: a run result `Cancelled`, or `1004` from update-hash and fix while the token of the process is cancelled; a `1004` without the signal is 1, and so is a run result `Error` after the signal) and log such a cancel as a warning instead of an error
- Handle top-level exceptions (including `MigrationAlreadyRunningException`, and an `OperationCanceledException` while the token of the process is cancelled, which exits with 130)

**Key Methods**:
```csharp
Task<int> DoWorkAsync(IHost host, RunCancellation cancellation)   // Primary entry point, dispatches by command
// All Execute* methods are private and take the RunCancellation of the process:
private Task<int> ExecuteMigrateUpAsync(RunCancellation cancellation)
private Task<int> ExecuteMigrateDownAsync(RunCancellation cancellation)
private Task<int> ExecuteValidateHashAsync(RunCancellation cancellation)
private Task<int> ExecuteUpdateHashAsync(RunCancellation cancellation)
private Task<int> ExecuteBaselineAsync(RunCancellation cancellation)
private Task<int> ExecuteInfoAsync(RunCancellation cancellation)
private Task<int> ExecuteFixIssuesAsync(RunCancellation cancellation)
// The pending list of info: the first `limit` entries, then "… and n more"
internal static string FormatPendingMigrations(IReadOnlyList<PendingMigrationInfo> pending, int limit)
```

### `ConsoleCancellation`
**Location**: `Raycoon.RayMigrator.Pipeline/ConsoleCancellation.cs` (internal)

**Purpose**: Lets Ctrl+C and SIGTERM cancel the command the CLI runs. `DirectModePipeline` listens only while `DoWorkAsync` runs; before and after it both signals keep their default. The first signal suppresses the default and queues the cancel to the thread pool, in the execution context of the command, because the cancel callbacks of the providers run on the calling thread (MySQL opens a connection for `KILL QUERY`); the queued work logs a warning, which reaches the database log of the run, and calls `RunCancellation.Cancel` with the `--cancelled-action` of the command (`RayMigratorConsoleOptions.CancelledAction`); every later signal keeps the default and ends the process at once, which leaves an open run row to the orphan repair. SIGTERM is handled on Linux and macOS only. See [Cancellation](../02-core-concepts/error-handling.md#cancellation).

### `SerilogFactory`
**Location**: `Raycoon.RayMigrator.Pipeline/SerilogFactory.cs`

**Purpose**: Creates and configures the Serilog logger from the RayMigrator configuration section. Creates the database sink unless `DatabaseLogging` is disabled (`Enabled: false`). Also maps `Microsoft.Extensions.Logging.LogLevel` to `Serilog.Events.LogEventLevel`.

## Service Layer Components

### `IMigrationService`
**Location**: `Raycoon.RayMigrator.Services.Abstractions/IMigrationService.cs`

**Interface Definition**:
```csharp
public interface IMigrationService
{
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request, CancellationToken cancellationToken = default);
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request, CancellationToken cancellationToken = default);
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request, CancellationToken cancellationToken = default);
    Task<BaselineResult> BaselineAsync(BaselineRequest request, CancellationToken cancellationToken = default);
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias, CancellationToken cancellationToken = default);
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100, CancellationToken cancellationToken = default);
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request, CancellationToken cancellationToken = default);
}
```

A cancelled migrate-up, migrate-down or baseline stops at the next block or file boundary and returns `Success = false` with `Result = Cancelled` (`Error` when the rollback of a cancelled migrate-up did not complete cleanly); update-hash and fix return `ErrorCode = 1004` (`TemplateResultCode.Cancelled`) with what they did so far; validate-hash, status and history throw `OperationCanceledException`. The token of a call cancels with `MigrationCancelledAction.Terminate`; a host that lets the user choose the action passes a `RunCancellation` in the request. See [Cancellation](../02-core-concepts/error-handling.md#cancellation) and [ADR-028](../arc42/09-Architecture-Decisions.md#adr-028-a-run-cancels-at-the-next-block-boundary-with-a-migrationcancelledaction-and-records-cancelled).

### `MigrationService`
**Location**: `Raycoon.RayMigrator.Services/MigrationService.cs`

**Responsibilities**:
- Implement `IMigrationService`
- Validate requests against `MigrationContext`
- Coordinate with `TemplateExecutor` for repository operations
- Orchestrate migration execution flow
- Stop a cancelled run at the next block boundary and take its `MigrationCancelledAction`
- Return strongly-typed results

**Dependencies**:
- `ILogger<MigrationService>` (injected)
- `TemplateExecutor` (injected)
- `IMigrationContextAccessor` (injected) — accesses the current `MigrationContext` via `_ctxAccessor.Current`
- `IServiceProvider` (injected)
- `ICliToolExecutor` (injected) — executes external CLI tools when `UseCliToolAlias` is configured

**Key internal methods related to file discovery**:
- `DiscoverAndPrepareMigrationFiles` — entry point for file scanning, calls the validators below
- `ValidateTargetGroupAliasCasing` (internal static) — checks that subdirectory names in release directories match configured TargetGroup aliases exactly (case-sensitive); throws `ConfigurationValidationException` on mismatch
- `ValidateFlatLayoutAmbiguity` (internal static) — for single-TargetGroup products, ensures no release directory mixes flat-layout files (directly under the release dir) and traditional-layout files (under a TargetGroup subdirectory); throws `ConfigurationValidationException` on ambiguity
- `ResolveTargetGroupMigrationOrder` (internal) — resolves TargetGroup execution order from CLI > migsettings > appsettings > config array order
- `LoadMigSettingsDefaults` (internal) — loads and merges `migsettings.txt` and `migsettings.{Environment}.txt` files

**Key internal methods related to execution**:
- `CanUseSharedConnection` (internal static) — returns `true` when the target and repository share the same `DatabaseType` and `ConnectionString`, `UseTransaction=true` is set, and block errors are not ignored (`MigrationErrorAction != Ignore`). When `true`, `ExecuteSqlBlocks` delegates to `ExecuteSqlBlocksAtomic`, which executes all SQL blocks and repository status updates in a single atomic transaction on a shared connection. Retries are supported: when `DbCommandMaxRetries > 0`, transient errors trigger a file-level retry (full transaction rollback and re-execution from block 1).

### Request/Response DTOs

**Location**: `Raycoon.RayMigrator.Services.Abstractions/Models/`

The request types of `IMigrationService` are in `Models/Requests.cs`, its result types in `Models/Results.cs`; `RunCancellation`, `PendingMigrationInfo`, `PendingMigrationReason`, `RepositorySchemaResult`, `RetentionRequest` and `RetentionResult` have files of their own.

| Request DTO | Purpose |
|-----|---------|
| `MigrateUpRequest` | Parameters for forward migration; `Cancellation` takes the `RunCancellation` of the run |
| `MigrateDownRequest` | Parameters for rollback migration; `Cancellation` takes the `RunCancellation` of the run |
| `ValidateHashRequest` | Parameters for hash validation |
| `UpdateHashRequest` | Parameters for hash update |
| `BaselineRequest` | Parameters for baseline operation; `Cancellation` takes the `RunCancellation` of the run |
| `FixIssuesRequest` | Parameters for repository fix operations |
| `RunCancellation` | The handle a host cancels one run with: `Cancel(action, reason)` cancels once, the first call decides the `MigrationCancelledAction` and later calls return false; `Token`, `Action`, `Reason` and `RequestedAtUtc` describe the request. `Cancel` runs the callbacks of the token, those of the providers among them, synchronously on the calling thread and swallows their exceptions, since the action and the token are set before. The engine links its `Token` with the token of the call. |

| Result DTO | Purpose |
|-----|---------|
| `OperationResult` | Abstract base class (Success, ErrorMessage, ErrorCode, Messages, ExecutedAt, Duration). `ErrorCode` is `int?`: negative = SQL template ResultCode, positive = C# backend ErrorCode, null = unclassified. |
| `MigrationOperationResult` | Result of migration up/down (extends `OperationResult`). Adds `MigrationRunId` (the repository run row, 0 without one), `ProductAlias`, `Environment`, `Operation`, `Result`, `TotalMigrations`, `SuccessfulMigrations`, `FailedMigrations`, `CurrentRelease`, `MigrationResults` (List<MigrationFileResult>). |
| `MigrationFileResult` | Result of a single migration file execution (FileName, ReleaseVersion, TargetGroup, TargetAlias (null when the entry summarizes all targets of the group), MigrationRecordId, Success, ErrorMessage, ExecutedAt, Duration). |
| `ValidationResult` | Result of hash validation (TotalFiles, ValidFiles, InvalidFiles, MissingFiles, Issues). |
| `HashValidationIssue` | Individual hash validation issue (FileName, IssueType, ExpectedHash, ActualHash, Details). |
| `HashUpdateResult` | Result of hash update (UpdatedFiles, NewFiles, RemovedFiles, UpdatedFileNames). |
| `BaselineResult` | Result of baseline operation (Environment, MigrationRunId, TargetReleaseVersion, BaselinedFiles, BaselinedRecords, Result: the result the run row records, `Ok`, `Error` or `Cancelled`). |
| `MigrationStatusInfo` | Current migration status for a product and environment (Environment, CurrentRelease, LastMigrationDate, TotalMigrationsExecuted, PendingMigrations, PendingFiles, LastRunResult, TargetGroups). `PendingFiles` lists what the next migrate-up runs, one `PendingMigrationInfo` per file and target in the order of that run; `PendingMigrations` is its length. |
| `PendingMigrationInfo` | One file on one target that the next migrate-up runs (ReleaseVersion, TargetGroupAlias, TargetAlias, Filename, FileOrderId, Reason, ResumeFromBlock, RollbackBlocksMigrated, RollbackBlocksTotal). `Reason` is a `PendingMigrationReason`: NotMigrated (10), Failed (20), Resumable (30, with `ResumeFromBlock`), RunAlways (40: the file runs in full on every migrate-up and is never Resumable), HashMismatch (50), RollbackIncomplete (60, with `RollbackBlocksMigrated` and `RollbackBlocksTotal`: the rollback of the file stopped in the middle, and migrate-up refuses to run until migrate-down has completed it; before every other reason). |
| `TargetGroupStatus` | Status of a target group (Alias, DatabaseType, CurrentRelease, ExecutedMigrations, PendingMigrations, LastMigrationDate, Targets). `PendingMigrations` counts its entries of `PendingFiles`; `LastMigrationDate` is the end of its Migrated record that finished last. |
| `MigrationHistory` | Migration run history of a product and environment (Environment, Runs: List<MigrationRunInfo>). |
| `MigrationRunInfo` | Information about a migration run (MigrationRunId, Environment, StartedAt, CompletedAt, Operation, Result, RunMode, TotalMigrations, SuccessfulMigrations, FailedMigrations, FromRelease, ToRelease). |
| `FixIssuesResult` | Result of Fix command (WasSimulated, OrphanedRunsFound, OrphanedRunsFixed, OrphanedRuns: List<OrphanedRunInfo>). |
| `OrphanedRunInfo` | Information about an orphaned MigrationRun entry (MigrationRunId, Environment, EnvironmentId, StartedAt, MinutesRunning, MigrationRunModeId, WasFixed). |

### `IRepositorySchemaService`
**Location**: `Raycoon.RayMigrator.Services.Abstractions/IRepositorySchemaService.cs`; implemented by `RepositorySchemaService` (`Raycoon.RayMigrator.Services/RepositorySchemaService.cs`)

**Purpose**: Creates or upgrades the schema of a repository from its `RepositoryOptions` alone, without a product, an environment or a migration context, for hosts that manage repositories (RayMigrator Studio). `Task<RepositorySchemaResult> EnsureAsync(RepositoryOptions repository, CancellationToken cancellationToken = default)` validates the options like the context factory validates the repository of a run, runs the schema steps of ADR-024 through `RepositorySchemaExecutor` and returns `RepositorySchemaResult` (`DatabaseType`, `VersionBefore`, `VersionAfter`, `MigratorMetaId`, `Created`, `Upgraded`). Registered as singleton by `AddRayMigratorServices`; it needs the host's `TemplateCache`.

### `IRepositoryMaintenanceService`
**Location**: `Raycoon.RayMigrator.Services.Abstractions/IRepositoryMaintenanceService.cs`; implemented by `RepositoryMaintenanceService` (`Raycoon.RayMigrator.Services/RepositoryMaintenanceService.cs`)

**Purpose**: Deletes old rows of the two tables that grow with every run, `MigrationRecordHistory` and `MigrationLog`, from the `RepositoryOptions` alone, for hosts that apply a retention policy (RayMigrator Studio, [ADR-027](../arc42/09-Architecture-Decisions.md#adr-027-the-engine-deletes-log-and-history-rows-on-a-hosts-request-and-never-reads-them-for-a-decision)). `Task<RetentionResult> CleanUpAsync(RepositoryOptions repository, RetentionRequest request, CancellationToken cancellationToken = default)` validates the options like `IRepositorySchemaService`, reads the schema version without an upgrade and runs the four retention templates through `RepositoryMaintenanceExecutor`: the history first, then the log, in batches of one statement that commit on their own, the oldest rows first, sparing the rows of open runs. `RetentionRequest(HistoryCutoffUtc, LogCutoffUtc, BatchSize = 2000, TimeBudget = null, DryRun = false)` names the cutoffs, the batch size and the time budget (null means 60 seconds); `RetentionResult(HistoryRows, LogRows, Batches, Completed, LogSkipped, Message)` says what was deleted or, in a dry run, counted. `MigrationRecord`, `MigrationRun` and `MigrationRunMeta` are never deleted. Registered as singleton by `AddRayMigratorServices`; it needs the host's `TemplateCache`. See [Repository schema: Retention](../03-database-layer/repository-schema.md#retention).

### CLI Tool Execution

**Location**: `Raycoon.RayMigrator.Services/CliToolExecutor.cs`

| Class | Purpose |
|-------|---------|
| `ICliToolExecutor` | Interface for executing external SQL CLI tools (sqlcmd, psql, mysql, mariadb, sqlite3) as an alternative to DAL-based SQL execution |
| `CliToolExecutor` | Implementation using `System.Diagnostics.Process`. Handles stdin/file input modes, timeout enforcement, exit code evaluation, and structured logging; kills the tool and the processes it started on timeout and when the caller cancels |
| `CliToolExecutionRequest` | Request model: `ExecutablePath`, `Arguments`, `InputMode`, `FileContent`, `FilePath`, `Filename`, `TimeoutInSeconds`, `ExitCodeMatcher` |
| `CliToolExecutionResult` | Result model: `Success`, `ExitCode`, `StandardOutput`, `StandardError`, `Duration`, `ErrorMessage` |

## Core Layer Components

### `MigrationContext`
**Location**: `Raycoon.RayMigrator.Core/MigrationContext.cs`

**Purpose**: Central state object carrying configuration and runtime state through execution pipeline.

**Key Properties**:
```csharp
public RayMigratorOptions RayMigratorOptions { get; set; }
public RayMigratorConsoleOptions RayMigratorConsoleOptions { get; set; }
public string RayMigratorVersion { get; set; }
public MigrationState MigrationState { get; set; }
public IEnumerable<TargetGroupOptions>? ProductTargetGroupOptionsEnumerable { get; init; }
public ConcurrentDictionary<string, DalSpecificProperties> DalSpecificPropertiesDictionary { get; set; }
public MigrationContext Clone { get; }  // Deep copy of MigrationState for logging
```

**Constructor**:
```csharp
public MigrationContext(
    RayMigratorOptions rayMigratorOptions,
    RayMigratorConsoleOptions rayMigratorConsoleOptions,
    string rayMigratorVersion,
    MigrationState? migrationState = null)
```

The constructor also eagerly initializes `ProductTargetGroupOptionsEnumerable` from the product matching `RayMigratorConsoleOptions.Product`.

**Lifecycle**: See [MigrationContext](../02-core-concepts/migration-context.md) for the full lifecycle description.

### Configuration Options

**Location**: `Raycoon.RayMigrator.Core/Configuration/Options/`

| Class | Purpose |
|-------|---------|
| `RayMigratorOptions` | Root configuration from appsettings.json |
| `RayMigratorConsoleOptions` | CLI parameters (command, product, environment, run mode, target release, target group aliases, target group execution order, hash validation scope, fix options, allow out of order, stop rollback on missing rollback file, cancelled action, config dir, etc.) |
| `CommandLineConfiguration` | System.CommandLine root command definition and argument parsing |
| `RepositoryOptions` | Repository database settings |
| `ProductDefaultOptions` | Product-level defaults (inherited by all products), includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile` |
| `TargetGroupDefaultOptions` | TargetGroup-level defaults (TargetMigrationOrder, HashValidationScope, StopRollbackOnMissingRollbackFile) |
| `TargetDefaultsOptions` | Target-level defaults (timeouts, retries) |
| `ProductOptions` | Product-level settings, includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile`, and `TargetGroupMigrationOrder` |
| `TargetGroupOptions` | Target group settings, includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile` |
| `TargetOptions` | Individual target settings, includes `UseCliToolAlias` and `CliToolParameters` |
| `DatabaseLoggingOptions` | Logging database settings |
| `SerilogOptions` | Serilog configuration section marker |
| `CliToolOptions` | External CLI tool definition (Alias, ExecutablePath, ArgumentTemplate, InputMode, SuccessExitCodes, CliToolTimeoutInSeconds). `ExitCodeMatcherInstance` provides the parsed `ExitCodeMatcher`. |
| `ExitCodeMatcher` | Parses `SuccessExitCodes` expressions (single values `"0"`, closed ranges `"1..5"`, open ranges `"10.."`, `"..-1"`) and matches process exit codes against them. |

### Migration State

**Location**: `Raycoon.RayMigrator.Core/MigrationState.cs`

**Properties**:
```csharp
// Migration Process
public MigrationEvent? MigrationEvent { get; set; }

// RunId's
public int MigratorMetaId { get; set; }  // Set from the RepositoryCheckCreateAsync result
public int ProductId { get; set; }
public int EnvironmentId { get; set; }   // Set from the RepositoryEnvironmentCheckInsertAsync result
public int MigrationRunId { get; set; }
public int MigrationRecordId { get; set; }

// File metadata
public string ReleaseVersionFromFileNameWithPath { get; set; }
public string FilenameWithRelativePath { get; set; }
public int FileOrderId { get; set; }
public int FileBlockId { get; set; }

// Step / Result
public MigrationRunResult MigrationRunResult { get; set; }
public MigrationOperation MigrationOperation { get; set; }
public MigrationStatus MigrationStatus { get; set; }

// TargetGroup- / Target-settings
public string TargetGroupAlias { get; set; }
public HashValidationScope? HashValidationScope { get; set; }
public string TargetAlias { get; set; }
```

### `MigrationStateSnapshot`
**Location**: `Raycoon.RayMigrator.Core/MigrationStateSnapshot.cs`

**Purpose**: Immutable snapshot of `MigrationState` properties used for structured logging. The snapshot captures all state properties (`ProductId`, `MigrationRunId`, `MigrationRecordId`, file metadata, `MigrationRunResult`, `MigrationOperation`, target group/target aliases, `HashValidationScope`) as `init`-only properties.

> **Note**: `MigrationContext.Clone` creates a new `MigrationContext` with a deep-copied `MigrationState`, not a `MigrationStateSnapshot`. The `MigrationStateSnapshot` class is a separate lightweight type for log enrichment.

### `IMigrationContextAccessor`
**Location**: `Raycoon.RayMigrator.Core/IMigrationContextAccessor.cs`

**Purpose**: Provides access to the current `MigrationContext` for the execution scope. Two implementations support different hosting models:

| Class | Purpose |
|-------|---------|
| `SingletonMigrationContextAccessor` | CLI mode: wraps a single `MigrationContext` instance (registered as Singleton) |
| `AsyncLocalMigrationContextAccessor` | API mode: uses a static `AsyncLocal<T>` for per-flow `MigrationContext` isolation (registered as Singleton) |

### `IMigrationContextFactory`
**Location**: `Raycoon.RayMigrator.Core/IMigrationContextFactory.cs`

**Purpose**: Factory for creating `MigrationContext` instances.

**Implementation**: `MigrationContextFactory` — creates a `MigrationContext` from `RayMigratorOptions` and `RayMigratorConsoleOptions`: `Create` prepares options a host built in memory (placeholders, defaults, data annotations, rule catalog), `CreateFromValidatedOptions` takes the options the CLI pipeline bound and validated. Both fill the DAL properties, check the schema names and make the context current. The command comes from the console options and is stamped into the MigrationRun settings snapshot (#6).

### `RayMigratorHostMode`
**Location**: `Raycoon.RayMigrator.Core/RayMigratorHostMode.cs`

**Purpose**: Enum distinguishing CLI mode (`Cli`) from API mode (`Api`). Used by `ServiceCollectionExtensions.AddRayMigratorServices()` to register the appropriate `IMigrationContextAccessor` implementation.

### Domain Models

**Location**: `Raycoon.RayMigrator.Core/Models/`

| Class | Purpose |
|-------|---------|
| `MigrationFileInfo` | Represents a parsed migration file ready for execution. Contains file metadata, TOML configuration, SQL blocks, computed hashes, and optional `UseCliToolAlias` override. |
| `MigrationRecord` | Represents a migration record from the repository database. Maps a row of `Repository_MigrationRecord_Select` or `Repository_MigrationRecordHistory_Select`, including `StartedAt` and `FinishedAt` (null when the select of an external DAL lacks them). |

### File Sorting

**Location**: `Raycoon.RayMigrator.Core/CultureDependendSorting.cs`

| Class | Purpose |
|-------|---------|
| `CultureDependentSorting` | Culture-aware file sorting utility. Provides `GetAndSortFilesRecursive()` for recursively enumerating and sorting files by relative directory path and filename using a specified `CultureInfo`. Note: the main migration file discovery in `MigrationService.DiscoverAndPrepareMigrationFiles` sorts files directly via `StringComparer.OrdinalIgnoreCase` rather than through this class. |

### Recovery

**Location**: `Raycoon.RayMigrator.Core/Recovery/`

| Class | Purpose |
|-------|---------|
| `InterruptedMigrationInfo` | Contains information about an interrupted migration that can be resumed. Includes block progress tracking (`BlocksMigrated`, `BlocksTotal`, `NextBlockToResume`, `ProgressPercent`). |

### Template Types

**Location**: `Raycoon.RayMigrator.Core/Templates/`

| Class | Purpose |
|-------|---------|
| `Template` | Represents a loaded SQL template with `TemplateType`, `DatabaseType`, `Filename`, and `Content` properties. |
| `TemplateResponse` | Result of a template execution, containing `ResultCode` (int), `ResultCodes` (int[], every leading code) and `ResultMessage` (string?). |
| `TemplateType` | Enum defining all SQL template types (30 values + Undefined). |

### `MigrationLoggingContext`
**Location**: `Raycoon.RayMigrator.Core/MigrationLoggingContext.cs`

**Purpose**: Static ambient context using `AsyncLocal<MigrationContext?>` for carrying `MigrationContext` through the async call chain. Used by the Serilog `MigrationContextEnricher` to add migration-specific properties to every log event without explicit parameter passing.

### Configuration Sources

**Location**: `Raycoon.RayMigrator.Core/Configuration/Sources/`

| Class | Purpose |
|-------|---------|
| `IOptionsSource` | Interface for loading the configuration of the CLI pipeline. Defines `LoadAsync(product, environment)` returning `OptionsSourceResult`; the one implementation is `JsonOptionsSource` (appsettings.json hierarchy). A host that builds options in memory goes through `IMigrationContextFactory` instead. |
| `OptionsSourceResult` | Result of configuration loading. Contains `RayMigratorConfigSection`, `RayMigratorConfigSectionAsWritten` (the section before placeholder resolution), `ReplacedEnvironmentVariables`, `HostConfiguration`, `ModeName`, and `ConfigFileDiagnostics`. |

### `EnvironmentResolver`
**Location**: `Raycoon.RayMigrator.Core/Configuration/EnvironmentResolver.cs`

**Purpose**: Static class that takes the target environment from `RayMigratorConsoleOptions.Environment` and cross-checks it against the `DOTNET_ENVIRONMENT` variable, which never replaces it. Returns a tuple of `(environment, environmentOrigin, errorCode)`. Detects conflicts between CLI argument and environment variable (returns error code 2) and a blank CLI argument (returns error code 3).

### `SensitiveDataMasker`
**Location**: `Raycoon.RayMigrator.Core/Configuration/SensitiveDataMasker.cs`

**Purpose**: Centralized static class for masking sensitive data (connection strings, passwords) in log output. Maintains a `HashSet<string>` of registered sensitive values and replaces them with a mask string. Thread-safe via lock-based writes and snapshot-based reads. CLI mode uses global state via `Initialize()`. API mode supports per-request scoping via `BeginScope()` using `AsyncLocal`.

### Configuration Validation

Validation is split across two projects. The **shared rule catalog** lives in `Raycoon.RayMigrator.Validation` (WASM-safe, zero dependencies — consumed by both the engine and the Blazor ConfigWizard). The **engine-side glue** lives in `Raycoon.RayMigrator.Core/Configuration/Validation/`.

**Shared catalog** (`Raycoon.RayMigrator.Validation`):

| Class | Purpose |
|-------|---------|
| `RuleCatalog` | Static entry point. `RuleCatalog.RunAll(ValidationInput)` executes all rules and returns a `ValidationReport`. Fixed rule list — no reflection. |
| `Rules/*` (9 rule classes) | `AliasUniquenessRule`, `TargetGroupMigrationOrderRule`, `SemanticContradictionsRule`, `CliToolDefinitionsRule`, `CliToolReferencesRule`, `CliToolParametersRule`, `SchemaRule`, `ConnectionStringRule`, `DefaultCascadeRule`. Each owns one rule-ID category. All `internal sealed`. |
| `RuleIds` | String constants (`RULE_1_1` … `RULE_8_3`). See [Appendix: Validation Rules](../appendix/validation-rules.md) for the full catalog. |
| `Models/ValidationIssue`, `ValidationReport`, `ValidationSeverity`, `ValidationInput` (+ sub-types) | Immutable POCOs passed between the adapters and the rules. |
| `Helpers/CliToolPlaceholderExtractor` | Extracts user-facing placeholder keys from a CLI tool's `ArgumentTemplate` (excludes reserved `{FilePath}`). |
| `Helpers/ExitCodeExpressionValidator` | Sole source of truth for parsing `SuccessExitCodes` expressions. `ExitCodeMatcher.TryParse` in `Core` delegates here. |

**Engine glue** (`Raycoon.RayMigrator.Core/Configuration/Validation/`):

| Class | Purpose |
|-------|---------|
| `RayMigratorOptionsValidator` | `IValidateOptions<RayMigratorOptions>` — wires the engine into the shared catalog. Delegates to `OptionsValidationInputAdapter` to flatten `RayMigratorOptions` into `ValidationInput` (connection strings taken from the section as written, so `RULE_7_3` sees placeholders instead of resolved credentials, #30), runs `RuleCatalog.RunAll`, emits warnings via static `Serilog.Log`, and fails `ValidateOptionsResult` on any error-severity issues. Registered in DI and triggered via `.ValidateOnStart()` in `DirectModePipeline`. The public static `GetReport(options, sectionAsWritten)` returns the full report (warnings and errors with code, severity and path) for hosts that show findings instead of failing. |
| `OptionsValidationInputAdapter` | Static mapper `RayMigratorOptions` → `ValidationInput`. Assumes defaults have already been merged by `ProductDefaultsPostConfigureOptions`. |
| `ProductDefaultsPostConfigureOptions` | `IPostConfigureOptions<RayMigratorOptions>` — merges `ProductDefaults` into each `Product`, `TargetGroup`, and `Target`. Runs before `RayMigratorOptionsValidator`. Delegates to the public static `MergeDefaults(RayMigratorOptions)` method. |
| `SchemaNameValidator` | Static pipeline-level check that validates `SchemaName` against `DalSpecificProperties.SupportsSchema` after DAL discovery. Complements (does not overlap) the catalog's structural `RULE_4_1` / `RULE_4_2` by bringing in DAL-specific knowledge. |
| `ValidationContextPropertySetter` | Static helper for setting property values via `ValidationContext` during DataAnnotations validation. Used by custom attributes (e.g., `RayRangeIntAttribute`, `RayDirectoryExistsAttribute`) to apply default values. |

**Custom Validation Attributes** (`Raycoon.RayMigrator.Core/Configuration/Validation/RayAttributes/`):

| Attribute | Purpose |
|-----------|---------|
| `RayDirectoryExistsAttribute` | Validates that a directory path exists on disk |
| `RayConnectionStringAttribute` | Validates connection string format |
| `RayEncodingAttribute` | Validates that an encoding name is recognized by .NET |
| `RayRangeIntAttribute` | Validates that an integer value falls within a specified range |
| `RayEnumAttribute` | Validates that a string value matches a valid enum member |

### Environment Variable Replacement

**Location**: `Raycoon.RayMigrator.Core/Configuration/Replacer/`

| Class | Purpose |
|-------|---------|
| `EnvironmentVariableReplacer` | Static class that scans `IConfigurationSection` values for `{ENV:VariableName}` placeholders and replaces them with the corresponding environment variable values. Returns a list of `EnvironmentVariableWithMetadata` for logging and validation. |
| `EnvironmentVariableWithMetadata` | Tracks a single environment variable replacement: `Path`, `ConfigurationKey`, `ConfigurationValue`, `ConfigurationValueReplaced`, `EnvironmentVariableName`, and `EnvironmentVariableValue`. |

### Configuration Utilities

| Class | Location | Purpose |
|-------|----------|---------|
| `ConfigurationConstants` | `Core/Configuration/ConfigurationConstants.cs` | Static constants and compiled regexes for `{ENV:...}` and `{CFG:...}` placeholder syntax, allowed template variable names, sensitive data masking string, `DotNetEnvironmentVariableName`, and `DatabaseAccessLayersRootDirectory`. |
| `ConfigurationHelper` | `Core/Configuration/ConfigurationHelper.cs` | Class with static `GetTemplateTypes()` method returning all `TemplateType` enum values (excluding `Undefined`). |
| `StringExtensions` | `Core/Extensions/StringExtensions.cs` | Extension methods for SHA-256 hashing, path parsing, and connection string utilities. |
| `RayMigratorOptionsExtensions` | `Core/Extensions/RayMigratorOptionsExtensions.cs` | Extension method (`ToDetailString`) on `IConfigurationSection` for formatted configuration output with optional sensitive data masking. |
| `MigrationRunModeExtensions` | `Core/Extensions/MigrationRunModeExtensions.cs` | Extension methods for the `MigrationRunMode` enum. |
| `EnumTypeExtensions` | `Core/Extensions/EnumTypeExtensions.cs` | Enum helper extensions. |
| `ExceptionExtensions` | `Core/Extensions/ExceptionExtensions.cs` | Exception formatting extensions (e.g., `GetExceptionDetails()`). |

## Infrastructure Layer Components

### `DatabaseLogWriter`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/DatabaseLogWriter.cs`

**Purpose**: Writes log entries to a database table. Registered as Singleton in DI via factory. `InitDatabaseLoggerAsync` runs `DatabaseLogging_CheckCreate` once at startup, which creates the log tables and the two indexes of `MigrationLog` where they are missing.

### `DatabaseLoggerQueue`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/DatabaseLoggerQueue.cs`

**Purpose**: Background queue for asynchronous database log writing using `BlockingCollection<Action>`. Supports deterministic shutdown via `Flush()` which signals completion and waits for all pending entries to be processed.

### `RayMigratorDatabaseSink`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/RayMigratorDatabaseSink.cs`

**Purpose**: Custom Serilog sink that buffers log events for database writing.

### `MigrationContextEnricher`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/MigrationContextEnricher.cs` (namespace: `Raycoon.RayMigrator.Infrastructure.Logging`)

**Purpose**: Serilog enricher that reads `MigrationLoggingContext.Current` and adds migration-specific properties to every log event. Console/file properties: `Environment`, `MigrationRunId`, `TargetGroupAlias`, `TargetAlias`, `MigrationFilename`, `MigrationFileId`, `MigrationBlockId`. Database sink properties: `RunModeId`, `ProductId`, `EnvironmentId`, `MigrationRecordId`, `ReleaseVersion`, `FileName`, `FileOrderId`, `FileBlockId`.

### `ConnectionValidator`
**Location**: `Raycoon.RayMigrator.Infrastructure/ConnectionValidator.cs` (namespace: `Raycoon.RayMigrator.Core.Configuration.Validation`)

**Purpose**: Static class that validates database connections (repository, target databases, database logger) during startup. Post-validation after `IValidateOptions<RayMigratorOptions>` has been performed.

### `RepositoryExtensions`
**Location**: `Raycoon.RayMigrator.Infrastructure/RepositoryExtensions.cs` (namespace: `Raycoon.RayMigrator.Core.Extensions`)

**Purpose**: Extension methods for `RepositoryOptions`, e.g. `GetDalSettings()` to create `DalSettings` from repository configuration.

### `TemplateExecutor`
**Location**: `Raycoon.RayMigrator.Infrastructure/TemplateExecutor.cs` (namespace: `Raycoon.RayMigrator.Core`)

**Purpose**: Execute SQL templates with placeholder substitution.

Every public method is **asynchronous** (`…Async`): it returns a `Task`, takes `CancellationToken cancellationToken = default` as its last parameter (left out below) and awaits the DAL. A call that fails after the token was cancelled ends with the DAL's `OperationCanceledException` instead of a `TemplateExecutionException`. `MigrationService` passes the token of a run to the reading templates and to the record updates of an atomic migrate-up, which are part of the file's transaction and roll back with it as a whole; its repository writes on a connection of their own take no token, so a cancel never separates a record from its history row.

**Key Methods**:
```csharp
Task RepositoryCheckCreateAsync()   // schema steps through RepositorySchemaExecutor with the values of the current context
Task RepositoryProductCheckInsertAsync()
Task RepositoryEnvironmentCheckInsertAsync()
Task<bool> RepositoryProductSelectAsync()
Task<bool> RepositoryEnvironmentSelectAsync()
Task RepositoryMigrationRunInsertAsync(string migrationRunSettingsJson)
Task RepositoryMigrationRunUpdateAsync(MigrationRunResult runResult, string? errorCode = null, string? errorMessage = null)
Task<bool> RepositoryMigrationRunHeartbeatAsync()
Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectOrphanedAsync(int productId, int environmentId)
Task<bool> RepositoryMigrationRunFixOrphanedAsync(int migrationRunId, int thresholdMinutes)
Task<int> RepositoryMigrationRecordFixOrphanedAsync(int migrationRunId, MigrationStatus? status)
Task<InterruptedMigrationInfo?> RepositoryMigrationGetInterruptedAsync()
Task<int> RepositoryMigrationInsertAsync(int existingMigrationRecordId, string filename, string releaseVersion,
    string targetGroupAlias, string targetAlias, int fileOrderId, string fileUpHash,
    string? fileUpConfigHash, string fileUpBlocksHash, int fileUpBlocksTotal,
    string? fileUpConfigJson, bool migrateDownFileExists)
Task RepositoryMigrationUpdateAsync(int migrationRecordId, MigrationStatus migrationStatus, int fileUpBlocksMigrated)
// Shared-connection atomic overload (used by ExecuteSqlBlocksAtomic):
Task RepositoryMigrationUpdateAsync(int migrationRecordId, MigrationStatus migrationStatus, int fileUpBlocksMigrated,
    DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds)
Task RepositoryMigrationUpdateRollbackAsync(int migrationRecordId, MigrationStatus migrationStatus, string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash, int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson)
// Shared-connection atomic overload (used by ExecuteRollbackBlocksAtomic):
Task RepositoryMigrationUpdateRollbackAsync(int migrationRecordId, MigrationStatus migrationStatus, string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash, int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson, DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds)
Task RepositoryMigrationUpdateHashAsync(int migrationRecordId, string fileUpHash, string? fileUpConfigHash, string fileUpBlocksHash)
Task<List<MigrationRecord>> RepositoryMigrationSelectAsync()   // always reads Migrate-mode records, independent of the command's run mode
Task<List<MigrationRecord>> RepositoryMigrationRecordHistorySelectAsync()   // the run history of info, the only reader of MigrationRecordHistory besides the retention counts
Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectAsync(int limit)
Task<TemplateResponse> ExecuteScalarWithNegativeResultCodeExceptionAsync(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null)
internal static Task<TemplateResponse> ExecuteScalarAsync(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList)   // no context, no logging; shared with the schema steps
internal static Task<object?> ExecuteScalarValueAsync(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList)   // the raw scalar, for the retention counts
```

**Constructor**: Takes `TemplateCache`, `ILogger<TemplateExecutor>`, `IMigrationContextAccessor`. Context access is **deferred** to first use (not constructor time). The `_repository` and `_repositoryDal` fields are lazily initialized via `InitializeFromContext()` on first access.

**Placeholder Resolution**:
1. `{ENV:VariableName}` → Replaced during `TemplateCache.Initialize()` (at startup); missing variables throw `ConfigurationValidationException`
2. `{CFG:PropertyName}` → Replaced per `GetRepositoryTemplate<T>()` / `GetTemplate<T>()` call; unreplaced placeholders throw `ConfigurationValidationException`
3. `@Parameter` → SQL parameter via `DalParameterList`

### `RepositorySchemaExecutor`
**Location**: `Raycoon.RayMigrator.Infrastructure/RepositorySchemaExecutor.cs` (namespace: `Raycoon.RayMigrator.Core`)

**Purpose**: The schema steps of one repository (ADR-024) without a migration context: `Repository_CheckCreate`, every missing `Repository_Upgrade_{n}` up to the target version and `Repository_MigratorMeta_CheckInsert`. `EnsureAsync(cancellationToken)` returns `RepositorySchemaOutcome(VersionBefore, VersionAfter, MigratorMetaId)`; `CheckVersionAsync(cancellationToken)` reads the version through `Repository_CheckCreate` and applies no step (it creates the empty schema version table, on SQL Server and PostgreSQL also the schema, when it is missing). Both refuse a repository without a schema version but with tables, or with a newer version, with `RepositorySchemaException`. The constructor takes the `TemplateCache`, a logger, the `RepositoryOptions`, the repository DAL, the engine version and the target version. `TemplateExecutor.RepositoryCheckCreateAsync` runs it with the values of the current context, `RepositorySchemaService` and `RepositoryMaintenanceService` with the repository options alone; it executes through `TemplateExecutor.ExecuteScalarAsync`.

### `RepositoryMaintenanceExecutor`
**Location**: `Raycoon.RayMigrator.Infrastructure/RepositoryMaintenanceExecutor.cs` (namespace: `Raycoon.RayMigrator.Core`)

**Purpose**: The retention templates of one repository (ADR-027) without a migration context: `DeleteHistoryAsync` and `DeleteLogAsync` delete one batch of rows older than a cutoff through `Repository_MigrationRecordHistory_Delete` and `DatabaseLogging_Delete`, `CountHistoryAsync` and `CountLogAsync` count the same rows through the `_Count` templates (`CountLogAsync` returns null when the repository database has no log table under the schema and prefix of the repository). Every template spares the rows of an open run; the cutoff goes to the templates as UTC text with whole seconds (`CutoffFormat`). `RepositoryMaintenanceService` runs the batches.

### `TemplateCache`
**Location**: `Raycoon.RayMigrator.Infrastructure/TemplateCache.cs` (namespace: `Raycoon.RayMigrator.Core.Templates`)

**Purpose**: Cache loaded SQL templates for performance. Loads all templates from `DataAccessLayers/{Type}/` at startup, resolves `{ENV:*}` placeholders, and provides typed accessors that resolve `{CFG:*}` placeholders via reflection-based property matching.

**Template Location**:
```csharp
Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataAccessLayers")
```

**Public Methods**:
```csharp
// Called by the host for every configuration it runs (the CLI pipeline once, after the options are bound)
public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options)

// Returns all loaded DAL type names (e.g. "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite")
public List<string> GetAvailableDatabaseTypes()

// Get a template with {CFG:*} placeholders replaced via reflection on propertyClass
public Template GetTemplate<T>(string databaseType, TemplateType templateType, T propertyClass)

// Get a repository template; derives databaseType from propertyClass.DatabaseType
public Template GetRepositoryTemplate<T>(TemplateType templateType, T propertyClass) where T : RepositoryOptions

// Get a template's content string with {CFG:*} placeholders replaced
public string GetTemplateContent<T>(string databaseType, TemplateType templateType, T propertyClass)
```

## Database Layer Components

### Database.Common (NuGet Package)

**Location**: `Raycoon.RayMigrator.Database.Common/`

Public API classes shipped as a NuGet package for external DAL development:

| Class | Purpose |
|-------|---------|
| `IDal` | Interface defining all database operations (execute, reader, scalar, connection validation, parameter conversion) |
| `IDalSettings` | Interface for database-specific settings (`UseTransaction`, `DbCommandTimeoutInSeconds`, `MaxRetries`, `RetryDelayMs`) |
| `DalBase` | Abstract base class implementing `IDal` with virtual `IsTransient(Exception ex)` for DAL-specific transient error detection (never true for a cancellation), virtual `IsTimeout(Exception ex)`, which tells a command timeout from other failures (a `TimeoutException` in the chain, overridden for SqlClient error -2 and the `CommandTimeoutExpired` of MySqlConnector; the log initialization does not retry a timeout), protected `ExecuteWithRetryAsync`/`ExecuteWithRetry` helpers that invoke `IsTransient` via `RetryHelper`, virtual `TryGetDbSpecificSqlParameter<T>()`, and non-virtual `TryGetDbTypeForType()`. It implements the asynchronous `IDal` members itself on top of the protected members a DAL overrides (`ExecuteNonQueryCoreAsync`, `ExecuteScalarCoreAsync`, each also for a shared connection, `ExecuteReaderCoreAsync`, `IsConnectionValidCoreAsync`) and turns whatever a call throws after the cancellation into an `OperationCanceledException` that carries the caller's token and, unless the provider reported a cancellation itself, names the provider's message |
| `DalSettings` | Default `IDalSettings` implementation |
| `DalParameter` | Represents a single SQL parameter with name, value, and type |
| `DalParameterList` | Collection of `DalParameter` instances with `AddParameter()` and `GetAllParameters()` methods |
| `DalSpecificProperties` | Database-specific properties (fields: `SqlBlockDelimiter`, `SqlMultiLineCommentStart`, `SqlMultiLineCommentEnd`, `SupportsSchema`, `SupportsTransactionalDdl`, `IdentifierQuoteStart`, `IdentifierQuoteEnd`, `DefaultSchema`, `FoldsUnquotedIdentifiersToLower`) |
| `DatabaseTypeAttribute` | `[DatabaseType("...")]` attribute for reflection-based DAL discovery by `DalFactory` |
| `RetryHelper` | Static retry logic that accepts a transient error predicate (`Func<Exception, (bool isTransient, string? errorCode)>`). Provides `ExecuteWithRetryAsync<T>`, `ExecuteWithRetryAsync` (void), and `ExecuteWithRetry<T>` (synchronous) overloads with linear backoff. Transient error detection is delegated to each DAL via `DalBase.IsTransient`. The asynchronous overloads take the caller's token, wait for a retry with it, and neither classify nor retry a failure after the cancellation. |
| `RetryExhaustedException` | Exception thrown when all retry attempts are exhausted. Contains `AttemptsMade` and `LastErrorCode` properties |

### `IDal` Interface
**Location**: `Raycoon.RayMigrator.Database.Common/IDal.cs`

**Interface Definition**:
```csharp
public interface IDal
{
    string DatabaseType { get; }
    DalSpecificProperties DalSpecificProperties { get; }

    Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
    void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);   // no token: the database log writer calls it from its own thread
    Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
    Task<bool> IsConnectionValidAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken = default);
    void CheckConnectionStringOrValidateConnection(bool validateConnection);
    bool TryGetDbTypeForType(Type type, out DbType dbType);
    bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new();
    // Shared-connection methods for atomic operations (repository + target in one transaction):
    DbConnection CreateConnection();
    Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
    Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
}
```

A call that fails after its token was cancelled throws an `OperationCanceledException` that carries the token, whatever the provider reported, and starts no further retry; the provider's exception is its inner exception, and unless the provider reported a cancellation itself, its message names the provider's message. When a call on a connection of its own fails, times out or is cancelled, the MySQL and MariaDB DALs release the `GET_LOCK()` locks of its session (`DO RELEASE_ALL_LOCKS()`, best effort) before the connection goes back to the pool. The shipped DALs pass the token to opening the connection and to the command, not to commit and rollback; the SQLite DAL observes it before a non-query or scalar batch starts, and a read also between its rows, because Microsoft.Data.Sqlite cannot interrupt a statement.

### `DalFactory`
**Location**: `Raycoon.RayMigrator.Database/DalFactory.cs`

Static factory with dual-mode auto-discovery. Not registered in DI — called directly via `DalFactory.TryGetDal(databaseType, connectionString, out IDal? dal)`.

**Discovery modes**:
1. **DependencyContext**: Scans `DependencyContext.Default` (from deps.json) for runtime libraries whose names start with `Raycoon.RayMigrator.`, loads them via `Assembly.Load`, and discovers classes implementing `IDal` with `[DatabaseType]` attributes. Works with single-file publish.
2. **Filesystem**: Scans `DataAccessLayers/` subdirectories for DLLs containing `IDal` implementations with `[DatabaseType]` attributes (for external DAL plugins)

An assembly whose types do not load is skipped and its loader exceptions are kept; the `ConfigurationValidationException` for an unknown database type names these assemblies and says that a DAL built against an earlier version of RayMigrator must be rebuilt against this version.

### Database Implementations

Each DAL is a separate project (not a subdirectory of the Database project):

| Database | Project | Implementation | Attribute |
|----------|---------|---------------|-----------|
| SQL Server | `Raycoon.RayMigrator.Database.SqlServer` | `DalSqlServer.cs` | `[DatabaseType("SqlServer")]` |
| PostgreSQL | `Raycoon.RayMigrator.Database.PostgreSQL` | `DalPostgreSql.cs` | `[DatabaseType("PostgreSQL")]` |
| MariaDB | `Raycoon.RayMigrator.Database.MariaDb` | `DalMariaDb.cs` | `[DatabaseType("MariaDb")]` |
| MySQL | `Raycoon.RayMigrator.Database.MySql` | `DalMySql.cs` | `[DatabaseType("MySql")]` |
| SQLite | `Raycoon.RayMigrator.Database.Sqlite` | `DalSqlite.cs` | `[DatabaseType("Sqlite")]` |

### Database Example Project

**Location**: `Raycoon.RayMigrator.Database.Example/`

**Purpose**: Skeleton template project for external DAL development. Contains `DalExample.cs` and a placeholder for each of the 30 templates as a starting point for implementing new database providers. Transient error detection is provided via an `IsTransient()` override in `DalExample.cs`.

### SQL Templates

**Source Location**: Each DAL project's `Templates/` directory (e.g., `Raycoon.RayMigrator.Database.SqlServer/Templates/`). At build time, templates are copied to the output directory under `DataAccessLayers/{DatabaseType}/` (flat layout, no `Templates/` subdirectory at runtime).

30 templates per DAL, matching all non-Undefined `TemplateType` enum values. `TemplateCache` refuses a DAL folder that lacks one of them. See [Template System](../03-database-layer/template-system.md) for the complete list.

## Core Layer: Enumerations and Constants

**Location**: `Raycoon.RayMigrator.Core/Configuration/Enums/`

| Enum | Values |
|------|--------|
| `MigrationCommand` | None (0), MigrateUp (1), MigrateDown (2), ValidateHash (3), UpdateHash (4), Info (5), Baseline (6), FixIssues (7) |
| `MigrationRunMode` | Undefined (0), Validate (10), Simulate (20), Migrate (100) |
| `MigrationRunSource` | Undefined (0), Cli (10), Ci (20), Studio (30) |
| `MigrationOperation` | Undefined (0), Rollback (5), MigrateDown (50), MigrateUp (100), Baseline (110) |
| `MigrationRunResult` | Undefined (0), Running (10), PartialSuccess (50), Cancelled (70), Recovered (80), Error (90), Ok (100) |
| `MigrationCancelledAction` | Undefined (0), Terminate (10), RollbackRun (20), RollbackFile (21), RollbackRelease (22): what a cancelled migrate-up does after it stopped; a value of the run (`RunCancellation.Cancel`, `--cancelled-action`), not a configuration option |
| `MigrationStatus` | Undefined (0), Pending (10), Executing (20), Failed (30), NotMigrated (50), Migrated (100) |
| `MigrationErrorAction` | Undefined (0), Terminate (10), Rollback (20), RollbackErrorOnly (21), RollbackRelease (22), Ignore (30) |
| `RollbackErrorAction` | Undefined (0), Terminate (10), Ignore (30) |
| `TargetMigrationOrder` | Undefined (0), FileByFile (1), TargetByTarget (2) |
| `HashValidationScope` | Undefined (0), File (1), SqlBlocks (2), Disabled (3) |
| `FixScope` | Undefined (0), All (1), OrphanedRuns (2) |
| `CliToolInputMode` | Undefined (0), File (1), Stdin (2) |

> **Note**: `MigrationEvent` is a static **class** in `Raycoon.RayMigrator.Core/Logging/` (namespace `Raycoon.RayMigrator.Core.Logging`), `CommandProfile` a record struct in `Raycoon.RayMigrator.Core/Configuration/`; neither lives in `Configuration/Enums/` any more (#19). `MigrationEvent` defines static `EventId` constants used for structured logging (e.g., `CommandLineParsing` = 10, `TemplateExecutionRepositoryCheckCreate` = 100, `RayMigratorServiceShutdown` = 1000).

**Location**: `Raycoon.RayMigrator.Core/Templates/`

| Enum | Values |
|------|--------|
| `TemplateType` | Undefined (0), Repository_Product_CheckInsert, Repository_Environment_CheckInsert, DatabaseLogging_CheckCreate, DatabaseLogging_Insert, DatabaseLogging_Delete, DatabaseLogging_Count, Repository_CheckCreate, Repository_Drop, Repository_Upgrade_1, Repository_Upgrade_2, Repository_Upgrade_3, Repository_MigratorMeta_CheckInsert, Repository_MigrationRun_Insert, Repository_MigrationRun_Update, Repository_MigrationRun_Select, Repository_MigrationRun_SelectOrphaned, Repository_MigrationRun_FixOrphaned, Repository_MigrationRun_Heartbeat, Repository_MigrationRecord_Insert, Repository_MigrationRecord_Update, Repository_MigrationRecord_UpdateHash, Repository_MigrationRecord_UpdateRollback, Repository_MigrationRecord_Select, Repository_MigrationRecord_GetInterrupted, Repository_MigrationRecord_FixOrphaned, Repository_MigrationRecordHistory_Select, Repository_MigrationRecordHistory_Delete, Repository_MigrationRecordHistory_Count, Repository_Product_Select, Repository_Environment_Select (declaration order; one `Repository_Upgrade_{n}` per schema version up to `RepositorySchema.CurrentVersion`, which is 3) |

## Read Model Package Components

The package `Raycoon.RayMigrator.Repository.ReadModel` (ADR-025) is the contract for readers of the repository that do not use the engine's templates, such as RayMigrator Studio. It depends on Core only; no EF Core, no database driver.

### `RepositoryNaming`

**Purpose**: The table and column names of one repository on one engine, from `DatabaseType`, `SchemaName` and `TableBaseName`: `TableName`, `ColumnName`, `QualifiedTableName`, `QualifiedColumnName`, `Quote` and `SelectStatement`. PascalCase on SQL Server and SQLite, snake_case on PostgreSQL, MySQL and MariaDB (`ToSnakeCase`, with the `RayMigrator` product-name exception); schema only on SQL Server and PostgreSQL; quoting as the templates write it.

### Row records

**Purpose**: One positional record per table (`RepositorySchemaVersionRow`, `MigratorMetaRow`, `ProductRow`, `EnvironmentRow`, `MigrationRunRow`, `MigrationRunMetaRow`, `MigrationRecordRow`, `MigrationRecordHistoryRow`, `LookupRow` for the five lookups, `MigrationEventRow`, `MigrationLogRow`). The constructor parameters are the columns in PascalCase in table order; lookup ids are typed as their enums, timestamps as UTC `DateTime`.

### `RepositoryRows`

**Purpose**: The record type of a `RepositoryTable` and, derived from the record's constructor, its column list (`ColumnsOf`), so that statements and records cannot drift.

### `RepositoryRowMapper`

**Purpose**: Builds a record from one ADO.NET row given as column values by name: integers of any width, `tinyint`/`sbyte`/`short` lookup ids to enums, `bit`/`0`/`1` to bool, SQLite text and kind-less timestamps to UTC, `DBNull` to null. A missing column or a null for a value type is reported with the record and column name.

### `RepositoryReadModel`

**Purpose**: `SchemaVersion` (= `RepositorySchema.CurrentVersion` of the engine release) and the list of `Tables`. A reader compares the version with `MAX(Version)` of `RepositorySchemaVersion` before trusting the names.

## Shared Layer Components

### `AssemblyInfoHelper`
**Location**: `Raycoon.RayMigrator.Shared/AssemblyInfoHelper.cs`

**Purpose**: Provides assembly version information shared across projects. Key methods: `GetRayMigratorVersion()` (the InformationalVersion of the engine assembly, never the entry assembly, so a host such as RayMigrator Studio reports the engine version), `FormatVersion(string)` (bare version for release builds, `<version>-dev+<7-char hash>` for builds outside the release workflows), `GetAsciiLogoLines(string version)`, `GetAsciiHeader()`.

> **Note**: The Console project has its own local `AssemblyInfoHelper` class (`Raycoon.RayMigrator.Console/AssemblyInfoHelper.cs`) with `GetAssemblyInfo()` and `GetRayMigratorVersion()` methods that delegate to `Shared.AssemblyInfoHelper`. This is used for startup banner display.

### Configuration help
**Location**: `Raycoon.RayMigrator.Shared/Configuration/Help/` (namespace `Raycoon.RayMigrator.Shared.Configuration.Help`)

**Purpose**: The help texts of the configuration in English and German, read by the Config Wizard and RayMigrator Studio. `ConfigurationHelp` returns `SectionHelp` and `FieldHelp` records from the `SectionHelp*.resx` and `FieldHelp*.resx` resources (field keys like `Repository_DatabaseType`); `JsonPathRegistry` maps a field key to its configuration path (`JsonPathInfo`, with the paths it inherits from and passes on) and back (`TryGetFieldKey`).

### Constants
**Location**: `Raycoon.RayMigrator.Shared/Constants/`

| Class | Purpose |
|-------|---------|
| `InternalConstants` | Internal string constants (e.g., `RayMigratorSectionName`) |
| `TemplateResultCode` | Catalog of negative result codes returned by SQL templates and of the positive engine error codes (1001 to 1005, among them `Cancelled` 1004 and `CancelledRollbackIncomplete` 1005) |

### Exceptions
**Location**: `Raycoon.RayMigrator.Shared/Exceptions/CustomExceptions.cs`

| Exception | Category | Purpose |
|-----------|----------|---------|
| `ApplicationStartupException` | Pre-Migration | Startup/initialization failures |
| `ConfigurationValidationException` | Pre-Migration | Configuration parsing/validation errors |
| `TemplateExecutionException` | Template | SQL template execution errors |
| `TemplateResultException` | Template | Negative result codes from template execution |
| `UndefinedTemplateResultException` | Template | Unknown negative result codes from customized templates (extends `TemplateResultException`) |
| `MigrationHashValidationException` | Migration-Run | Hash mismatch on migration files |
| `MigrationFileParsingException` | Migration-Run | TOML metadata or file parsing errors |
| `MigrationExecutionException` | Migration-Run | SQL execution errors during migration |
| `CliToolExecutionException` | Migration-Run | External CLI tool execution errors (extends `MigrationExecutionException`). Properties: `ExecutablePath`, `ExitCode` |
| `CliToolTimeoutException` | Migration-Run | CLI tool timeout (extends `CliToolExecutionException`). Property: `TimeoutSeconds` |
| `RayMigratorInternalException` | Migration-Run | Unexpected internal errors |
| `NotYetImplementedException` | Planned | Features not yet implemented |
| `DatabaseParameterException` | Database | Database parameter conversion failures |
| `MigrationAlreadyRunningException` | Database | Concurrent migration lock conflict |
| `MigrationRecoveryException` | Database | Errors during recovery operations |
| `DatabaseTransientException` | Database | Transient DB errors after retry exhaustion |

## Related Documentation

- [Overview](overview.md) - High-level architecture
- [Data Flow](data-flow.md) - How components interact
- [Patterns](patterns.md) - Implementation patterns
