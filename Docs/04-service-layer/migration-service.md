# Migration Service

The `MigrationService` is the central orchestrator for all migration operations. See [activity-diagrams.md](activity-diagrams.md) for visual flowcharts of each command.

## Interface

**Location**: `Raycoon.RayMigrator.Services.Abstractions/IMigrationService.cs`

```csharp
public interface IMigrationService
{
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request);
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request);
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request);
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request);
    Task<BaselineResult> BaselineAsync(BaselineRequest request);
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias);
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100);
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request);
}
```

## Request Types

### MigrateUpRequest

```csharp
public class MigrateUpRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public bool AllowOutOfOrder { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
}
```

### MigrateDownRequest

```csharp
public class MigrateDownRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string TargetReleaseVersion { get; set; } = string.Empty;  // Required
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}
```

### ValidateHashRequest

```csharp
public class ValidateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public HashValidationScope? HashValidationScope { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}
```

### UpdateHashRequest

```csharp
public class UpdateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}
```

### BaselineRequest

`TargetReleaseVersion` is nullable. When `null` or empty, all releases are baselined.

```csharp
public class BaselineRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
}
```

### FixIssuesRequest {#fix-issues-request}

```csharp
public class FixIssuesRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public FixIssues Scope { get; set; } = FixIssues.OrphanedRuns;
    public int OlderThanMinutes { get; set; } = 60;
    public bool DryRun { get; set; } = false;
    public MigrationStatus AssumedMigrationStatus { get; set; } = MigrationStatus.NotMigrated;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
}
```

## Enums Used in Request and Response Types

### MigrationRunMode

Controls the execution behavior of a migration command.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Validate` | 10 | Validates configuration and files; does not connect to databases |
| `Simulate` | 20 | Validates, checks connectivity, reads repository; does not write repository records or execute SQL |
| `Migrate` | 100 | Full execution against target databases |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationRunMode.cs`

Extension methods on `MigrationRunMode` (defined in `MigrationRunModeExtensions.cs`):

| Method | Validate | Simulate | Migrate |
|--------|----------|----------|---------|
| `ShouldExecuteSql()` | false | false | true |
| `ShouldWriteRepository()` | false | false | true |
| `ShouldReadRepository()` | false | true | true |

### MigrationOperation

Identifies the type of operation being performed.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Rollback` | 5 | Performing rollback |
| `MigrateDown` | 50 | Performing down-migration |
| `MigrateUp` | 100 | Performing up-migration |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationOperation.cs`

### MigrationRunResult

Tracks the outcome of a migration operation.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Running` | 10 | Migration currently in progress |
| `Error` | 90 | Stopped due to error |
| `Ok` | 100 | Successfully completed |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationRunResult.cs`

### MigrationStatus

Tracks the deployment status of a migration file on a target database.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Pending` | 10 | Migration record created, execution pending |
| `Executing` | 20 | SQL blocks are being executed |
| `Failed` | 30 | Error occurred, DB state may be unclear |
| `NotMigrated` | 50 | Not deployed (new, rolled back, or skipped) |
| `Migrated` | 100 | Successfully deployed |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationStatus.cs`

### HashValidationScope

Controls which hash is compared during validation.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `File` | 1 | Compare full file hash (`FileUpHash`) |
| `SqlBlocks` | 2 | Compare SQL blocks hash only (`FileUpBlocksHash`) |
| `Disabled` | 3 | Skip hash validation |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/HashValidationScope.cs`

### FixIssues

Scope of the Fix command.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `All` | 1 | Fixes all known repository problems |
| `OrphanedRuns` | 2 | Fixes orphaned MigrationRun entries (process crashed while Running) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/FixIssues.cs`

### MigrationErrorAction

Defines the behavior when a migration file encounters an error. Values: `Terminate` (10), `Rollback` (20), `RollbackErrorOnly` (21), `RollbackRelease` (22), `Ignore` (30). See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior descriptions and [Enum Reference](../08-cli-reference/command-reference.md#enum-reference) for the complete enum table.

### RollbackErrorAction

Defines the behavior when a rollback operation encounters an error. Since a failed rollback cannot itself be rolled back, only `Terminate` (10, default) and `Ignore` (30) are meaningful. See [Error Handling — Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling) for details.

### CliToolInputMode

Controls how the migration SQL file is passed to an external CLI tool.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not explicitly set; falls back to `File` behavior at runtime |
| `File` | 1 | File path passed as a command-line argument via `{FilePath}` in `ArgumentTemplate`. Used by tools like `sqlcmd -i`, `psql -f`, `sqlite3 -init` |
| `Stdin` | 2 | File content piped to the process via standard input. Used by tools like `mysql` and `mariadb` |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/CliToolInputMode.cs`

Default when `InputMode` is null or empty in configuration: `File` (the string is parsed via `Enum.TryParse`; if parsing fails or the value is `Undefined`, the executor treats it as `File`).

## Response Types

### OperationResult (Base Class)

```csharp
public abstract class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }  // Negative = SQL template ResultCode, positive = C# backend ErrorCode, null = unclassified. See TemplateResultCode catalog.
    public List<string> Messages { get; set; } = new List<string>();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}
```

### MigrationOperationResult

Returned by `MigrateUpAsync` and `MigrateDownAsync`:

```csharp
public class MigrationOperationResult : OperationResult
{
    public Guid RunId { get; set; }
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? CurrentRelease { get; set; }
    public List<MigrationFileResult> MigrationResults { get; set; } = new List<MigrationFileResult>();
}
```

### ValidationResult

Returned by `ValidateHashAsync`:

```csharp
public class ValidationResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ValidFiles { get; set; }
    public int InvalidFiles { get; set; }
    public int MissingFiles { get; set; }
    public List<HashValidationIssue> Issues { get; set; } = new List<HashValidationIssue>();
}
```

### HashUpdateResult

Returned by `UpdateHashAsync`:

```csharp
public class HashUpdateResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public int UpdatedFiles { get; set; }
    public int NewFiles { get; set; }
    public int RemovedFiles { get; set; }
    public List<string> UpdatedFileNames { get; set; } = new List<string>();
}
```

### BaselineResult

Returned by `BaselineAsync`. `TargetReleaseVersion` is nullable — `null` when all releases were baselined.

```csharp
public class BaselineResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public int BaselinedFiles { get; set; }
}
```

### MigrationStatusInfo

Returned by `GetStatusAsync`:

```csharp
public class MigrationStatusInfo
{
    public string ProductAlias { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public DateTime? LastMigrationDate { get; set; }
    public int TotalMigrationsExecuted { get; set; }
    public int PendingMigrations { get; set; }
    public MigrationRunResult? LastRunResult { get; set; }
    public Dictionary<string, TargetGroupStatus> TargetGroups { get; set; } = new Dictionary<string, TargetGroupStatus>();
}
```

### TargetGroupStatus

Per-target-group status within `MigrationStatusInfo`:

```csharp
public class TargetGroupStatus
{
    public string Alias { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public int ExecutedMigrations { get; set; }
    public DateTime? LastMigrationDate { get; set; }
    public List<string> Targets { get; set; } = new List<string>();
}
```

### MigrationHistory

Returned by `GetHistoryAsync`:

```csharp
public class MigrationHistory
{
    public string ProductAlias { get; set; } = string.Empty;
    public List<MigrationRunInfo> Runs { get; set; } = new List<MigrationRunInfo>();
}
```

### MigrationRunInfo

Individual run entry within `MigrationHistory`:

```csharp
public class MigrationRunInfo
{
    public int MigrationRunId { get; set; }
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public MigrationRunMode RunMode { get; set; }
    public string? InitiatedBy { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? ToRelease { get; set; }
}
```

### FixIssuesResult

Returned by `FixIssuesAsync`:

```csharp
public class FixIssuesResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool WasDryRun { get; set; }
    public int OrphanedRunsFound { get; set; }
    public int OrphanedRunsFixed { get; set; }
    public List<OrphanedRunInfo> OrphanedRuns { get; set; } = new();
}
```

### OrphanedRunInfo

Individual orphaned run entry within `FixIssuesResult`:

```csharp
public class OrphanedRunInfo
{
    public int MigrationRunId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public int EnvironmentId { get; set; }
    public DateTime StartedAt { get; set; }
    public double MinutesRunning { get; set; }
    public int MigrationRunModeId { get; set; }
    public bool WasFixed { get; set; }
}
```

### MigrationFileResult

Result of a single migration file execution, used within `MigrationOperationResult.MigrationResults`:

```csharp
public class MigrationFileResult
{
    public string FileName { get; set; } = string.Empty;
    public string ReleaseVersion { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
```

### HashValidationIssue

Individual validation issue within `ValidationResult.Issues`:

```csharp
public class HashValidationIssue
{
    public string FileName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty; // "Modified", "Missing", "New"
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
    public string Details { get; set; } = string.Empty;
}
```

## Implementation

**Location**: `Raycoon.RayMigrator.Services/MigrationService.cs`

### Constructor Dependencies

```csharp
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
}
```

### Constants

| Constant | Type | Value | Description |
|----------|------|-------|-------------|
| `AutoFixOrphanedRunsThresholdMinutes` | `internal const int` | `10` | Minimum age (in minutes) for an orphaned MigrationRun to be eligible for auto-fix during `RepositoryMigrationRunInsertWithAutoFix` |

### MigrateUpAsync Flow

```mermaid
sequenceDiagram
    participant Caller
    participant Svc as MigrationService
    participant Tmpl as TemplateExecutor
    participant Repo as Repository
    participant Target as Target DB

    Caller->>Svc: MigrateUpAsync(request)

    Note over Svc: Phase 1: Initialization<br/>(state always set; repo ops only when ShouldWriteRepository)
    Svc->>Svc: Set MigrationRunResult=Running, MigrationOperation=MigrateUp
    Svc->>Tmpl: RepositoryCheckCreate()
    Tmpl->>Repo: Create/verify repository
    Repo-->>Tmpl: VersionId

    Svc->>Tmpl: RepositoryProductCheckInsert()
    Tmpl->>Repo: Ensure product exists
    Repo-->>Tmpl: ProductId

    Svc->>Tmpl: RepositoryEnvironmentCheckInsert()
    Tmpl->>Repo: Ensure environment exists
    Repo-->>Tmpl: EnvironmentId

    Svc->>Tmpl: RepositoryMigrationGetInterrupted()
    Svc->>Svc: BuildMigrationRunSettingsJson()
    Svc->>Svc: RepositoryMigrationRunInsertWithAutoFix(settingsJson)
    Note over Svc: Auto-fixes orphaned runs if parallel-run lock detected
    Svc->>Tmpl: RepositoryMigrationRunInsert(settingsJson)
    Tmpl->>Repo: Create migration run
    Repo-->>Tmpl: MigrationRunId

    Note over Svc: Phase 2: File Discovery & Preparation
    Svc->>Svc: DiscoverAndPrepareMigrationFiles()
    Svc->>Tmpl: RepositoryMigrationSelect(MigrationRunMode.Migrate)
    Tmpl->>Repo: Query existing records
    Svc->>Svc: FilterAlreadyMigratedFiles()
    Svc->>Svc: FilterByTargetRelease()
    Svc->>Svc: ValidateTargetGroupAliases()
    Svc->>Svc: FilterByTargetGroups()
    Svc->>Svc: DetectOutOfOrderFiles()
    Svc->>Svc: LogMigrationSafetyWarnings()

    Note over Svc: Phase 3: Execute Migrations
    loop For each release
        Note over Svc: ResolveTargetGroupMigrationOrder()<br/>(CLI > migsettings > appsettings > config order)
        loop For each TargetGroup (resolved order)
            Note over Svc: Dispatch by TargetMigrationOrder
            alt Simultaneously (file→target)
                Svc->>Svc: ExecuteTargetGroupSimultaneously()
                Note over Svc: Inside: TryFinalizeCompletedMigration()<br/>RepositoryMigrationInsert()<br/>ResolveUseCliToolAlias() → ExecuteWithCliTool() or ExecuteSqlBlocks()<br/>RepositoryMigrationUpdate(Migrated)
            else Successively (target→file)
                Svc->>Svc: ExecuteTargetGroupSuccessively()
                Note over Svc: Inside: TryFinalizeCompletedMigration()<br/>RepositoryMigrationInsert()<br/>ResolveUseCliToolAlias() → ExecuteWithCliTool() or ExecuteSqlBlocks()<br/>RepositoryMigrationUpdate(Migrated)
            end
        end
    end

    Note over Svc: Phase 5: Finalization
    Svc->>Tmpl: RepositoryMigrationRunUpdate(Ok)
    Svc-->>Caller: MigrationOperationResult
```

### MigrateUpAsync (Simplified)

The context is always accessed via `_ctxAccessor.Current` (not a direct `_ctx` field), which supports both CLI (singleton) and API (async-local per-request) hosting modes.

```csharp
public async Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request)
{
    // Phase 1: Initialization
    _ctxAccessor.Current.MigrationState.MigrationRunResult = MigrationRunResult.Running;
    _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;

    if (request.RunMode.ShouldWriteRepository())
    {
        _templateExecutor.RepositoryCheckCreate();
        _templateExecutor.RepositoryProductCheckInsert();
        _templateExecutor.RepositoryEnvironmentCheckInsert();
        var interruptedMigration = _templateExecutor.RepositoryMigrationGetInterrupted();
        if (interruptedMigration != null) _logger.LogWarning("Interrupted migration detected: ...", interruptedMigration.MigrationRecordId, ...);
        var settingsJson = BuildMigrationRunSettingsJson(_ctxAccessor.Current);
        await RepositoryMigrationRunInsertWithAutoFix(settingsJson);
    }

    // Phase 2: File Discovery & Preparation
    var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);
    List<MigrationFileInfo> filesToMigrate;
    List<MigrationRecord> existingRecords;
    if (request.RunMode.ShouldReadRepository())
    {
        // Always query with MigrationRunMode.Migrate to read actual Migrate-mode records,
        // even when running in Simulate mode (which no longer writes its own records).
        existingRecords = _templateExecutor.RepositoryMigrationSelect(MigrationRunMode.Migrate);
        filesToMigrate = FilterAlreadyMigratedFiles(migrationFiles, existingRecords, productOptions);
    }
    else
    {
        existingRecords = new List<MigrationRecord>();
        filesToMigrate = migrationFiles; // Validate: process all files
    }
    filesToMigrate = FilterByTargetRelease(filesToMigrate, request.TargetReleaseVersion);
    ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
    filesToMigrate = FilterByTargetGroups(filesToMigrate, request.TargetGroupAliases);
    DetectOutOfOrderFiles(filesToMigrate, existingRecords);

    // Log safety warnings for dangerous configuration combinations
    LogMigrationSafetyWarnings(filesToMigrate, productOptions);

    // Phase 3: Execute Migrations (Release → TargetGroup → Targets)
    var successfullyMigratedRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

    foreach (var release in orderedReleases)
    {
        foreach (var targetGroup in productOptions.TargetGroups!)
        {
            var result = targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously
                ? await ExecuteTargetGroupSimultaneously(tgFiles, targetGroup, ...)
                : await ExecuteTargetGroupSuccessively(tgFiles, targetGroup, ...);

            if (!result.Success)
            {
                await HandleMigrationError(productOptions, result.FailedFile!, result.FailedMigrationRecordId,
                    successfullyMigratedRecords);
                _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Error);
                return errorResult;
            }
        }
    }

    // Phase 5: Finalization
    _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok);
    return successResult;
}
```

### MigrateDownAsync Flow

MigrateDown supports two modes based on `RunMode`:

- **Validate mode** (`!RunMode.ShouldReadRepository()`): Validates rollback file existence and parseability without touching the repository. Returns warnings for missing rollback files.
- **Simulate/Migrate mode** (`RunMode.ShouldReadRepository()`): Queries migrated records from the repository and filters to releases after the target. In Migrate mode, executes rollback SQL in reverse `FileOrderId` order and writes updated repository records. In Simulate mode, reads the repository but does not execute SQL or write any records.

```csharp
public async Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request)
{
    // --- Validate mode: check rollback files exist and parse correctly ---
    if (!request.RunMode.ShouldReadRepository())
    {
        var filesToValidate = FilterReleasesAfterTarget(allFiles, request.TargetReleaseVersion!);
        ValidateTargetGroupAliases(request.TargetGroupAliases, productOpts.TargetGroups!);
        filesToValidate = FilterByTargetGroups(filesToValidate, request.TargetGroupAliases);
        // Validate each rollback file exists and can be parsed
        return validationResult;
    }

    // Early validation of target group aliases (before any repository operations)
    ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);

    // --- Phase 1: Initialization ---
    _templateExecutor.RepositoryCheckCreate();
    _templateExecutor.RepositoryProductCheckInsert();
    _templateExecutor.RepositoryEnvironmentCheckInsert();
    _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateDown;
    await RepositoryMigrationRunInsertWithAutoFix(settingsJson);

    // --- Phase 2: Query migrations for rollback ---
    var migrationsToRollback = existingRecords
        .Where(r => r.MigrationStatusId == MigrationStatus.Migrated
            || (r.MigrationStatusId == MigrationStatus.Failed
                && r.FileDownBlocksMigrated.HasValue
                && r.FileDownBlocksMigrated > 0
                && r.FileDownBlocksMigrated < r.FileDownBlocksTotal))
        .Where(r => string.Compare(r.ReleaseVersion, request.TargetReleaseVersion, StringComparison.OrdinalIgnoreCase) > 0)
        .Where(r => /* target group filter if specified */)
        .OrderByDescending(r => r.FileOrderId)
        .ToList();

    // --- Phase 3: Execute rollbacks ---
    var rollbackResult = await ExecuteRollbackForMigrations(migrationsToRollback, productOptions, request.RunMode);

    // --- Phase 4: Finalization ---
    _templateExecutor.RepositoryMigrationRunUpdate(rollbackResult.AllSuccessful ? MigrationRunResult.Ok : MigrationRunResult.Error);
    return operationResult;
}
```

### BaselineAsync Flow

Baseline marks migration files as migrated without executing their SQL. It follows the same Release-based TargetGroup dispatch as MigrateUp.

```csharp
public async Task<BaselineResult> BaselineAsync(BaselineRequest request)
{
    // --- Phase 1: Initialization ---
    _templateExecutor.RepositoryCheckCreate();
    _templateExecutor.RepositoryProductCheckInsert();
    _templateExecutor.RepositoryEnvironmentCheckInsert();
    _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;
    await RepositoryMigrationRunInsertWithAutoFix(settingsJson);

    // --- Phase 2: File Discovery ---
    var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);

    // --- Phase 3: Filter files ---
    var filesToBaseline = FilterByTargetRelease(migrationFiles, request.TargetReleaseVersion);
    ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
    filesToBaseline = FilterByTargetGroups(filesToBaseline, request.TargetGroupAliases);

    var existingRecords = _templateExecutor.RepositoryMigrationSelect();
    filesToBaseline = FilterAlreadyMigratedFiles(filesToBaseline, existingRecords, productOptions);

    // --- Phase 4: Record each file as migrated (Release → TargetGroup → Targets) ---
    foreach (var release in baselineOrderedReleases)
    {
        foreach (var targetGroup in productOptions.TargetGroups!)
        {
            if (targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously)
            {
                // File → Target order
                foreach (var file in tgFiles)
                    foreach (var target in targetGroup.Targets!)
                        await BaselineFile(file, target);
            }
            else
            {
                // Target → File order (Successively, default)
                foreach (var target in targetGroup.Targets!)
                    foreach (var file in tgFiles)
                        await BaselineFile(file, target);
            }
        }
    }

    // BaselineFile: Validate CLI alias + Insert + Update(Migrated) without executing SQL
    async Task BaselineFile(MigrationFileInfo file, TargetOptions targetOptions)
    {
        // Validate CLI tool alias if set (no execution, but ensures config is correct)
        string? cliAlias = ResolveUseCliToolAlias(file, targetOptions);
        if (cliAlias != null) GetCliToolByAlias(cliAlias); // Throws if alias not found

        int migrationRecordId = _templateExecutor.RepositoryMigrationInsert(...);
        _templateExecutor.RepositoryMigrationUpdate(migrationRecordId, MigrationStatus.Migrated, file.FileUpBlocksTotal);
    }

    // --- Phase 5: Finalization ---
    _templateExecutor.RepositoryMigrationRunUpdate(MigrationRunResult.Ok);
    return baselineResult;
}
```

### ValidateHashAsync Flow

Validates migration file integrity by comparing file hashes on disk against the repository.

1. **Phase 1**: Repository initialization (`RepositoryCheckCreate`, `RepositoryProductCheckInsert`, `RepositoryEnvironmentCheckInsert`)
2. **Phase 2**: File discovery and optional target group filtering
3. **Phase 3**: Query existing records (`RepositoryMigrationSelect`)
4. **Phase 4**: Compare files with repository records per `HashValidationScope` (File or SqlBlocks). Detects three issue types:
   - **New**: File on disk, not yet migrated
   - **Modified**: Hash mismatch between disk and repository
   - **Missing**: Record in repository but file no longer on disk
5. **Phase 5**: Build and return `ValidationResult`

### UpdateHashAsync Flow

Updates stored hashes in the repository to match current file contents (after approved migration file changes).

1. **Phase 1**: Repository initialization (`RepositoryCheckCreate`, `RepositoryProductCheckInsert`, `RepositoryEnvironmentCheckInsert`)
2. **Phase 2**: File discovery and optional target group filtering
3. **Phase 3**: Query existing records
4. **Phase 4**: For each file with a matching Migrated record, compare all three hashes (`FileUpHash`, `FileUpConfigHash`, `FileUpBlocksHash`). If any differ, call `RepositoryMigrationUpdateHash`. Also counts files missing from disk.
5. **Phase 5**: Return `HashUpdateResult` with updated/new/removed counts

### GetStatusAsync Flow

Returns the current migration status overview for a product (used by the `info` command).

1. **Phase 1**: Repository initialization (`RepositoryCheckCreate`, `RepositoryProductCheckInsert`, `RepositoryEnvironmentCheckInsert`)
2. **Phase 2**: Query existing migration records
3. **Phase 3**: File discovery (to count pending migrations)
4. **Phase 4**: Compute status metrics: current release, pending migrations, last run result, and per-target-group status (`TargetGroupStatus`)

Returns `MigrationStatusInfo` with product-level and target-group-level status.

### GetHistoryAsync Flow

Returns migration run history for a product.

1. **Phase 1**: Repository initialization (`RepositoryCheckCreate`, `RepositoryProductCheckInsert`, `RepositoryEnvironmentCheckInsert`)
2. **Phase 2**: Query `MigrationRun` records via `RepositoryMigrationRunSelect(limit)`
3. **Phase 3**: For each run, query migration records to compute totals (total, successful, failed). Detects `MigrateDown` operations from migration records.

Returns `MigrationHistory` with a list of `MigrationRunInfo` entries.

### FixIssuesAsync Flow

Fixes repository inconsistencies such as orphaned `MigrationRun` entries.

1. **Phase 1**: Repository initialization (`RepositoryCheckCreate`, `RepositoryProductCheckInsert`, `RepositoryEnvironmentCheckInsert`)
2. **Phase 2**: Query orphaned runs via `RepositoryMigrationRunSelectOrphaned(productId, environmentId)`
3. **Phase 3**: Filter by `OlderThanMinutes`
4. **Phase 4**: Log found orphans
5. **Phase 5**: If not `DryRun`, fix each orphaned run: fix orphaned MigrationRecord entries (`RepositoryMigrationRecordFixOrphaned` with `AssumedMigrationStatus`) then mark the run as Error (`RepositoryMigrationRunFixOrphaned`)
6. **Phase 6**: Return `FixIssuesResult`

## TargetGroup Execution

### ExecuteTargetGroupSimultaneously

**Visibility**: `internal` (for testability)

Executes migrations in **file-first** order: `foreach file -> foreach target`. This means each migration file is applied to all targets before moving to the next file.

An error aborts the entire TargetGroup unless `MigrationErrorAction` is `Ignore`, in which case the file is marked as Failed and execution continues to the next file. Within the Simultaneously loop, when block-level failures occur with Ignore, remaining targets for that file are skipped (`break`), and the next file is processed.

```csharp
internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupSimultaneously(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
    ProductOptions productOptions, MigrateUpRequest request,
    List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)> successfullyMigratedRecords,
    List<MigrationFileResult> migrationResults,
    List<MigrationRecord> existingRecords)
```

Each file/target execution:
1. Inserts a migration record (`RepositoryMigrationInsert`)
2. Sets `_ctxAccessor.Current.MigrationState` fields (MigrationRecordId, ReleaseVersionFromFileNameWithPath, FilenameWithRelativePath, FileOrderId, TargetGroupAlias, TargetAlias)
3. Checks for resumable partial execution (`FindResumableBlock`)
4. Resolves CLI tool alias via `ResolveUseCliToolAlias(file, targetOptions)`
5. If a CLI tool alias is resolved: executes via `ExecuteWithCliTool()` (entire file as single unit)
6. Otherwise: executes SQL blocks via `ExecuteSqlBlocks()` (block-wise DAL execution)
7. Updates migration record status (`RepositoryMigrationUpdate`)
8. Adds `(file, migrationRecordId, targetOptions.Alias!)` to `successfullyMigratedRecords`

### ExecuteTargetGroupSuccessively

**Visibility**: `internal` (for testability)

Executes migrations in **target-first** order: `foreach target -> foreach file`. This means all migration files are applied to one target before moving to the next target.

Error handling behavior is identical to Simultaneously: Ignore causes the file to be marked as Failed and continues to the next file; all other error actions abort the TargetGroup. Like Simultaneously, execution branches between CLI tool (`ResolveUseCliToolAlias` / `ExecuteWithCliTool`) and DAL (`ExecuteSqlBlocks`) per file/target.

```csharp
internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupSuccessively(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
    ProductOptions productOptions, MigrateUpRequest request,
    List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)> successfullyMigratedRecords,
    List<MigrationFileResult> migrationResults,
    List<MigrationRecord> existingRecords)
```

### GetExecutionOrder (Static Helper)

**Visibility**: `internal static`

Returns the ordered sequence of `(FileOrderId, TargetAlias)` pairs based on the `TargetMigrationOrder` of a single target group. Pure static helper for unit testing execution order logic without requiring TemplateExecutor.

```csharp
internal static List<(int FileOrderId, string TargetAlias)> GetExecutionOrder(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroup)
```

- `Simultaneously`: File -> Target (outer loop files, inner loop targets)
- `Successively` / `Undefined`: Target -> File (outer loop targets, inner loop files)

### GetFullExecutionOrder (Static Helper)

**Visibility**: `internal static`

Returns the full execution order across all releases and target groups: `Release -> TargetGroup (config order or explicit order) -> inner file/target order (per TargetMigrationOrder)`. Delegates to `GetExecutionOrder` for the inner ordering. When `targetGroupMigrationOrder` is provided, calls `ValidateAndReorderTargetGroups` to reorder target groups accordingly.

```csharp
internal static List<(int FileOrderId, string TargetGroupAlias, string TargetAlias)> GetFullExecutionOrder(
    List<MigrationFileInfo> files, List<TargetGroupOptions> targetGroups,
    string[]? targetGroupMigrationOrder = null)
```

### TargetGroupMigrationOrder

`TargetGroupMigrationOrder` overrides the default TargetGroup execution sequence (which follows the configuration array order) for `MigrateUpAsync` and `BaselineAsync`. It does not affect `MigrateDownAsync`.

The order is resolved per-release via `ResolveTargetGroupMigrationOrder` using the following priority chain (highest to lowest):

1. **CLI**: `request.TargetGroupMigrationOrder` (e.g., `--target-group-migration-order Backend,Frontend`)
2. **migsettings**: `TargetGroupMigrationOrder` key in the release-level `migsettings.txt` (TOML array, e.g. `["Backend", "Frontend"]`)
3. **appsettings**: `ProductOptions.TargetGroupMigrationOrder` (comma-separated string in configuration)
4. **null**: use the configuration array order (no override)

Validation rules enforced by `ValidateAndReorderTargetGroups`:
- Only applicable when the product has more than one TargetGroup (throws `ConfigurationValidationException` if only one exists)
- Must specify all TargetGroup aliases exactly once
- Alias matching is case-sensitive (a case-insensitive match triggers a corrective error message)
- Duplicates are rejected

### TargetGroupExecutionResult

**Visibility**: `internal class` (nested in MigrationService)

Result type returned by both `ExecuteTargetGroupSimultaneously` and `ExecuteTargetGroupSuccessively`.

```csharp
internal class TargetGroupExecutionResult
{
    public bool Success { get; set; } = true;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public MigrationFileInfo? FailedFile { get; set; }
    public int FailedMigrationRecordId { get; set; }
    public string? ErrorMessage { get; set; }
}
```

## CLI Tool Execution

RayMigrator supports executing migration files via external CLI tools (e.g., `sqlcmd`, `psql`, `mysql`, `mariadb`, `sqlite3`) as an alternative to the built-in DAL-based SQL execution. CLI tools are configured globally under `RayMigrator.CliTools[]` and referenced via the `UseCliToolAlias` property at multiple levels of the configuration hierarchy.

### UseCliToolAlias Resolution

`ResolveUseCliToolAlias()` (`internal static`) resolves the effective CLI tool alias for a file+target combination. The file-level TOML/migsettings `UseCliToolAlias` takes precedence over the target-level `UseCliToolAlias`. Returns `null` when no CLI tool should be used (standard DAL execution).

```csharp
internal static string? ResolveUseCliToolAlias(MigrationFileInfo file, TargetOptions targetOptions)
{
    return file.UseCliToolAlias ?? targetOptions.UseCliToolAlias;
}
```

The `UseCliToolAlias` property is available at five configuration levels (lowest to highest priority): `ProductDefaults` -> `ProductOptions` -> `TargetGroupOptions` -> `TargetOptions` -> migration file TOML/migsettings. The cascade from `ProductDefaults` through `TargetOptions` is applied at startup by `ProductDefaultsPostConfigureOptions` (each level inherits from its parent when not explicitly set); by execution time `TargetOptions.UseCliToolAlias` already holds the cascaded value. `ResolveUseCliToolAlias` then only resolves the final file-vs-target override.

### GetCliToolByAlias

`GetCliToolByAlias()` (`private`) looks up a `CliToolOptions` by alias from `RayMigratorOptions.CliTools[]`. Throws `ConfigurationValidationException` if the alias is not found or no `CliTools` are defined.

### ResolveCliToolArguments

`ResolveCliToolArguments()` (`internal static`) replaces placeholders in a CLI tool's `ArgumentTemplate`:

- `{FilePath}` is replaced with the migration file's full path
- Custom placeholders (e.g., `{Server}`, `{User}`, `{Password}`, `{Database}`) are resolved from `TargetOptions.CliToolParameters`

### ExecuteWithCliTool

`ExecuteWithCliTool()` (`internal`) executes a migration file via an external CLI tool. Unlike `ExecuteSqlBlocks()` which processes blocks individually, the CLI tool executes the entire file as a single unit.

```csharp
internal async Task<(int succeededBlocks, int failedBlocks)> ExecuteWithCliTool(
    MigrationFileInfo file,
    TargetGroupOptions targetGroupOptions,
    TargetOptions targetOptions,
    int migrationRecordId,
    MigrationRunMode runMode,
    CliToolOptions cliToolOptions)
```

Behavior:
- **Non-Migrate modes** (Validate/Simulate): Logs the CLI tool that would be invoked; optionally updates repository
- **Migrate mode**: Builds arguments via `ResolveCliToolArguments`, reads file content for `Stdin` input mode, delegates to `ICliToolExecutor.ExecuteAsync()`
- On success: returns `(file.FileUpBlocksTotal, 0)`
- On failure: throws `MigrationExecutionException` with exit code and stderr details

### ICliToolExecutor / CliToolExecutor

**Location**: `Raycoon.RayMigrator.Services/CliToolExecutor.cs`

`ICliToolExecutor` defines a single method:

```csharp
public interface ICliToolExecutor
{
    Task<CliToolExecutionResult> ExecuteAsync(CliToolExecutionRequest request, CancellationToken cancellationToken = default);
}
```

`CliToolExecutionRequest` carries all parameters for a single tool invocation:

```csharp
public class CliToolExecutionRequest
{
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; }
    public required CliToolInputMode InputMode { get; init; }
    public string? FileContent { get; init; }      // Used when InputMode = Stdin
    public required string FilePath { get; init; }
    public required string Filename { get; init; }
    public required int TimeoutInSeconds { get; init; }
    public required ExitCodeMatcher ExitCodeMatcher { get; init; }
}
```

`CliToolExecutionResult` is returned by `ExecuteAsync`:

```csharp
public class CliToolExecutionResult
{
    public required bool Success { get; init; }
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}
```

`CliToolExecutor` implements this interface using `System.Diagnostics.Process`. It:
- Starts the external process with redirected stdout, stderr, and optionally stdin
- Writes file content to stdin when `InputMode = Stdin`
- Enforces a configurable timeout (`CliToolTimeoutInSeconds`, default 120s); kills the process tree on timeout
- Evaluates the exit code against the `ExitCodeMatcher` whitelist (built from `SuccessExitCodes`, default `["0"]`). Any exit code not matched is treated as failure.
- Arguments are not logged (may contain passwords from `CliToolParameters`); only the executable path and filename are logged

Exception types:
- `CliToolExecutionException` (subclass of `MigrationExecutionException`): Process start failure or unexpected exit code
- `CliToolTimeoutException` (subclass of `CliToolExecutionException`): Process exceeded the configured timeout

## Orphaned Run Auto-Fix

### RepositoryMigrationRunInsertWithAutoFix

**Visibility**: `internal`

When `RepositoryMigrationRunInsert` fails with `MigrationAlreadyRunningException` (ResultCode `-2`), this method automatically attempts to fix orphaned runs before retrying:

1. Queries orphaned MigrationRun entries via `RepositoryMigrationRunSelectOrphaned`
2. Filters to runs older than `AutoFixOrphanedRunsThresholdMinutes` (10 minutes)
3. If no auto-fixable orphaned runs are found, rethrows the original exception
4. For each auto-fixable orphaned run: fixes MigrationRecord entries (`RepositoryMigrationRecordFixOrphaned` with `NotMigrated`) and marks the run as Error (`RepositoryMigrationRunFixOrphaned`)
5. Retries `RepositoryMigrationRunInsert` once

Used by `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync`.

## Recovery: TryFinalizeCompletedMigration

**Visibility**: `internal`

Before executing a file+target combination, both `ExecuteTargetGroupSimultaneously` and `ExecuteTargetGroupSuccessively` call `TryFinalizeCompletedMigration` to recover from crashes between target execution and the final status update. This handles the case where all SQL blocks were executed successfully but the migration record was not updated to `Migrated` status (e.g., due to a process crash).

Conditions for finalization:
1. An `Executing` record exists for the same file, release, target group, and target
2. All blocks were executed (`FileUpBlocksMigrated > 0` and `FileUpBlocksMigrated >= FileUpBlocksTotal`)
3. The file's SQL blocks hash matches (`FileUpBlocksHash` match)
4. No rollback was attempted (`FileDownHash` is null)

If found, the record is updated to `Migrated` status and the file+target is skipped (already done).

Returns the existing record ID if finalized, or `-1` if no such record exists.

## Safety Warnings

### LogMigrationSafetyWarnings

**Visibility**: `internal`

Called during Phase 2 (after filtering, before execution) to log warnings for potentially dangerous migration configurations:

- **Rule 2.1 — ROLLBACK_ACTION_WITHOUT_TRANSACTION**: A rollback-type `MigrationErrorAction` is configured but `UseTransaction=false`. Partial changes from failed SQL blocks cannot be automatically reverted without a transaction.
- **Rule 2.2 — ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE**: A rollback-type `MigrationErrorAction` is configured but `RequireRollbackFile=false` and no rollback file exists. Rollback for the file will be skipped if an error occurs.
- **Rule 2.6 — RUN_ALWAYS_WITH_HASH_VALIDATION**: `RunAlways=true` combined with `HashValidationScope=File` or `SqlBlocks`. Hash validation may report false positives for RunAlways files whose content changes between runs.
- **Rule 2.7 — USE_TRANSACTION_IRRELEVANT_WITH_CLI**: `UseTransaction` was explicitly set (via TOML header or migsettings) but a CLI tool is configured. UseTransaction has no effect when a CLI tool executes the migration. Warns per file (file-level `UseCliToolAlias`) or per target (target-level `UseCliToolAlias`, filtered by the file's TargetGroupAlias).
- **Rule 2.8 — DDL_ON_NON_TRANSACTIONAL_DB**: DDL statements (`CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `RENAME`) found in a file targeting a database without transactional DDL support (e.g., MariaDB, MySQL) with `UseTransaction=true`. DDL causes implicit COMMIT — transaction protection is limited. Detection uses `DalSpecificProperties.SupportsTransactionalDdl`.
- **Rule 2.9 — NO_TRANSACTION_MULTI_BLOCK**: `UseTransaction=false` with multiple SQL blocks. Partial failures cannot be atomically rolled back by the database.
- **Rule 2.10 — NO_TRANSACTION_WITH_RETRIES**: `UseTransaction=false` combined with `DbCommandMaxRetries > 0`. Retries may cause duplicate execution of non-idempotent statements.
- **Rule 2.12 — SIMULTANEOUSLY_WITH_ROLLBACK**: A TargetGroup uses `TargetMigrationOrder=Simultaneously` combined with a rollback-type `MigrationErrorAction`. Rollback in Simultaneously mode affects targets in interleaved order, which may produce inconsistent state across targets.

Uses a compiled `DdlPattern` regex (`^\s*(CREATE|ALTER|DROP|TRUNCATE|RENAME)\s`) to detect DDL statements.

## Atomic Shared Connection Execution

When the Repository and a migration Target share the same ConnectionString, `MigrationService` uses a shared connection to guarantee atomic commits of SQL blocks and repository status updates.

### Guard: `CanUseSharedConnection`

A static pure function that evaluates four conditions:

```csharp
internal static bool CanUseSharedConnection(
    MigrationFileInfo file, TargetOptions targetOptions,
    RepositoryOptions repository, string targetGroupDatabaseType,
    bool ignoreBlockErrors)
```

Returns `true` only when:
1. `file.UseTransaction == true`
2. `ignoreBlockErrors == false`
3. `repository.DatabaseType` matches `targetGroupDatabaseType` (case-insensitive)
4. `targetOptions.ConnectionString` equals `repository.ConnectionString` (ordinal comparison)

### Forward Path: `ExecuteSqlBlocksAtomic`

Called by `ExecuteSqlBlocks` when `CanUseSharedConnection` returns `true`. Wraps all SQL blocks, per-block repository updates, and the final `Migrated` status update in a single transaction:

1. `targetDal.CreateConnection()` → `OpenAsync()`
2. `connection.BeginTransactionAsync()`
3. For each block: `ExecuteNonQueryAsync` + `RepositoryMigrationUpdate(Executing)` — both on the shared connection
4. `RepositoryMigrationUpdate(Migrated)` — inside the same transaction
5. `CommitAsync()` — atomic commit

On failure: `RollbackAsync()` undoes all SQL blocks and all repository writes.

**File-level retry**: If `DbCommandMaxRetries > 0` and the error is transient (`DalBase.IsTransient` returns `true`), the entire sequence (connection creation through block execution) is retried from scratch, up to `MaxRetries` times with `RetryDelayMs` between attempts.

The return type of `ExecuteSqlBlocks` includes a `bool atomicCommitCompleted` flag. When `true`, the callers (`ExecuteTargetGroupSimultaneously`, `ExecuteTargetGroupSuccessively`) skip the separate final `RepositoryMigrationUpdate(Migrated)` call that the non-atomic path requires.

### Rollback Path: `ExecuteRollbackBlocksAtomic`

Applies the same atomic pattern to rollback operations. All rollback blocks + the final `NotMigrated` status update run in a single transaction with file-level retry.

If the rollback itself fails (broken rollback SQL), the transaction is rolled back and the `Failed` status is written via the normal (non-shared) path — a separate connection that is independent of the rolled-back transaction.

> **See also**: [Error Handling — Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection) for the conceptual overview.

## Error Handling

### HandleMigrationError

The service handles errors based on `MigrationErrorAction` via `HandleMigrationError()`:

```csharp
private async Task HandleMigrationError(
    ProductOptions productOptions,
    MigrationFileInfo failedFile,
    int failedMigrationRecordId,
    List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)> successfullyMigratedRecords)
{
    // ErrorAction Inheritance: file-level TOML override takes precedence over product-level config
    var errorAction = failedFile.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

    switch (errorAction)
    {
        case MigrationErrorAction.Terminate:
            // No rollback - database may be in unclear state
            break;

        case MigrationErrorAction.RollbackErrorOnly:
            // Rollback only the failed migration
            await RollbackSingleMigration(productOptions, failedFile, failedMigrationRecordId);
            break;

        case MigrationErrorAction.Rollback:
            // Rollback failed + all previously successful migrations (reverse order)
            // Failed file uses _ctxAccessor.Current.MigrationState.TargetAlias
            // Successful records use the stored targetAlias from the tuple (bug fix: correct per-target alias)
            await ExecuteRollbackForMigrations(recordsToRollback, productOptions, runMode);
            break;

        case MigrationErrorAction.RollbackRelease:
            // Rollback failed + all successful migrations from the same release only
            await ExecuteRollbackForMigrations(releaseRecordsToRollback, productOptions, runMode);
            break;

        case MigrationErrorAction.Ignore:
            // Log only - no rollback performed (Ignore is handled at TargetGroup level)
            break;
    }
}
```

**Important**: The `successfullyMigratedRecords` tuple stores `targetOptions.Alias!` per record (not `_ctxAccessor.Current.MigrationState.TargetAlias`). This ensures that when building rollback records, each successful migration uses the correct target alias it was executed against, not the last-set target alias from the context.

The `MigrationErrorAction` is resolved via **ErrorAction Inheritance**: a file-level override (from TOML metadata or migsettings) takes precedence over the product-level configuration. This applies both in `HandleMigrationError` and within `ExecuteTargetGroupSimultaneously`/`ExecuteTargetGroupSuccessively` where `MigrationErrorAction.Ignore` causes the file to be marked as Failed while execution continues to the next file.

### ExecuteRollbackForMigrations

**Visibility**: `private`

Shared between MigrateUp (error recovery) and MigrateDown (explicit rollback). Returns a `RollbackResult` (private nested class).

For each migration record to rollback:
1. Locates the rollback file on disk
2. If missing and `RequireRollbackFile=true`: aborts the entire rollback chain (marks record as Failed)
3. If missing and `RequireRollbackFile=false` during **error-recovery rollback** (`MigrationErrorAction=Rollback/RollbackRelease/RollbackErrorOnly`): checks `StopRollbackOnMissingRollbackFile` (CLI > TargetGroup > Product, default `true`). If `true`, stops the chain (adds warning, returns without marking Failed). If `false`, logs a warning and continues to the next record without updating the migration status.
4. If missing and `RequireRollbackFile=false` during **explicit migrate-down**: always logs a warning and continues the chain (skips the record, does not update status)
5. Parses rollback file and resolves `RollbackErrorAction` (file-level override -> product-level -> default `Terminate`)
6. Updates migration record with rollback metadata (`RepositoryMigrationUpdateRollback`)
7. Resolves CLI tool alias via `ResolveUseCliToolAlias(rollbackFileInfo, targetOptions)`
8. If a CLI tool alias is resolved: executes via `ExecuteWithCliTool()` (entire rollback file as single unit)
9. Otherwise: executes rollback SQL blocks via DAL (supports resume from partial rollback via `FileDownBlocksMigrated`)
10. On block failure with `RollbackErrorAction.Terminate`: aborts entire rollback chain
11. On block failure with `RollbackErrorAction.Ignore`: skips failed block, continues rollback
12. On all blocks successful: marks migration as `NotMigrated`

### RollbackSingleMigration

**Visibility**: `private`

Convenience method for `MigrationErrorAction.RollbackErrorOnly`. Creates a single-element `MigrationRecord` list and delegates to `ExecuteRollbackForMigrations`.

### ExtractErrorCode

**Visibility**: `private static`

Extracts a categorized `ErrorCode` from exceptions using pattern matching:
- `MigrationAlreadyRunningException` -> `TemplateResultCode.MigrationAlreadyRunning`
- `UndefinedTemplateResultException` -> `ResultCode` from the exception (checked before `TemplateResultException` since it is a subclass)
- `TemplateResultException` -> `ResultCode` from the exception
- `MigrationFileParsingException` -> `ErrorCode` if present, else `TemplateResultCode.MigrationFileParsingFailed`
- `ConfigurationValidationException` -> `TemplateResultCode.ConfigurationValidationFailed`
- Other exceptions -> `null`

## File Discovery and Parsing

### DiscoverAndPrepareMigrationFiles

**Visibility**: `private`

Discovers and parses all migration files for a product. Steps:
1. Loads migsettings defaults (`LoadMigSettingsDefaults`)
2. Enumerates all `.{extension}` files recursively, sorted by relative path
3. Skips rollback files, environment-specific files for other environments, and migsettings files
4. Parses each file (`ParseMigrationFile`), applying TOML environment filter
5. Validates `RequireRollbackFile` (throws `MigrationFileParsingException` if missing rollbacks)

### ParseMigrationFile

**Visibility**: `private`

Parses a single migration file:
1. Reads file content with configured encoding
2. Extracts TOML metadata block (`ExtractTomlAndSql`)
3. Parses TOML configuration (`ParseTomlConfig`)
4. Applies migsettings defaults (file-level TOML keys override migsettings)
5. Extracts release version and target group alias from path
6. Checks `ShouldSkipBlockSplitting` -- if CLI tool execution is configured for all targets, treats entire SQL as one block; otherwise determines block delimiter and splits via `SplitSqlIntoBlocks`
7. Computes SHA-256 hashes (file, config, SQL blocks)
8. Checks for rollback file existence

### Key Static Helpers

All marked `internal static` for testability:

| Method | Purpose |
|--------|---------|
| `ExtractTomlAndSql` | Extracts TOML block (`/* [RayMigrator] ... */`) and SQL content |
| `ParseTomlConfig` | Parses TOML key=value pairs (UseTransaction, Description, Environments, Targets, RunAlways, RequireRollbackFile, MigrationErrorAction, RollbackErrorAction, UseCliToolAlias, TargetGroupMigrationOrder, StopRollbackOnMissingRollbackFile) |
| `ParseTomlBool` | Parses `true`/`false` TOML values |
| `ParseTomlString` | Strips surrounding quotes from TOML strings |
| `ParseTomlStringArray` | Parses `["val1", "val2"]` TOML arrays |
| `ParseTomlEnum<T>` | Parses TOML enum values (rejects `Undefined`) |
| `GetValidEnumValues<T>` | Returns valid (non-zero) enum value names for error messages |
| `SplitSqlIntoBlocks` | Splits SQL by database-specific delimiter (regex, multiline, case-insensitive) |
| `ShouldSkipBlockSplitting` | Determines whether block splitting should be skipped for a migration file (returns true when file-level `UseCliToolAlias` is set or when all targets in the file's target group use CLI tools) |
| `BuildMigrationRunSettingsJson` | Serializes a complete settings snapshot as JSON (masks connection strings) |
| `GetRollbackFilename` | Constructs rollback filename (e.g., `20_InsertData.rollback.sql`) |
| `IsRollbackFile` | Checks if filename matches rollback pattern |
| `IsEnvironmentSpecificFile` | Checks if filename contains environment suffix |
| `IsForEnvironment` | Checks if environment-specific file matches current environment |
| `GetFileEncoding` | Resolves `Encoding` from name (defaults to UTF-8) |
| `DetectOutOfOrderFiles` | Finds files from releases older than the highest migrated release |
| `FilterByTargetRelease` | Filters files up to and including a target release |
| `FilterReleasesAfterTarget` | Filters files from releases after the target (for MigrateDown) |
| `FilterByTargetGroups` | Filters files by target group aliases |
| `ValidateTargetGroupAliases` | Validates that specified target group aliases exist in config |
| `ValidateTargetGroupAliasCasing` | Validates that directories in the migration root match TargetGroup alias casing exactly; throws `ConfigurationValidationException` on case-mismatch |
| `ValidateFlatLayoutAmbiguity` | Ensures each release uses either flat or traditional directory layout exclusively; throws `ConfigurationValidationException` on mixed layout within the same release |
| `ResolveUseCliToolAlias` | Resolves the effective CLI tool alias for a file+target (file-level override -> target-level, returns null for DAL execution) |
| `ResolveCliToolArguments` | Replaces `{FilePath}` and custom `CliToolParameters` placeholders in a CLI tool's `ArgumentTemplate` |
| `ParseTargetGroupMigrationOrder` | Parses a comma-separated string of TargetGroup aliases into a string array; returns null for null/whitespace input |
| `ValidateAndReorderTargetGroups` | Validates a TargetGroup execution order array against the configured TargetGroups (exact-case match required) and returns the reordered list |

Additionally, this private static helper exists:

| Method | Purpose |
|--------|---------|
| `SerializeTomlAsJson` | Serializes parsed TOML properties into a JSON string for `FileUpConfigJson` |

And this private instance helper:

| Method | Purpose |
|--------|---------|
| `GetBlockDelimiter` | Determines the SQL block delimiter for a file based on its target group's database type (falls back to `GO`) |
| `GetCliToolByAlias` | Looks up a `CliToolOptions` by alias from the global `CliTools` configuration; throws `ConfigurationValidationException` if not found |

### Key Instance Helpers

| Method | Visibility | Purpose |
|--------|-----------|---------|
| `RepositoryMigrationRunInsertWithAutoFix` | `internal` | Inserts a MigrationRun, auto-fixing orphaned runs older than `AutoFixOrphanedRunsThresholdMinutes` if a parallel-run lock is detected |
| `FilterAlreadyMigratedFiles` | `internal` | Filters out successfully migrated files (respects `RunAlways`, per-TargetGroup `HashValidationScope`, and hash changes) |
| `ResolveHashValidationScope` | `internal static` | Resolves the effective `HashValidationScope` for a TargetGroup from `ProductOptions` (Undefined falls back to File) |
| `ResolveTargetGroupMigrationOrder` | `internal` | Resolves the effective TargetGroup execution order via priority chain: CLI (`request.TargetGroupMigrationOrder`) > release-level migsettings > `ProductOptions.TargetGroupMigrationOrder` (appsettings) > null (use config array order) |
| `TryFinalizeCompletedMigration` | `internal` | Checks if a previous run completed all blocks but was not finalized (status stuck at Executing); if found, updates to Migrated |
| `FindResumableBlock` | `internal` | Finds a resumable partial execution (Failed/Executing record with matching block hash) |
| `ExecuteSqlBlocks` | `internal` | Executes SQL blocks against a target database (supports `ignoreBlockErrors` and `startFromBlock` for resume); delegates to `ExecuteSqlBlocksAtomic` when `CanUseSharedConnection` returns `true` |
| `ExecuteSqlBlocksAtomic` | `private` | Executes all SQL blocks and the final `Migrated` status update in a single transaction on a shared connection; supports file-level retry on transient errors |
| `ExecuteRollbackBlocksAtomic` | `private` | Executes all rollback SQL blocks and the final `NotMigrated` status update in a single transaction on a shared connection; supports file-level retry on transient errors |
| `LogMigrationSafetyWarnings` | `internal` | Logs warnings for dangerous config combinations: `UseTransaction=false` with multi-block files, `UseTransaction=false` with retries, DDL on databases without transactional DDL support (`SupportsTransactionalDdl=false`), `UseTransaction` explicitly set with CLI tool configured |
| `LoadMigSettingsDefaults` | `internal` | Loads and merges migsettings.txt files (base + environment-specific per directory) |
| `ParseMigSettingsFile` | `internal` | Parses a single migsettings.txt file |
| `ResolveMigSettingsForFile` | `internal` | Resolves the effective migsettings for a file's directory (merges parent directories) |
| `ReplaceEnvironmentVariablesInSqlBlock` | `internal` | Replaces `{ENV:VAR}` placeholders in SQL blocks |
| `ExecuteWithCliTool` | `internal` | Executes a migration file via an external CLI tool (entire file as single unit, no block-wise execution) |

## Internal Types

### RollbackResult

**Visibility**: `private class` (nested in MigrationService)

```csharp
private class RollbackResult
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public bool AllSuccessful => FailCount == 0;
    public string? ErrorMessage { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<MigrationFileResult> FileResults { get; set; } = new();
    public void AddWarning(string filename, string message) { ... }
    public void AddFailure(string filename, string message) { ... }
}
```

### MigSettingsEntry

**Visibility**: `internal class` (nested in MigrationService)

Holds parsed TOML defaults from `migsettings.txt` files:

```csharp
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
```

## Usage Examples

### From RayMigratorService

```csharp
private async Task<int> ExecuteMigrateUpAsync()
{
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

    var result = await _migrationService.MigrateUpAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Migrate-Up failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Migrate-Up completed successfully for product {Product}",
        _consoleOptions.Product);
    return 0;
}
```

### Direct Service Usage (Testing)

```csharp
[Fact]
public async Task MigrateUp_ShouldSucceed_WhenValidRequest()
{
    // Arrange
    var service = _serviceProvider.GetRequiredService<IMigrationService>();
    var request = new MigrateUpRequest
    {
        ProductAlias = "TestProduct",
        Environment = "Test",
        RunMode = MigrationRunMode.Simulate
    };

    // Act
    var result = await service.MigrateUpAsync(request);

    // Assert
    result.Success.Should().BeTrue();
    result.FailedMigrations.Should().Be(0);
}
```

## DI Registration

**Location**: `Raycoon.RayMigrator.Services/ServiceCollectionExtensions.cs`

The `AddRayMigratorServices` extension method registers all service-layer dependencies:

```csharp
public static IServiceCollection AddRayMigratorServices(
    this IServiceCollection services,
    RayMigratorHostMode hostMode = RayMigratorHostMode.Cli)
{
    services.AddScoped<IMigrationService, MigrationService>();
    services.AddScoped<ICliToolExecutor, CliToolExecutor>();

    if (hostMode == RayMigratorHostMode.Cli)
        services.AddSingleton<IMigrationContextAccessor, SingletonMigrationContextAccessor>();
    else
        services.AddScoped<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();

    services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();

    return services;
}
```

- **CLI mode** (`RayMigratorHostMode.Cli`): Uses `SingletonMigrationContextAccessor` (single shared context).
- **API mode** (`RayMigratorHostMode.Api`): Uses `AsyncLocalMigrationContextAccessor` (per-request context isolation via `AsyncLocal<T>`).
- `IMigrationContextFactory` is always a singleton (stateless factory).
- `ICliToolExecutor` / `CliToolExecutor` is registered as scoped for external SQL tool execution.

## Related Documentation

- [Template Executor](template-executor.md) - SQL execution
- [File Discovery](file-discovery.md) - Migration discovery
- [Block Execution](block-execution.md) - SQL block parsing
- [RayMigratorService](../05-console-layer/raymigrator-service.md) - CLI bridge
