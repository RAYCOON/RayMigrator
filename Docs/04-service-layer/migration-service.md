# Migration Service

The `MigrationService` is the central orchestrator for all migration operations. See [activity-diagrams.md](activity-diagrams.md) for visual flowcharts of each command.

## Interface

**Location**: `Raycoon.RayMigrator.Services.Abstractions/IMigrationService.cs`

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

A cancelled call stops at the next block boundary and returns its result instead of throwing: migrate-up, migrate-down and baseline end with `Success = false` and `MigrationRunResult.Cancelled` (or `Error`, when the rollback a cancelled migrate-up started did not complete cleanly, or when the migrate-up failed at a file before the cancel stopped its error recovery), update-hash and fix with `ErrorCode = TemplateResultCode.Cancelled` (1004) and what they did so far. The read operations (`ValidateHashAsync`, `GetStatusAsync`, `GetHistoryAsync`) throw `OperationCanceledException`. The token of the call cancels with `MigrationCancelledAction.Terminate`; a host that lets the user choose the action passes a [`RunCancellation`](#runcancellation) in the request. See [Error Handling: Cancellation](../02-core-concepts/error-handling.md#cancellation) and [ADR-028](../arc42/09-Architecture-Decisions.md#adr-028-a-run-cancels-at-the-next-block-boundary-with-a-migrationcancelledaction-and-records-cancelled).

`Raycoon.RayMigrator.Services.Abstractions` has two more services for hosts that manage a repository without a run, both from the repository options alone, without a product, an environment or a `MigrationContext`:

- `IRepositorySchemaService`: `Task<RepositorySchemaResult> EnsureAsync(RepositoryOptions repository, CancellationToken cancellationToken = default)` runs the schema steps of ADR-024 (see [MigrationContext](../02-core-concepts/migration-context.md#creating-or-upgrading-a-repository-without-a-run)). Within a run, `MigrationService` runs the same steps through `TemplateExecutor.RepositoryCheckCreateAsync`.
- `IRepositoryMaintenanceService`: `Task<RetentionResult> CleanUpAsync(RepositoryOptions repository, RetentionRequest request, CancellationToken cancellationToken = default)` deletes old rows of `MigrationRecordHistory` and `MigrationLog` in batches (ADR-027, see [Repository Schema: Retention](../03-database-layer/repository-schema.md#retention)).

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
    public RunCancellation? Cancellation { get; set; }   // the handle the host cancels the run with; the first Cancel decides the action
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
    public RunCancellation? Cancellation { get; set; }   // stops at the next rollback block; the action is ignored
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
    public RunCancellation? Cancellation { get; set; }   // stops before the next file; the action is ignored
}
```

### FixIssuesRequest {#fix-issues-request}

```csharp
public class FixIssuesRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public FixScope Scope { get; set; } = FixScope.OrphanedRuns;
    public int OlderThanMinutes { get; set; } = 2;   // minimum age of the last heartbeat (or of the start); 0 repairs every open run
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate; // Migrate repairs, Simulate lists only (#22)
    public MigrationStatus? AssumedMigrationStatus { get; set; }   // null (default): each open record keeps its progress; NotMigrated or Migrated: set on every open record
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
}
```

### RunCancellation

**Location**: `Raycoon.RayMigrator.Services.Abstractions/Models/RunCancellation.cs`

The handle a host cancels one run with, and the action the run takes when it stops. A `CancellationToken` carries no payload, so the host passes the handle in the request (`MigrateUpRequest.Cancellation`, `MigrateDownRequest.Cancellation`, `BaselineRequest.Cancellation`) and calls `Cancel` when the user stops the run:

```csharp
public sealed class RunCancellation : IDisposable
{
    public CancellationToken Token { get; }                 // cancelled by the first Cancel
    public MigrationCancelledAction? Action { get; }        // null while the run was not cancelled
    public string? Reason { get; }
    public DateTime? RequestedAtUtc { get; }
    public bool IsCancellationRequested { get; }

    public bool Cancel(MigrationCancelledAction action, string? reason = null);   // true for the call that cancelled
    public void Dispose();
}
```

The first `Cancel` decides the action; later calls and calls after `Dispose` return `false`, and `Undefined` throws `ArgumentOutOfRangeException`. `Cancel` may be called from another thread while the run reads the action; it runs the cancel callbacks of the token, those of the providers among them, synchronously on the calling thread (MySQL opens a connection of its own for `KILL QUERY`) and swallows their exceptions: the action and the token are set before the callbacks run, so the run stops at its next checkpoint all the same, even when the statement in progress could not be interrupted. The engine links `Token` with the token of the call; a run cancelled through the token of the call alone takes `Terminate`. One handle belongs to one run, and the host disposes it after the run returned. The CLI creates one per process (see [RayMigratorService](../05-console-layer/raymigrator-service.md)).

## Enums Used in Request and Response Types

### MigrationRunMode

Controls the execution behavior of a migration command.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Validate` | 10 | Validates configuration and files; does not connect to the targets or read the repository (the database log is still initialized unless it is disabled) |
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
| `Baseline` | 110 | Marking migration files as migrated without executing them (`baseline` command) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationOperation.cs`

`BaselineAsync` stamps its run and records with `Baseline`; `MigrateDownAsync` stamps the records it rolls back with `MigrateDown`, the error-recovery rollbacks and the rollbacks of a cancel action inside a migrate-up stamp them with `Rollback`. A record keeps the `MigrationRunId` of the run that created it, so `GetHistoryAsync` does not count records per run: it reads the terminal transitions from `MigrationRecordHistory` (`Repository_MigrationRecordHistory_Select`), whose rows carry the run that caused each transition, and summarizes them per run via `MigrationService.SummarizeRun()` (distinct records, records that reached the target state of the run's operation, failed records); the operation comes from the run row (`MigrationRun.MigrationOperationId`, schema version 2) (#13). This is the only place the engine reads `MigrationRecordHistory`, and no decision depends on it: a run whose history rows a retention cleanup deleted reports no migrations ([ADR-027](../arc42/09-Architecture-Decisions.md#adr-027-the-engine-deletes-log-and-history-rows-on-a-hosts-request-and-never-reads-them-for-a-decision)).

Every non-migrate command starts with `InitializeRepositoryAsync()`: `RepositoryCheckCreateAsync` always runs, but product and environment are only registered (`*_CheckInsert`) when the command's `CommandProfile.WritesRepository` is true; `info`, `validate-hash` and `fix --run-mode simulate` look them up read-only and treat a repository without them as empty (#6). The error-recovery rollbacks (`HandleMigrationError`) and the rollbacks of a cancel action (`HandleCancelActionAsync`) take the run mode from the request that started the run, not from the context.

### MigrationRunResult

Tracks the outcome of a migration operation.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `Running` | 10 | Migration currently in progress |
| `PartialSuccess` | 50 | Finished, but at least one file was skipped or left Failed |
| `Cancelled` | 70 | Stopped on request at the next block or file boundary; the `MigrationCancelledAction` completed |
| `Recovered` | 80 | Failed, but the error-recovery rollback completed cleanly (#18) |
| `Error` | 90 | Stopped due to error, or the error recovery did not complete cleanly, also when a cancel stopped it (with the code of the failure); also a cancelled migrate-up whose rollback did not complete cleanly |
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

### FixScope

Scope of the Fix command.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not set |
| `All` | 1 | Fixes all known repository problems |
| `OrphanedRuns` | 2 | Fixes orphaned MigrationRun entries (process crashed while Running) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/FixScope.cs`

### MigrationErrorAction

Defines the behavior when a migration file encounters an error. Values: `Terminate` (10), `Rollback` (20), `RollbackErrorOnly` (21), `RollbackRelease` (22), `Ignore` (30). See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior descriptions and [Enum Reference](../08-cli-reference/command-reference.md#enum-reference) for the complete enum table.

### RollbackErrorAction

Defines the behavior when a rollback operation encounters an error. Since a failed rollback cannot itself be rolled back, only `Terminate` (10, default) and `Ignore` (30) are meaningful. See [Error Handling — Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling) for details.

### MigrationCancelledAction

What a cancelled migrate-up does after it stopped, chosen by the first `RunCancellation.Cancel` (a value of the run, not a configuration option): `Terminate` (10, default), `RollbackRun` (20, the rollback chain of `Rollback`), `RollbackFile` (21, of `RollbackErrorOnly`), `RollbackRelease` (22, of `RollbackRelease`); `Undefined` (0) is invalid. Migrate-down, baseline and a running error recovery ignore it. Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationCancelledAction.cs`. See [Error Handling: Cancellation](../02-core-concepts/error-handling.md#migrationcancelledaction).

### PendingMigrationReason

Why the next migrate-up runs a file on a target, or refuses to run it, as `PendingMigrationInfo.Reason` reports it.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid |
| `NotMigrated` | 10 | No record on the target, a rolled-back record, or a record of a run that is still open without a committed block or with every rollback block run (the orphan repair of the next migrate-up settles the records of that run by their progress, and the list classifies them as it leaves them); runs from the first block |
| `Failed` | 20 | The last attempt failed, or stopped without a final status in a run that has ended, and left nothing to resume; runs from the first block |
| `Resumable` | 30 | A previous run committed the leading blocks and stopped (a failure, a cancel with `Terminate`, a run that ended after the last block without finalizing the record, a run that ended abruptly, whose open record the orphan repair settles with its committed blocks, or a failed block that `MigrationErrorAction=Ignore` skipped); migrate-up skips `ResumeFromBlock` blocks. Never for a `RunAlways` file or a file that a CLI tool runs on the target, which run in full |
| `RunAlways` | 40 | The file runs on every migrate-up (`RunAlways = true`), always in full from its first block: migrate-up neither resumes it after the committed blocks of an earlier attempt nor finalizes a record of it. Takes precedence over every other reason except `RollbackIncomplete` |
| `HashMismatch` | 50 | Migrated, but its hash under the `HashValidationScope` of the target group no longer matches; runs again from the first block |
| `RollbackIncomplete` | 60 | The rollback of the file stopped in the middle: the record is `Failed`, or `Executing` in a run that has ended, with `RollbackBlocksMigrated` of its `RollbackBlocksTotal` rollback blocks (a cancelled migrate-down or error recovery, a failed rollback block after the first, the orphan repair of a run that ended abruptly in the middle of the rollback). Migrate-up refuses to run while there is one; migrate-down continues the rollback with the next rollback block. Takes precedence over every other reason |

Source: `Raycoon.RayMigrator.Services.Abstractions/Models/PendingMigrationReason.cs`

### CliToolInputMode

Controls how the migration SQL file is passed to an external CLI tool.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not explicitly set; falls back to `File` behavior at runtime |
| `File` | 1 | File path passed as a command-line argument via `{FilePath}` in `ArgumentTemplate`. Used by tools like `sqlcmd -i`, `psql -f`, `sqlite3 -init` |
| `Stdin` | 2 | File content piped to the process via standard input. Used by tools like `mysql` and `mariadb` |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/CliToolInputMode.cs`

`InputMode` is required; a tool without it fails configuration validation (`RayEnum(isRequired: true)` in Core, RULE_3_11 in the Validation project). `InputModeEnum` resolves the string to the member and throws for a value that is not a member name (#19).

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
    public int MigrationRunId { get; set; }   // MigrationRun.Id; 0 when the run mode writes no run row or the run ended before the row was inserted
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }   // also Cancelled (70) for a cancelled run, which returns instead of throwing
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
    public int UpdatedFiles { get; set; }      // distinct files updated on at least one target, counted at their first updated record
    public int UpdatedRecords { get; set; }    // repository records (file x target) updated (#9)
    public int NewFiles { get; set; }
    public int RemovedFiles { get; set; }      // counted once per file, not once per target record
    public List<string> UpdatedFileNames { get; set; } = new List<string>();
}
```

### BaselineResult

Returned by `BaselineAsync`. `TargetReleaseVersion` is nullable — `null` when all releases were baselined.

```csharp
public class BaselineResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public int MigrationRunId { get; set; }   // MigrationRun.Id; 0 when the run failed before the row was inserted
    public string? TargetReleaseVersion { get; set; }
    public int BaselinedFiles { get; set; }   // files recorded per release and target group
    public int BaselinedRecords { get; set; } // records written (file x pending target); also set when the baseline failed midway
    public MigrationRunResult Result { get; set; }   // what the run row records: Ok, Error, or Cancelled
}
```

`RunCompletedEvent.Result` carries `BaselineResult.Result` for baseline as well.

### MigrationStatusInfo

Returned by `GetStatusAsync`:

```csharp
public class MigrationStatusInfo
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public DateTime? LastMigrationDate { get; set; }   // end (or start) of the newest run row
    public int TotalMigrationsExecuted { get; set; }
    public int PendingMigrations { get; set; }   // always PendingFiles.Count, counted per file and target
    public List<PendingMigrationInfo> PendingFiles { get; set; } = [];
    public MigrationRunResult? LastRunResult { get; set; }
    public Dictionary<string, TargetGroupStatus> TargetGroups { get; set; } = new Dictionary<string, TargetGroupStatus>();
}
```

`PendingFiles` lists what the next migrate-up runs, one entry per file and target, in the order migrate-up runs them without `--target-group-migration-order`: releases in order; the target groups of a release in their `TargetGroupMigrationOrder` (the migsettings of the release, else the product), otherwise in configuration order; within a target group file by file or target by target as its `TargetMigrationOrder` says, the targets in configuration order. A file is pending on every target its `Targets` filter selects: a `RunAlways` file always, any other file unless the target has a Migrated record whose hash matches under the `HashValidationScope` of the target group. A run with `--to-release` or `--target-group` runs a part of the list, and files of a release older than what a target has migrated are listed although migrate-up refuses them without `--allow-out-of-order`.

### PendingMigrationInfo

One entry of `MigrationStatusInfo.PendingFiles`:

```csharp
public sealed record PendingMigrationInfo(
    string ReleaseVersion,
    string TargetGroupAlias,
    string? TargetAlias,           // null only when neither the configuration, the Targets filter nor a record names a target
    string Filename,
    int FileOrderId,               // the discovery order, as FileStartedEvent reports it
    PendingMigrationReason Reason, // see PendingMigrationReason above
    int? ResumeFromBlock,          // Resumable only: the committed leading blocks migrate-up skips (the block count when it only finalizes)
    int? RollbackBlocksMigrated = null,   // RollbackIncomplete only: the rollback blocks that ran; migrate-down continues after them
    int? RollbackBlocksTotal = null);     // RollbackIncomplete only: the blocks of the rollback file
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
    public int PendingMigrations { get; set; }        // the entries of PendingFiles of this target group
    public DateTime? LastMigrationDate { get; set; }  // FinishedAt of its Migrated record that finished last; null when none has an end
    public List<string> Targets { get; set; } = new List<string>();
}
```

`LastMigrationDate` comes from `MigrationRecord.FinishedAt` in UTC, which the record select returns since 0.20.0; with an external DAL whose select lacks the column it stays null. A record that the orphan repair settled as `Migrated` counts with the time of the repair, which it writes as `FinishedAt`.

### MigrationHistory

Returned by `GetHistoryAsync`:

```csharp
public class MigrationHistory
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;   // the runs of this environment only
    public List<MigrationRunInfo> Runs { get; set; } = new List<MigrationRunInfo>();
}
```

### MigrationRunInfo

Individual run entry within `MigrationHistory`:

```csharp
public class MigrationRunInfo
{
    public int MigrationRunId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public MigrationRunMode RunMode { get; set; }
    public int TotalMigrations { get; set; }        // from MigrationRecordHistory: 0 for a run whose history rows were cleaned up
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? FromRelease { get; set; }   // the highest Migrated release when the run started; null for a first run
    public string? ToRelease { get; set; }
    public MigrationRunSource? Source { get; set; }
    public string? InitiatedBy { get; set; }
    public string? InitiatedBySubject { get; set; }
    public string? Host { get; set; }
    public string? ErrorCode { get; set; }       // the engine error code that decided a result other than Ok, e.g. "1004" for Cancelled
    public string? ErrorMessage { get; set; }    // cut to 4000 characters; null for Ok
    public DateTime? LastHeartbeatAt { get; set; }
}
```

### FixIssuesResult

Returned by `FixIssuesAsync`:

```csharp
public class FixIssuesResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool WasSimulated { get; set; }
    public List<FixScope> Repairs { get; set; } = new();   // the repairs the scope expanded to (#16)
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
    public DateTime? LastHeartbeatAt { get; set; }    // the start until the first heartbeat; null only for a row written without it
    public double MinutesSinceHeartbeat { get; set; } // the age --older-than compares (since the start without a heartbeat)
    public string? InitiatedBy { get; set; }
    public string? Host { get; set; }
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
    public string? TargetAlias { get; set; }    // null when the entry summarizes all targets (a file that succeeded in FileByFile order)
    public int MigrationRecordId { get; set; }  // 0 when no record exists or applies
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
    private readonly TemplateExecutor _templateExecutor;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICliToolExecutor _cliToolExecutor;

    public MigrationService(
        ILogger<MigrationService> logger,
        TemplateExecutor templateExecutor,
        IMigrationContextAccessor ctxAccessor,
        IServiceProvider serviceProvider,
        ICliToolExecutor cliToolExecutor)
    {
        _logger = logger;
        _templateExecutor = templateExecutor;
        _ctxAccessor = ctxAccessor;
        _serviceProvider = serviceProvider;
        _cliToolExecutor = cliToolExecutor;
    }
}
```

The service reads the configuration of the current run (products, CLI tools, repository) from `_ctxAccessor.Current.RayMigratorOptions`, never from `IOptions<RayMigratorOptions>`. A host that runs one context per operation therefore needs no options registration in its container; the CLI pipeline binds `IOptions` only to validate the files and hands the value to the context factory.

### Orphan Threshold and Heartbeat

The service has no constants of its own. The age after which an open run counts as orphaned is `Repository.OrphanedRunThresholdInMinutes` of the context (default 2), compared with the age of the run's last heartbeat (or of its start, for a run without one) by `RepositoryMigrationRunInsertWithAutoFix`. A run that inserted its row refreshes `MigrationRun.LastHeartbeatAt` every `Repository.HeartbeatIntervalInSeconds` (default 30, `0` switches it off) through `MigrationRunHeartbeat` until it ends.

### MigrateUpAsync Flow

```mermaid
sequenceDiagram
    participant Caller
    participant Svc as MigrationService
    participant Tmpl as TemplateExecutor
    participant Repo as Repository
    participant Target as Target DB

    Caller->>Svc: MigrateUpAsync(request, cancellationToken)
    Note over Svc: Links the token of the call with request.Cancellation

    Note over Svc: Phase 1: Initialization<br/>(state always set; repo ops only when ShouldWriteRepository)
    Svc->>Svc: Set MigrationRunResult=Running, MigrationOperation=MigrateUp
    Svc->>Tmpl: RepositoryCheckCreateAsync()
    Tmpl->>Repo: Schema steps via RepositorySchemaExecutor:<br/>Repository_CheckCreate, missing Repository_Upgrade_n,<br/>Repository_MigratorMeta_CheckInsert
    Repo-->>Tmpl: MigratorMetaId

    Svc->>Tmpl: RepositoryProductCheckInsertAsync()
    Tmpl->>Repo: Ensure product exists
    Repo-->>Tmpl: ProductId

    Svc->>Tmpl: RepositoryEnvironmentCheckInsertAsync()
    Tmpl->>Repo: Ensure environment exists
    Repo-->>Tmpl: EnvironmentId

    Svc->>Tmpl: RepositoryMigrationGetInterruptedAsync(token)
    Svc->>Svc: BuildMigrationRunSettingsJson()
    Svc->>Svc: RepositoryMigrationRunInsertWithAutoFix(settingsJson)
    Note over Svc: Auto-fixes orphaned runs if parallel-run lock detected,<br/>then starts the heartbeat
    Svc->>Tmpl: RepositoryMigrationRunInsertAsync(settingsJson)
    Tmpl->>Repo: Create migration run
    Repo-->>Tmpl: MigrationRunId

    Note over Svc: Phase 2: File Discovery & Preparation
    Svc->>Svc: DiscoverAndPrepareMigrationFiles()
    Svc->>Tmpl: RepositoryMigrationSelectAsync(token)
    Tmpl->>Repo: Query existing records
    Svc->>Svc: FilterAlreadyMigratedFiles()
    Svc->>Svc: FilterByTargetRelease()
    Svc->>Svc: ValidateTargetGroupAliases()
    Svc->>Svc: FilterByTargetGroups()
    Svc->>Svc: DetectOutOfOrderFiles()
    Svc->>Svc: FindIncompleteRollbacks()
    Note over Svc: Refuses the run (Error, no file runs) for out-of-order files<br/>without --allow-out-of-order and for a record whose rollback stopped in the middle
    Svc->>Svc: LogMigrationSafetyWarnings()

    Note over Svc: Phase 3: Execute Migrations
    loop For each release
        Note over Svc: ResolveTargetGroupMigrationOrder()<br/>(CLI > migsettings > appsettings > config order)
        loop For each TargetGroup (resolved order)
            Note over Svc: Dispatch by TargetMigrationOrder
            alt FileByFile (file→target)
                Svc->>Svc: ExecuteTargetGroupFileByFile()
                Note over Svc: Inside: checkpoint, TryFinalizeCompletedMigrationAsync()<br/>RepositoryMigrationInsertAsync()<br/>ResolveUseCliToolAlias() → ExecuteWithCliTool() or ExecuteSqlBlocks()<br/>RepositoryMigrationUpdateAsync(Migrated)
            else TargetByTarget (target→file)
                Svc->>Svc: ExecuteTargetGroupTargetByTarget()
                Note over Svc: Inside: checkpoint, TryFinalizeCompletedMigrationAsync()<br/>RepositoryMigrationInsertAsync()<br/>ResolveUseCliToolAlias() → ExecuteWithCliTool() or ExecuteSqlBlocks()<br/>RepositoryMigrationUpdateAsync(Migrated)
            end
            opt result.Cancelled
                Svc->>Svc: CompleteCancelledMigrateUpAsync()
                Note over Svc: HandleCancelActionAsync() without the token,<br/>RepositoryMigrationRunUpdateAsync(Cancelled, 1004)<br/>or (Error, 1005); return
            end
        end
    end

    Note over Svc: Phase 5: Finalization
    Svc->>Tmpl: RepositoryMigrationRunUpdateAsync(Ok or PartialSuccess)
    Svc-->>Caller: MigrationOperationResult
```

### MigrateUpAsync (Simplified)

The context is always accessed via `_ctxAccessor.Current` (not a direct `_ctx` field), which supports both CLI (singleton) and API (async-local per-request) hosting modes.

```csharp
public async Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request, CancellationToken cancellationToken = default)
{
    // The run observes the token of the call and the RunCancellation of the request, whichever comes first
    using var runCancellation = LinkRunCancellation(cancellationToken, request.Cancellation);
    var token = runCancellation.Token;

    // The records this run wrote, in execution order, for the rollback of an error action or a cancel action: the files
    // it migrated or finalized, and the files it left Failed under MigrationErrorAction=Ignore
    var recordsOfThisRun = new List<RunRecord>();

    try
    {
        // A run cancelled before it started writes nothing
        token.ThrowIfCancellationRequested();

        // Phase 1: Initialization
        _ctxAccessor.Current.MigrationState.MigrationRunResult = MigrationRunResult.Running;
        _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;

        if (request.RunMode.ShouldWriteRepository())
        {
            // Repository writes on the repository's own connection never take the token; the checkpoints between them
            // stop the run. Only on the atomic path do the record updates share the file's transaction and its token.
            await _templateExecutor.RepositoryCheckCreateAsync();
            await _templateExecutor.RepositoryProductCheckInsertAsync();
            await _templateExecutor.RepositoryEnvironmentCheckInsertAsync();
            var interruptedMigration = await _templateExecutor.RepositoryMigrationGetInterruptedAsync(token);
            if (interruptedMigration != null) _logger.LogWarning("Interrupted migration detected: ...", interruptedMigration.MigrationRecordId, ...);
            token.ThrowIfCancellationRequested();
            var settingsJson = BuildMigrationRunSettingsJson(_ctxAccessor.Current, request.AllowOutOfOrder);
            await RepositoryMigrationRunInsertWithAutoFix(settingsJson);   // also starts the run heartbeat
        }

        // Phase 2: File Discovery & Preparation
        var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);
        token.ThrowIfCancellationRequested();
        List<MigrationFileInfo> filesToMigrate;
        List<MigrationRecord> existingRecords;
        if (request.RunMode.ShouldReadRepository())
        {
            // RepositoryMigrationSelectAsync always reads Migrate-mode records (the only ones that exist),
            // so Simulate mode sees the same records as Migrate mode.
            existingRecords = await _templateExecutor.RepositoryMigrationSelectAsync(token);
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
        DetectOutOfOrderFiles(filesToMigrate, existingRecords);   // out-of-order files refuse the run without --allow-out-of-order

        // A file whose rollback stopped in the middle must not run again before migrate-down completed the rollback
        var incompleteRollbacks = FindIncompleteRollbacks(filesToMigrate, existingRecords, productOptions);
        if (incompleteRollbacks.Count > 0)
            throw new InvalidOperationException(IncompleteRollbackMessage(incompleteRollbacks));

        // Log safety warnings for dangerous configuration combinations
        LogMigrationSafetyWarnings(filesToMigrate, productOptions);

        // Phase 3: Execute Migrations (Release → TargetGroup → Targets)
        foreach (var release in orderedReleases)
        {
            foreach (var targetGroup in orderedTargetGroups)
            {
                var result = targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.FileByFile
                    ? await ExecuteTargetGroupFileByFile(tgFiles, targetGroup, ..., token)
                    : await ExecuteTargetGroupTargetByTarget(tgFiles, targetGroup, ..., token);

                if (result.Cancelled)
                {
                    // The interrupted file is Failed with its committed blocks; take the MigrationCancelledAction
                    // (without the token) and record Cancelled (1004), or Error (1005) when the rollback was not clean
                    return await CompleteCancelledMigrateUpAsync(request, productOptions, result.CancelledFile,
                        result.CancelledMigrationRecordId, recordsOfThisRun, ...);
                }

                if (!result.Success)
                {
                    // The error recovery runs with the token: a cancel stops it at the next rollback block, and the run
                    // stays Error with the code of the failure, its message the failure first, then the recovery
                    var recovery = await HandleMigrationError(productOptions, result.FailedFile!, result.FailedMigrationRecordId,
                        recordsOfThisRun, request.RunMode, token);
                    var runResult = recovery is { Cancelled: false, FailCount: 0, WarningCount: 0 } ? MigrationRunResult.Recovered
                        : MigrationRunResult.Error;
                    string? errorMessage = recovery is { Stop: { } stop }
                        ? CancelledErrorRecoveryMessage(result.ErrorMessage, result.FailedFile!.Filename, stop, recovery.Total, request.Cancellation?.Reason)
                        : result.ErrorMessage;
                    await _templateExecutor.RepositoryMigrationRunUpdateAsync(runResult, result.ErrorCode?.ToString(), errorMessage);
                    return errorResult;
                }
            }
        }

        // Phase 5: Finalization (PartialSuccess when MigrationErrorAction=Ignore skipped files)
        await _templateExecutor.RepositoryMigrationRunUpdateAsync(failedMigrations > 0 ? MigrationRunResult.PartialSuccess : MigrationRunResult.Ok);
        return successResult;
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
        // Cancelled outside a file (before the run row, during discovery or the repository reads): no interrupted file
        return await CompleteCancelledMigrateUpAsync(request, productOptions, null, 0, recordsOfThisRun, ...);
    }
}
```

The real method splits into the public wrapper, which links the tokens and reports `RunCompletedEvent`, and `MigrateUpCoreAsync`; the run heartbeat stops in a `finally` block.

Before the first file, migrate-up refuses to run when a (file, target) pair it would run has a record whose rollback stopped in the middle: `Failed`, or `Executing` in a run that has ended, with `0 < FileDownBlocksMigrated < FileDownBlocksTotal`, as a cancelled migrate-down or error recovery, a failed rollback block after the first or the orphan repair of a run that ended abruptly in the middle of the rollback leaves it (`FindIncompleteRollbacks`, `FindIncompleteRollback`). The record insert would reset the rollback columns and run the file from its first block on a target that kept part of what the rollback did not take back yet. Like a refused out-of-order run, the `InvalidOperationException` ends the run as `Error` before a file runs, in Simulate as well; the message (`IncompleteRollbackMessage`) names the release, file and target of every such record, its `k of n rollback blocks` and that migrate-down continues at block k+1. A `Failed` record without rollback progress (its first rollback block failed) is not refused: nothing continues it, and migrate-up runs the file again from its first block.

### MigrateDownAsync Flow

MigrateDown supports two modes based on `RunMode`:

- **Validate mode** (`!RunMode.ShouldReadRepository()`): Validates rollback file existence and parseability without touching the repository. Returns warnings for missing rollback files.
- **Simulate/Migrate mode** (`RunMode.ShouldReadRepository()`): Queries the records of the releases after the target and picks those migrate-down rolls back (`IsRolledBackByMigrateDown`): `Migrated` records, files that stopped after committed blocks without a rollback (`IsStoppedAfterCommittedBlocks`: `Failed`, or `Executing` in a run that has ended, with `FileUpBlocksMigrated > 0` and no `FileDownHash`), whose rollback file runs from its first rollback block, and rollbacks that stopped in the middle (`HasIncompleteRollback`). It logs a warning for each record of that range that stopped without a committed block (`IsStoppedWithoutCommittedBlock`), which it does not roll back. In Migrate mode, executes rollback SQL in reverse `FileOrderId` order and writes updated repository records. In Simulate mode, reads the repository but does not execute SQL or write any records.

```csharp
public async Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request, CancellationToken cancellationToken = default)
{
    // Linked with request.Cancellation like migrate-up; the action of a cancel is ignored
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
    await _templateExecutor.RepositoryCheckCreateAsync();
    await _templateExecutor.RepositoryProductCheckInsertAsync();
    await _templateExecutor.RepositoryEnvironmentCheckInsertAsync();
    _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateDown;
    await RepositoryMigrationRunInsertWithAutoFix(settingsJson);

    // --- Phase 2: Query migrations for rollback ---
    var recordsInRange = existingRecords
        .Where(r => string.Compare(r.ReleaseVersion, request.TargetReleaseVersion, StringComparison.OrdinalIgnoreCase) > 0)
        .Where(r => /* target group filter if specified */)
        .ToList();

    // Migrated, a file that stopped after committed blocks, or a rollback that stopped in the middle
    var migrationsToRollback = recordsInRange
        .Where(IsRolledBackByMigrateDown)
        .OrderByDescending(r => r.FileOrderId)
        .ToList();

    // A file that stopped without a committed block is not rolled back; a warning names it
    foreach (var record in recordsInRange.Where(IsStoppedWithoutCommittedBlock))
        _logger.LogWarning("File not rolled back | ... | Reason: its record counts no committed block; ...");

    // --- Phase 3: Execute rollbacks (with the token: a cancel stops the chain at the next rollback block) ---
    var rollbackResult = await ExecuteRollbackForMigrations(migrationsToRollback, productOptions, request.RunMode,
        isErrorRecovery: false, token);

    if (rollbackResult.Cancelled)
    {
        // The record in progress is Failed with its leading rollback blocks; the next migrate-down continues it when some ran,
        // and migrate-up refuses to run the file until then
        await _templateExecutor.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Cancelled, "1004", cancelledMessage);
        return cancelledResult;
    }

    // --- Phase 4: Finalization: Error after a failed rollback, PartialSuccess after a skipped or ignored one, else Ok ---
    await _templateExecutor.RepositoryMigrationRunUpdateAsync(rollbackResult.RunResult);
    return operationResult;
}
```

### BaselineAsync Flow

Baseline marks migration files as migrated without executing their SQL. It follows the same Release-based TargetGroup dispatch as MigrateUp.

```csharp
public async Task<BaselineResult> BaselineAsync(BaselineRequest request, CancellationToken cancellationToken = default)
{
    // Linked with request.Cancellation like migrate-up; a cancel stops before the next file and ignores the action

    // --- Phase 1: Initialization ---
    await _templateExecutor.RepositoryCheckCreateAsync();
    await _templateExecutor.RepositoryProductCheckInsertAsync();
    await _templateExecutor.RepositoryEnvironmentCheckInsertAsync();
    _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.Baseline;
    await RepositoryMigrationRunInsertWithAutoFix(settingsJson);

    // --- Phase 2: File Discovery ---
    var migrationFiles = DiscoverAndPrepareMigrationFiles(productOptions, request.Environment);

    // --- Phase 3: Filter files ---
    var filesToBaseline = FilterByTargetRelease(migrationFiles, request.TargetReleaseVersion);
    ValidateTargetGroupAliases(request.TargetGroupAliases, productOptions.TargetGroups!);
    filesToBaseline = FilterByTargetGroups(filesToBaseline, request.TargetGroupAliases);

    var existingRecords = await _templateExecutor.RepositoryMigrationSelectAsync(token);
    filesToBaseline = FilterAlreadyMigratedFiles(filesToBaseline, existingRecords, productOptions);

    // --- Phase 4: Record each file as migrated (Release → TargetGroup → Targets) ---
    foreach (var release in baselineOrderedReleases)
    {
        foreach (var targetGroup in productOptions.TargetGroups!)
        {
            if (targetGroup.TargetMigrationOrderEnum == TargetMigrationOrder.FileByFile)
            {
                // File → Target order
                foreach (var file in tgFiles)
                    foreach (var target in targetGroup.Targets!)
                        await BaselineFile(file, target);
            }
            else
            {
                // Target → File order (TargetByTarget, default)
                foreach (var target in targetGroup.Targets!)
                    foreach (var file in tgFiles)
                        await BaselineFile(file, target);
            }
        }
    }

    // BaselineFile: Validate CLI alias + Insert + Update(Migrated) without executing SQL
    async Task BaselineFile(MigrationFileInfo file, TargetOptions targetOptions)
    {
        token.ThrowIfCancellationRequested();   // checkpoint: the records written so far stay Migrated

        // Validate CLI tool alias if set (no execution, but ensures config is correct)
        string? cliAlias = ResolveUseCliToolAlias(file, targetOptions);
        if (cliAlias != null) GetCliToolByAlias(cliAlias); // Throws if alias not found

        int migrationRecordId = await _templateExecutor.RepositoryMigrationInsertAsync(...);
        await _templateExecutor.RepositoryMigrationUpdateAsync(migrationRecordId, MigrationStatus.Migrated, file.FileUpBlocksTotal);
    }

    // --- Phase 5: Finalization (a cancel records Cancelled with 1004 and returns BaselineResult.Result = Cancelled) ---
    await _templateExecutor.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Ok);
    return baselineResult;   // BaselineResult.Result = Ok
}
```

### ValidateHashAsync Flow

Validates migration file integrity by comparing file hashes on disk against the repository.

1. **Phase 1**: Repository initialization (`InitializeRepositoryAsync`: `RepositoryCheckCreateAsync`, product and environment looked up read-only)
2. **Phase 2**: File discovery and optional target group filtering
3. **Phase 3**: Query existing records (`RepositoryMigrationSelectAsync`)
4. **Phase 4**: Compare files with repository records per `HashValidationScope` (File or SqlBlocks). Detects three issue types:
   - **New**: File on disk, not yet migrated
   - **Modified**: Hash mismatch between disk and repository
   - **Missing**: Record in repository but file no longer on disk
5. **Phase 5**: Build and return `ValidationResult`

A cancel throws `OperationCanceledException`.

### UpdateHashAsync Flow

Updates stored hashes in the repository to match current file contents (after approved migration file changes).

1. **Phase 1**: Repository initialization (`InitializeRepositoryAsync`: `RepositoryCheckCreateAsync`, `RepositoryProductCheckInsertAsync`, `RepositoryEnvironmentCheckInsertAsync`)
2. **Phase 2**: File discovery and optional target group filtering
3. **Phase 3**: Query existing records
4. **Phase 4**: For each file, take its Migrated records (one per target) and compare all three hashes (`FileUpHash`, `FileUpConfigHash`, `FileUpBlocksHash`) per record via `HashesDiffer`; `null` and an empty config hash are treated as equal (a file without a TOML block). Call `RepositoryMigrationUpdateHashAsync` for every stale record. Also counts files missing from disk, once per file.
5. **Phase 5**: Return `HashUpdateResult` with updated/new/removed counts

A cancel stops before the next record and returns `Success = false`, `ErrorCode = 1004` and the records updated so far; a file counts in `UpdatedFiles` and `UpdatedFileNames` once its first record is updated, so a cancel between the records of a file reports it.

### GetStatusAsync Flow

Returns the current migration status overview for a product (used by the `info` command).

1. **Phase 1**: Repository initialization (`InitializeRepositoryAsync`: `RepositoryCheckCreateAsync`, product and environment looked up read-only)
2. **Phase 2**: Query existing migration records (`RepositoryMigrationSelectAsync`)
3. **Phase 3**: File discovery
4. **Phase 4**: Compute status metrics: current release, last run result and date (the newest `MigrationRun` row, `RepositoryMigrationRunSelectAsync(1)`), the pending migrations and the per-target-group status (`TargetGroupStatus`)

The pending migrations come from `ListPendingMigrations`, which evaluates every file per target with the checks of migrate-up (`FilterAlreadyMigratedFiles` rules, `IsAppliedOnTarget`, the `Targets` filter and the `HashValidationScope` of the target group) and classifies each pending pair with `ClassifyPendingMigration`: a record whose rollback stopped in the middle (`FindIncompleteRollback`) is `RollbackIncomplete` first, whatever the file is, with `RollbackBlocksMigrated` and `RollbackBlocksTotal` from the record, because migrate-up refuses to run while there is one; the `Pending` and `Executing` records of the open run (the newest run row is `Running`) are first classified as the orphan repair of the next migrate-up leaves them (`ProjectOrphanRepair`): `Failed` with committed blocks, `Migrated` with every block, `NotMigrated` without progress, a rollback likewise; then a `RunAlways` file is `RunAlways`, whatever its record says, because migrate-up runs it in full from its first block; otherwise a record that `TryFinalizeCompletedMigrationAsync` finalizes or `FindResumableBlock` resumes is `Resumable` with its `ResumeFromBlock` (both helpers pass over a `RunAlways` file, so that the status and migrate-up decide alike); then `HashMismatch` for a Migrated record whose hash no longer matches, `Failed` for a failed attempt without a resume point, and `NotMigrated` for the rest. `OrderAsMigrateUpRuns` sorts the pairs into the order of a migrate-up without `--target-group-migration-order`; a `TargetGroupMigrationOrder` that migrate-up would refuse leaves the target groups in configuration order with a warning, so that the status still answers, and one that names exactly the only target group of the product is ignored, as migrate-up ignores it, with a warning for an order of the migsettings (the validation at startup reports the product setting as RULE_1_13). `PendingMigrations` is the length of the list; every `TargetGroupStatus` counts its entries and takes `LastMigrationDate` from its Migrated record that finished last. Every value of the status reads the projected records: the current release, `TotalMigrationsExecuted` and the release and the executed migrations of a target group count a record that the repair will finish as `Migrated`, while `LastMigrationDate` counts only records with an end, which the projection does not write.

Returns `MigrationStatusInfo` with product-level and target-group-level status. A cancel throws `OperationCanceledException`.

### GetHistoryAsync Flow

Returns migration run history for a product.

1. **Phase 1**: Repository initialization (`InitializeRepositoryAsync`: `RepositoryCheckCreateAsync`, product and environment looked up read-only)
2. **Phase 2**: Query the newest `MigrationRun` rows of the product and environment via `RepositoryMigrationRunSelectAsync(limit)` and the terminal transitions of all records via `RepositoryMigrationRecordHistorySelectAsync()` (`Repository_MigrationRecordHistory_Select`)
3. **Phase 3**: For each run, group the history rows by run and compute totals (total, successful, failed) with `SummarizeRun` against the operation of the run row (`MigrationOperationId`); the run details (source, initiator, host, error code and message, heartbeat) come from the run row

Returns `MigrationHistory` with a list of `MigrationRunInfo` entries. Apart from the counts of the retention, this is the only reader of `MigrationRecordHistory` in the engine, and no command decides on it: a run whose history rows `IRepositoryMaintenanceService.CleanUpAsync` deleted is listed with 0 migrations (ADR-027). A cancel throws `OperationCanceledException`.

### FixIssuesAsync Flow

Fixes repository inconsistencies such as orphaned `MigrationRun` entries.

1. **Phase 1**: Repository initialization (`InitializeRepositoryAsync`; product and environment registered in Migrate mode, looked up read-only in Simulate)
2. **Phase 2**: Query orphaned runs via `RepositoryMigrationRunSelectOrphanedAsync(productId, environmentId)`
3. **Phase 3**: Filter by `OlderThanMinutes` against `MinutesSinceHeartbeat` (the age of the last heartbeat, or of the start for a run without one)
4. **Phase 4**: Log found orphans
5. **Phase 5**: Unless the run mode is `Simulate`, fix each orphaned run: mark the run as Error (`RepositoryMigrationRunFixOrphanedAsync`, which refuses a run that wrote a heartbeat since the select), then settle its open MigrationRecord entries (`RepositoryMigrationRecordFixOrphanedAsync` with `AssumedMigrationStatus`: null settles each entry by its progress, a status sets it on every entry)
6. **Phase 6**: Return `FixIssuesResult`

A cancel stops before the next run and returns `Success = false`, `ErrorCode = 1004` and the runs fixed so far.

## TargetGroup Execution

### ExecuteTargetGroupFileByFile

**Visibility**: `internal` (for testability)

Executes migrations in **file-first** order: `foreach file -> foreach target`. This means each migration file is applied to all targets before moving to the next file.

An error aborts the entire TargetGroup unless `MigrationErrorAction` is `Ignore`, in which case the file is marked as Failed with the blocks before its first failed block, which the next run resumes after, its record joins the records of the run (`Failed` with those blocks, also none; after an exception only when the record was inserted), which a later rollback of the run rolls back, and execution continues to the next file. Within the FileByFile loop, when block-level failures occur with Ignore, remaining targets for that file are skipped (`break`), and the next file is processed.

```csharp
internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupFileByFile(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
    ProductOptions productOptions, MigrateUpRequest request,
    List<RunRecord> recordsOfThisRun,
    List<MigrationFileResult> migrationResults,
    List<MigrationRecord> existingRecords,
    CancellationToken cancellationToken)
```

Each file/target execution:
1. Checkpoint: a cancelled run stops before the next file or target
2. Finalizes a record a previous run completed but never finalized (`TryFinalizeCompletedMigrationAsync`; never for a `RunAlways` file, nor for a file that a CLI tool runs, whose alias `ResolveUseCliToolAlias(file, targetOptions)` resolves before this step: the tool runs the whole file)
3. Inserts a migration record (`RepositoryMigrationInsertAsync`)
4. Sets `_ctxAccessor.Current.MigrationState` fields (MigrationRecordId, ReleaseVersionFromFileNameWithPath, FilenameWithRelativePath, FileOrderId, TargetGroupAlias, TargetAlias, FileBlockId, FileBlocksCommitted)
5. Checks for resumable partial execution (`FindResumableBlock`; a `RunAlways` file and a file that a CLI tool runs always start at their first block)
6. A file that resumes, which happens on the DAL path only, writes the blocks it resumes after to its record before its first block (`MarkResumedRecordAsync`: `Executing` with those blocks), because the insert reset the counter and a run that ends before the next block commits must keep them
7. If a CLI tool alias is resolved: executes via `ExecuteWithCliTool()` (entire file as single unit)
8. Otherwise: executes SQL blocks via `ExecuteSqlBlocks()` (block-wise DAL execution, a checkpoint before every block)
9. Updates migration record status (`RepositoryMigrationUpdateAsync`)
10. Adds `new RunRecord(file, migrationRecordId, targetOptions.Alias!, status, committedBlocks)` to `recordsOfThisRun`: `Migrated` with every block for a migrated or finalized file, `Failed` with `MigrationState.FileBlocksCommitted` for a file that ended with failed blocks under `Ignore`

An `OperationCanceledException` while the token is cancelled goes to `CompleteCancelledFileAsync`: the block in progress did not commit where the provider interrupted it (SQLite, MySQL after `KILL QUERY`, and MariaDB in a `SLEEP()` that its `KILL QUERY` ended without an error may have run it to its end), the record is `Failed` with `MigrationState.FileBlocksCommitted` (under `MigrationErrorAction = Ignore` the blocks before the first failed one, as at the end of such a file), written without the token on the repository's own connection, and `FileCompletedEvent` reports `FileOutcome.Cancelled`. The method returns with `Cancelled = true`, `CancelledFile` and `CancelledMigrationRecordId`; a run cancelled before the file started on the target leaves no interrupted file. A cancel before the next target of a file that migrated on a target of this run already counts that file like a finished file (`SuccessCount`, a `MigrationFileResult` with `Success = true`), as the error path counts a file that failed on one of its targets; `RollbackFile` then has no interrupted file to roll back.

### ExecuteTargetGroupTargetByTarget

**Visibility**: `internal` (for testability)

Executes migrations in **target-first** order: `foreach target -> foreach file`. This means all migration files are applied to one target before moving to the next target.

Error and cancel handling behavior is identical to FileByFile: Ignore causes the file to be marked as Failed and continues to the next file; all other error actions abort the TargetGroup; a cancel stops before the next target or file, or at the next block of the file in progress. Like FileByFile, execution branches between CLI tool (`ResolveUseCliToolAlias` / `ExecuteWithCliTool`) and DAL (`ExecuteSqlBlocks`) per file/target.

```csharp
internal async Task<TargetGroupExecutionResult> ExecuteTargetGroupTargetByTarget(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroupOptions,
    ProductOptions productOptions, MigrateUpRequest request,
    List<RunRecord> recordsOfThisRun,
    List<MigrationFileResult> migrationResults,
    List<MigrationRecord> existingRecords,
    CancellationToken cancellationToken)
```

### GetExecutionOrder (Static Helper)

**Visibility**: `internal static`

Returns the ordered sequence of `(FileOrderId, TargetAlias)` pairs based on the `TargetMigrationOrder` of a single target group. Pure static helper for unit testing execution order logic without requiring TemplateExecutor.

```csharp
internal static List<(int FileOrderId, string TargetAlias)> GetExecutionOrder(
    List<MigrationFileInfo> files, TargetGroupOptions targetGroup)
```

- `FileByFile`: File -> Target (outer loop files, inner loop targets)
- `TargetByTarget` / `Undefined`: Target -> File (outer loop targets, inner loop files)

### GetFullExecutionOrder (Static Helper)

**Visibility**: `internal static`

Returns the full execution order across all releases and target groups: `Release -> TargetGroup (config order or explicit order) -> inner file/target order (per TargetMigrationOrder)`. Delegates to `GetExecutionOrder` for the inner ordering. When `targetGroupMigrationOrder` is provided, calls `ValidateAndReorderTargetGroups` to reorder target groups accordingly; it drops the warning of an order that names the only target group, which leaves the configuration order.

```csharp
internal static List<(int FileOrderId, string TargetGroupAlias, string TargetAlias)> GetFullExecutionOrder(
    List<MigrationFileInfo> files, List<TargetGroupOptions> targetGroups,
    string[]? targetGroupMigrationOrder = null)
```

### TargetGroupMigrationOrder

`TargetGroupMigrationOrder` overrides the default TargetGroup execution sequence (which follows the configuration array order) for `MigrateUpAsync` and `BaselineAsync`, and `GetStatusAsync` lists the pending migrations in it. It does not affect `MigrateDownAsync`.

The order is resolved per-release via `ResolveTargetGroupMigrationOrder` using the following priority chain (highest to lowest):

1. **CLI**: `request.TargetGroupMigrationOrder` (e.g., `--target-group-migration-order Backend,Frontend`)
2. **migsettings**: `TargetGroupMigrationOrder` key in the release-level `migsettings.txt` (TOML array, e.g. `["Backend", "Frontend"]`)
3. **appsettings**: `ProductOptions.TargetGroupMigrationOrder` (comma-separated string in configuration)
4. **null**: use the configuration array order (no override)

Validation rules enforced by `ValidateAndReorderTargetGroups(executionOrder, targetGroups, source, out string? ignoredTargetGroup)`; `source` names where the order comes from (`--target-group-migration-order`, `migsettings of release <release>` or `product setting`), and every message about the order names it as `(source: <source>)`:
- Only applicable when the product has more than one TargetGroup. On a product with one TargetGroup an order that names exactly its alias is ignored: the method returns that TargetGroup and sets `ignoredTargetGroup` to its alias, and `LogIgnoredTargetGroupMigrationOrder` logs `TargetGroupMigrationOrder (source: <source>) is ignored: the product has only one TargetGroup [<alias>].` once per source and command (the CLI order once, the migsettings of each release once), and not for the product setting, which RULE_1_13 reports at startup. Any other order on such a product throws `ConfigurationValidationException` (`... is not allowed when product has only 1 TargetGroup(s).`)
- Must specify all TargetGroup aliases exactly once
- Alias matching is case-sensitive (a case-insensitive match triggers a corrective error message)
- Duplicates are rejected

The configuration validation applies the same rules to `ProductOptions.TargetGroupMigrationOrder` at startup (RULE_1_10 to RULE_1_13, see [Validation Rules](../appendix/validation-rules.md)); the migsettings and CLI orders meet these rules only here, when a command applies them.

### TargetGroupExecutionResult

**Visibility**: `internal class` (nested in MigrationService)

Result type returned by both `ExecuteTargetGroupFileByFile` and `ExecuteTargetGroupTargetByTarget`.

```csharp
internal class TargetGroupExecutionResult
{
    public bool Success { get; set; } = true;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public MigrationFileInfo? FailedFile { get; set; }
    public int FailedMigrationRecordId { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }                   // ExtractErrorCode of the failure, recorded in MigrationRun.ErrorCode
    public bool Cancelled { get; set; }                   // the run was cancelled; Success is false
    public MigrationFileInfo? CancelledFile { get; set; } // the interrupted file, or null when the run stopped before a file started
    public int CancelledMigrationRecordId { get; set; }
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
    CliToolOptions cliToolOptions,
    bool markRecordExecuting = true,
    CancellationToken cancellationToken = default)
```

Behavior:
- **Non-Migrate modes** (Validate/Simulate): Logs the CLI tool that would be invoked and writes nothing
- **Migrate mode**: Builds arguments via `ResolveCliToolArguments`, reads file content for `Stdin` input mode, marks the record `Executing` with 0 committed blocks (the tool commits nothing the engine can count, so a run that ends while the tool runs leaves nothing to resume or to finalize; the rollback chain passes `markRecordExecuting: false`, because it marked its record already and the up counter is not the tool's), delegates to `ICliToolExecutor.ExecuteAsync(request, cancellationToken)`
- On success: returns `(file.FileUpBlocksTotal, 0)`
- On failure: throws `MigrationExecutionException` with exit code and stderr details
- On cancel: the tool is killed with the processes it started and the call ends with an `OperationCanceledException`; the rollback chain calls it with `CancellationToken.None`, because a rollback through a CLI tool always completes. A tool that has exited when the cancel takes effect is judged by its exit code (a success keeps its result if the tool got the whole file); a tool that fails, or that did not get the whole file in `Stdin` mode, after the cancel ends the call with an `OperationCanceledException` as well, so it counts as the cancel and not as a failure for `MigrationErrorAction` (Ctrl+C reaches the whole process group on Linux and macOS, and the cancel may cut off the write of the file)

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
- Reads stdout and stderr from the start of the tool, so that a tool that writes output while it reads its input cannot block the write of the input
- Writes file content to stdin when `InputMode = Stdin`
- Enforces a configurable timeout (`CliToolTimeoutInSeconds`, default 120s) from the start of the tool, which covers the write of the input as well, so that a tool that does not read its input ends at the timeout too; kills the process tree on timeout
- Kills the process tree as well when the caller cancels, and then throws `OperationCanceledException`; a tool is not started when the token is already cancelled, the write to its standard input observes the token, and its output is read without the token, so a cancel that arrives after the tool got the whole file and exited keeps its result
- Evaluates the exit code against the `ExitCodeMatcher` whitelist (built from `SuccessExitCodes`, default `["0"]`). Any exit code not matched is treated as failure. In `Stdin` mode a tool that exits before it read the whole file is judged by its exit code as well, and a success code then throws `CliToolExecutionException`, because the tool cannot have run the whole file.
- Arguments are not logged (may contain passwords from `CliToolParameters`); only the executable path and filename are logged

Exception types:
- `CliToolExecutionException` (subclass of `MigrationExecutionException`): Process start failure or unexpected exit code
- `CliToolTimeoutException` (subclass of `CliToolExecutionException`): Process exceeded the configured timeout

## Orphaned Run Auto-Fix

### RepositoryMigrationRunInsertWithAutoFix

**Visibility**: `internal`

When `RepositoryMigrationRunInsertAsync` fails with `MigrationAlreadyRunningException` (ResultCode `-2`), this method automatically attempts to fix orphaned runs before retrying:

1. Queries orphaned MigrationRun entries via `RepositoryMigrationRunSelectOrphanedAsync`
2. Filters to runs whose last heartbeat (or start) is older than `Repository.OrphanedRunThresholdInMinutes` (default 2)
3. If no auto-fixable orphaned runs are found, rethrows the original exception
4. For each auto-fixable orphaned run: marks the run as Error (`RepositoryMigrationRunFixOrphanedAsync`, which leaves a run alone that wrote a heartbeat since the select) and then settles its open MigrationRecord entries by their progress (`RepositoryMigrationRecordFixOrphanedAsync` without a status: `Failed` with the committed blocks, `Migrated` when every block committed, `NotMigrated` without progress) and logs how many it settled; when every candidate turned out alive, rethrows the original exception
5. Reads the records again for the `FromReleaseVersion` of the new run, because the repair may have finished records, then retries `RepositoryMigrationRunInsertAsync` once and starts the heartbeat

Used by `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync`.

## Recovery: TryFinalizeCompletedMigrationAsync

**Visibility**: `internal`

Before executing a file+target combination, both `ExecuteTargetGroupFileByFile` and `ExecuteTargetGroupTargetByTarget` call `TryFinalizeCompletedMigrationAsync` to finish a record whose blocks all committed but whose final status update never happened. It observes the token before the record is finalized; the update itself takes no token, like every repository write of a run. A run that ended abruptly does not leave such a record behind: the orphan repair finishes an `Executing` record with every block as `Migrated` before the first file (see [Migration State Machine: After a Hard Abort](../02-core-concepts/migration-state-machine.md#after-a-hard-abort)). The record this method finds is `Executing` in a run that has ended, for example when the repository writes at the end of the file failed (the final `Migrated` status and the `Failed` status after it) while the run row could still be closed.

Conditions for finalization:
1. An `Executing` record exists for the same file, release, target group, and target
2. All blocks were executed (`FileUpBlocksMigrated > 0` and `FileUpBlocksMigrated >= FileUpBlocksTotal`)
3. The file's SQL blocks hash matches (`FileUpBlocksHash` match)
4. No rollback was attempted (`FileDownHash` is null)
5. The file is not a `RunAlways` file: such a file runs in full on every migrate-up (`FindFinalizableRecord`, which the pending list of `GetStatusAsync` shares)
6. No CLI tool runs the file on the target: migrate-up does not call the method for such a file, because the tool runs the whole file. The record of a file that a tool runs counts no block while the tool runs, so an `Executing` record with every block can only come from an attempt on the DAL before the target got the tool, or from an engine before 0.20.0, whose tool path counted one block before the tool started

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
- **Rule 2.12 — SIMULTANEOUSLY_WITH_ROLLBACK**: A TargetGroup uses `TargetMigrationOrder=FileByFile` combined with a rollback-type `MigrationErrorAction`. Rollback in FileByFile mode affects targets in interleaved order, which may produce inconsistent state across targets.

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

1. `targetDal.CreateConnection()` → `OpenAsync(cancellationToken)`
2. `connection.BeginTransactionAsync(cancellationToken)`
3. For each block: checkpoint, then `ExecuteNonQueryAsync(..., cancellationToken)` + `RepositoryMigrationUpdateAsync(Executing, connection, transaction, ..., cancellationToken)` — both on the shared connection
4. `RepositoryMigrationUpdateAsync(Migrated)` — inside the same transaction
5. `CommitAsync()` — atomic commit, without the token: a commit that has started completes

On failure or cancel: `RollbackAsync()` undoes all SQL blocks and all repository writes; a cancelled file is then written `Failed` with the blocks before the resume offset (normally 0) on the repository's own connection (on PostgreSQL the cancel leaves the shared transaction unusable).

**File-level retry**: If `DbCommandMaxRetries > 0` and the error is transient (`DalBase.IsTransient` returns `true`, never for a cancellation), the entire sequence (connection creation through block execution) is retried from scratch, up to `MaxRetries` times with `RetryDelayMs` between attempts; the wait observes the token.

The return type of `ExecuteSqlBlocks` includes a `bool atomicCommitCompleted` flag. When `true`, the callers (`ExecuteTargetGroupFileByFile`, `ExecuteTargetGroupTargetByTarget`) skip the separate final `RepositoryMigrationUpdateAsync(Migrated)` call that the non-atomic path requires.

### Rollback Path: `ExecuteRollbackBlocksAtomic`

Applies the same atomic pattern to rollback operations. All rollback blocks + the final `NotMigrated` status update run in a single transaction with file-level retry. The chain calls it with `CancellationToken.None`: interrupted, the transaction would leave the record `Failed` without rollback progress, so an atomic rollback always completes and a cancel takes effect before the next record.

If the rollback itself fails (broken rollback SQL), the transaction is rolled back and the `Failed` status is written via the normal (non-shared) path — a separate connection that is independent of the rolled-back transaction. The record keeps the rollback blocks of the attempt the rollback continued (`rollbackStartBlock`, 0 without one): the transaction took back only the blocks of this attempt.

> **See also**: [Error Handling — Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection) for the conceptual overview.

## Error Handling

### HandleMigrationError

The service handles errors based on `MigrationErrorAction` via `HandleMigrationError()`, which returns the result of the error-recovery rollback chain, or null when the action rolls nothing back, so that the caller can persist the run as `Recovered` when the chain was clean (#18):

```csharp
private async Task<RollbackResult?> HandleMigrationError(
    ProductOptions productOptions,
    MigrationFileInfo failedFile,
    int failedMigrationRecordId,
    List<RunRecord> recordsOfThisRun,
    MigrationRunMode runMode,
    CancellationToken cancellationToken)
{
    // ErrorAction Inheritance: file-level TOML override takes precedence over product-level config
    var errorAction = failedFile.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

    switch (errorAction)
    {
        case MigrationErrorAction.Terminate:
            // No rollback - database may be in unclear state
            return null;

        case MigrationErrorAction.Ignore:
            // Log only - no rollback performed (Ignore is handled at TargetGroup level)
            return null;
    }

    // RollbackErrorOnly, Rollback, RollbackRelease: the shared rollback chain, with the run's token,
    // so a cancel stops the recovery at the next rollback block (RollbackResult.Cancelled); the run stays Error
    return await HandleStopAsync(errorAction, failedFile, failedMigrationRecordId,
        recordsOfThisRun, productOptions, runMode, cancellationToken);
}
```

### HandleCancelActionAsync and HandleStopAsync

`CompleteCancelledMigrateUpAsync` ends a cancelled migrate-up: it reads the action of the request (`request.Cancellation?.Action ?? Terminate`), lets `HandleCancelActionAsync` take it, and records the run as `Cancelled` with `TemplateResultCode.Cancelled` (1004), or as `Error` with `TemplateResultCode.CancelledRollbackIncomplete` (1005) when the rollback reported a failure or a warning; the message is `Cancelled on request (<action>): <outcome>` plus the reason. A run cancelled before its files were discovered has nothing to act on: a rollback action reports `nothing to roll back`, and a run cancelled before its run row was inserted writes none (`MigrationRunId` 0). Simulate and Validate persist nothing and roll nothing back. A cancel is logged as a warning without the exception, whose message the warning names; the exception with its stack trace goes to `Debug`. `HandleCancelActionAsync` maps the action to the error action with the same number (`RollbackRun` → `Rollback`, `RollbackFile` → `RollbackErrorOnly`, `RollbackRelease` → `RollbackRelease`; `Terminate` rolls nothing back) and runs `HandleStopAsync` with `CancellationToken.None`, so a second cancel cannot leave the rollback halfway.

`HandleStopAsync` is the rollback chain of an error action and a cancel action alike. The static `BuildStopChain` builds the list: the stopped file first (`Failed` with its committed blocks, also none, when there is one with a record; a file that failed before the insert of its record ran nothing and is left out), then the records this run wrote (`recordsOfThisRun`), newest first, each as the run left it: the files it migrated or finalized (`Migrated` with every block) and the files it left `Failed` under `Ignore` (with their leading committed blocks, also none, because blocks after a failed one ran as well); all of them for `Rollback`, those of the stopped file's release for `RollbackRelease` (without a stopped file the release of the record the run wrote last), none for `RollbackErrorOnly`; a record of an earlier run never. `BuildStopChain` returns null for an action that rolls nothing back, and `HandleStopAsync` passes a list that is not empty to `ExecuteRollbackForMigrations`, which rolls back each record from its first rollback block.

**Important**: `RunRecord` stores `targetOptions.Alias!` per record (not `_ctxAccessor.Current.MigrationState.TargetAlias`). This ensures that when building rollback records, each record uses the correct target alias it was executed against, not the last-set target alias from the context.

The `MigrationErrorAction` is resolved via **ErrorAction Inheritance**: a file-level override (from TOML metadata or migsettings) takes precedence over the product-level configuration. This applies both in `HandleMigrationError` and within `ExecuteTargetGroupFileByFile`/`ExecuteTargetGroupTargetByTarget` where `MigrationErrorAction.Ignore` causes the file to be marked as Failed while execution continues to the next file.

### ExecuteRollbackForMigrations

**Visibility**: `private`

Shared between MigrateUp (error recovery and the rollback of a cancel action, both through `HandleStopAsync`) and MigrateDown (explicit rollback). Returns a `RollbackResult` (private nested class).

For each migration record to rollback:
1. Locates the rollback file on disk
2. If missing and `RequireRollbackFile=true`: aborts the entire rollback chain; the record keeps its status (nothing of its rollback ran, so migrate-down rolls a `Migrated` record, and a file that stopped after committed blocks with them, back once the file is there), and the error message names the missing rollback file and says so (`KeptStatusWithoutRequiredRollbackFile`; a file that stopped without a committed block, which only the rollback of a stopped migrate-up reaches, is not rolled back by migrate-down, and the next migrate-up runs it from its first block)
3. If missing and `RequireRollbackFile=false` during **error-recovery rollback** (`MigrationErrorAction=Rollback/RollbackRelease/RollbackErrorOnly`): checks `StopRollbackOnMissingRollbackFile` (CLI > TargetGroup > Product, default `true`). If `true`, stops the chain (adds warning, returns without marking Failed). If `false`, logs a warning and continues to the next record without updating the migration status.
4. If missing and `RequireRollbackFile=false` during **explicit migrate-down**: always logs a warning and continues the chain (skips the record, does not update status)

   In both cases a file that stopped after committed blocks keeps them: the warning of the chain names them (`; the record keeps its k committed block(s), and the file stays pending for the next migrate-up`), and when the chain goes on, the log warns that the file keeps its committed blocks instead of the information line `No Rollback file provided`
5. Parses rollback file and resolves `RollbackErrorAction` (file-level override -> product-level -> default `Terminate`)
6. Updates migration record with rollback metadata (`RepositoryMigrationUpdateRollbackAsync`), `Executing` with the rollback blocks the record already counts (a rollback that continues one that stopped in the middle keeps them, so a migrate-down that ends before the next rollback block leaves a rollback that migrate-down continues)
7. Resolves CLI tool alias via `ResolveUseCliToolAlias(rollbackFileInfo, targetOptions)`
8. If a CLI tool alias is resolved: executes via `ExecuteWithCliTool()` (entire rollback file as single unit); a failure of the tool counts as a failed block under `RollbackErrorAction.Ignore`, and under any other action marks the record `Failed` with the rollback blocks of an earlier attempt (the tool counts none) and aborts the chain
9. Otherwise: executes rollback SQL blocks via DAL (supports resume from partial rollback via `FileDownBlocksMigrated`)
10. On block failure with `RollbackErrorAction.Terminate`: marks the record `Failed` with the rollback blocks before the failed one (on the atomic path those of the attempt it continued) and aborts entire rollback chain
11. On block failure with `RollbackErrorAction.Ignore`: skips failed block, continues rollback; `FileDownBlocksMigrated` keeps the leading rollback blocks that ran before the first failure until the rollback ends, and a cancel keeps them, so that the next migrate-down runs the failed block again; the rollback that ran to its end is `Failed` with every rollback block
12. On all blocks successful: marks migration as `NotMigrated`

A chain that reached its end closes its messages, which migrate-down returns, with `RollbackChainSummary`: `Successfully rolled back n migration(s)` when every record was rolled back cleanly, otherwise `Rolled back k of n migration(s) cleanly; m with a warning`, because a record the chain passed with a warning (a missing rollback file that is not required, rollback errors that `RollbackErrorAction.Ignore` skipped) is no clean rollback; its warning comes before the line.

A cancel of the token stops the chain before the next record, which keeps its state, or before the next rollback block of the record in progress, which ends `Failed` with the leading rollback blocks that ran (with `RollbackErrorAction.Ignore` those before the first failed one; `StopCancelledRollbackAsync`, `FileOutcome.Cancelled`, the write takes no token) and which migrate-up refuses to run until migrate-down completed it; the result has `Cancelled = true`. The first block of a rollback file, an atomic rollback and a rollback through a CLI tool always run to their end. The chain of a cancel action receives `CancellationToken.None`.

### ExtractErrorCode

**Visibility**: `internal static`

Extracts a categorized `ErrorCode` from exceptions using pattern matching:
- `OperationCanceledException` -> `TemplateResultCode.Cancelled` (1004)
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
| `ValidateAndReorderTargetGroups` | Validates a TargetGroup execution order array against the configured TargetGroups (exact-case match required) and returns the reordered list; on a product with one TargetGroup it ignores an order of exactly its alias and names that TargetGroup in `ignoredTargetGroup` for the caller to log (`LogIgnoredTargetGroupMigrationOrder`) |
| `BuildStopChain` | Builds the rollback chain of a stopped migrate-up from the stopped file and the records the run wrote (`RunRecord`): the stopped file first when it has a record, then the records in the scope of the action, newest first; null for an action that rolls nothing back (see [HandleCancelActionAsync and HandleStopAsync](#handlecancelactionasync-and-handlestopasync)) |
| `RollbackChainSummary` | The closing line of a rollback chain that reached its end: how many of its records it rolled back cleanly |

Additionally, this private static helper exists:

| Method | Purpose |
|--------|---------|
| `SerializeTomlAsJson` | Serializes parsed TOML properties into a JSON string for `FileUpConfigJson` |

And these private instance helpers:

| Method | Purpose |
|--------|---------|
| `GetBlockDelimiter` | Determines the SQL block delimiter for a file based on its target group's database type (falls back to `GO`) |
| `GetCliToolByAlias` | Looks up a `CliToolOptions` by alias from the global `CliTools` configuration; throws `ConfigurationValidationException` if not found |
| `LogIgnoredTargetGroupMigrationOrder` | Logs `TargetGroupMigrationOrder (source: <source>) is ignored: the product has only one TargetGroup [<alias>].` for an order that `ValidateAndReorderTargetGroups` ignored, once per source and command (the CLI order once, although it is resolved for every release, the migsettings of each release once), and not for the product setting, which RULE_1_13 reports at startup; `{Source:l}` and `{TargetGroup:l}` render the values without the quotes of a string property |

### Key Instance Helpers

| Method | Visibility | Purpose |
|--------|-----------|---------|
| `RepositoryMigrationRunInsertWithAutoFix` | `internal` | Inserts a MigrationRun, auto-fixing orphaned runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` if a parallel-run lock is detected, then starts the heartbeat |
| `FilterAlreadyMigratedFiles` | `internal` | Filters out successfully migrated files (respects `RunAlways`, per-TargetGroup `HashValidationScope`, and hash changes) |
| `ResolveHashValidationScope` | `internal static` | Resolves the effective `HashValidationScope` for a TargetGroup from `ProductOptions` (Undefined falls back to File) |
| `ResolveTargetGroupMigrationOrder` | `internal` | Resolves the effective TargetGroup execution order via priority chain: CLI (`request.TargetGroupMigrationOrder`) > release-level migsettings > `ProductOptions.TargetGroupMigrationOrder` (appsettings) > null (use config array order) |
| `TryFinalizeCompletedMigrationAsync` | `internal` | Checks if a previous run completed all blocks but was not finalized (status stuck at Executing); if found, updates to Migrated. Not for a `RunAlways` file; migrate-up does not call it for a file that a CLI tool runs |
| `FindResumableBlock` | `internal` | Finds a resumable partial execution (Failed/Executing record with matching block hash); 0 for a `RunAlways` file, which runs in full; migrate-up does not call it for a file that a CLI tool runs, which runs in full as well |
| `ExecuteSqlBlocks` | `internal` | Executes SQL blocks against a target database (supports `ignoreBlockErrors` and `startFromBlock` for resume, checks the token before every block); delegates to `ExecuteSqlBlocksAtomic` when `CanUseSharedConnection` returns `true` |
| `ListPendingMigrations` / `ClassifyPendingMigration` | `private` / `internal static` | The pending (file, target) pairs of `GetStatusAsync` with their `PendingMigrationReason`, `ResumeFromBlock` and, for `RollbackIncomplete`, the rollback blocks, in the order of migrate-up (`OrderAsMigrateUpRuns`); never `Resumable` for a file that a CLI tool runs on the target |
| `FindIncompleteRollback` / `FindIncompleteRollbacks` / `IncompleteRollbackMessage` | `internal static` | The record of a (file, target) pair whose rollback stopped in the middle (`Failed`, `0 < FileDownBlocksMigrated < FileDownBlocksTotal`), those of the pairs a migrate-up would run, and the message of the refused run |
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
    public int WarningCount { get; set; }      // skipped missing rollback files, rollbacks ignored under RollbackErrorAction=Ignore
    public bool AllSuccessful => FailCount == 0;
    public bool Cancelled => Stop != null;     // a cancel stopped the chain; the records it did not reach keep their state
    public RollbackStop? Stop { get; set; }    // where: before a record, or between two rollback blocks of a record
    public int Total { get; set; }             // the records the chain was given
    public int RolledBackCount => FileResults.Count(r => r.Success);
    public MigrationRunResult RunResult => FailCount > 0 ? MigrationRunResult.Error
        : WarningCount > 0 ? MigrationRunResult.PartialSuccess : MigrationRunResult.Ok;
    public string? ErrorMessage { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<MigrationFileResult> FileResults { get; set; } = new();
    public void AddWarning(string filename, string message) { ... }
    public void AddFailure(string filename, string message, string? targetAlias = null, int migrationRecordId = 0) { ... }
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
private async Task<int> ExecuteMigrateUpAsync(RunCancellation cancellation)
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
        Cancellation = cancellation,   // the RunCancellation of the process, cancelled by Ctrl+C or SIGTERM
    };

    var result = await _migrationService.MigrateUpAsync(request, cancellation.Token);

    if (!result.Success)
    {
        if (result.Result == MigrationRunResult.Cancelled)
        {
            _logger.LogWarning("Migrate-Up cancelled for product {Product}: {Message}",
                _consoleOptions.Product, result.ErrorMessage);
            return CancelledExitCode;   // 130
        }

        _logger.LogError("Migrate-Up failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

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

## Progress events

The three run operations report their progress through `MigrationContext.ProgressObserver` (`IMigrationProgressObserver`, namespace `Raycoon.RayMigrator.Core.Progress`). The public methods are thin wrappers around the execution bodies: they reset the run-started flag and raise the completion from the result, so an operation that ends before its run row exists still reports a matching start.

| Event | Raised | Carries |
|-------|--------|---------|
| `RunStartedEvent` | once, after the run row exists (migrate-up, migrate-down, baseline) or, in a run mode without a run row, after the repository records were read; before the completion when the operation ended earlier | product, environment, command, run mode, `MigrationRunId` (0 without a run row), `FromRelease` (the highest migrated release, in Simulate and Validate as well), `TargetRelease` |
| `FileStartedEvent` | per file and target, after the record exists and the resume offset and the executor (DAL or CLI tool) are known; a record a previous run completed but never finalized is reported as started and `Recovered` without SQL; for rollbacks once the rollback file of the record is parsed and the record is marked `Executing`, and for a rollback that ends before (a missing rollback file, a failure before the file is parsed) right before its completion, with what the repository recorded; for baseline after the record exists | operation (`MigrateUp`, `MigrateDown`, `Rollback` for error recovery, `Baseline`), release, target group, target, file, `FileOrderId`, `MigrationRecordId`, `BlocksTotal` (1 for a CLI tool; for a rollback that never reached its file, what the repository recorded), `ResumeFromBlock` (0 for a file or a rollback file that a CLI tool runs, because the tool runs it whole, and for a RunAlways file, which migrate-up runs in full) |
| `BlockCompletedEvent` | per executed SQL block: on the block-by-block and simulate paths as the block completes, on the atomic path after the commit of the file; migrations and rollbacks; not for files that run through a CLI tool | block number and total, `Success` (false only under the Ignore error action), error message |
| `FileCompletedEvent` | per file and target, always after its `FileStartedEvent`; a failure before the record insert names the file and target it hit, with record id 0 | `FileOutcome`: `Migrated`, `Recovered`, `Simulated`, `Failed`, `FailedIgnored`, `RolledBack`, `RollbackFailed`, `Skipped` (a rollback without a rollback file, or a chain that stopped at one), `Baselined`, `Cancelled` (100: a cancel interrupted the file or its rollback; a rollback that follows reports its own start and completion); duration; error message |
| `RunCompletedEvent` | once per operation, as the last event | success, the `MigrationRunResult` the operation returned (also `Cancelled`, and for baseline `BaselineResult.Result`; null when the operation threw), the counts of the result, duration, error message |

On the atomic shared-connection path the blocks of a file commit together with the repository update at the end of the file; their events are raised after the commit, so a reported block is durable and a transient retry of the batch repeats no event. Validate-hash, update-hash, fix, info and history raise no events. The engine calls the observer synchronously on the thread of the run; an observer that throws is logged at Warning and the run continues. `NullMigrationProgressObserver.Instance` is the default; the CLI sets no observer.

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
        services.AddSingleton<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();

    services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();
    services.AddSingleton<IRepositorySchemaService, RepositorySchemaService>();
    services.AddSingleton<IRepositoryMaintenanceService, RepositoryMaintenanceService>();

    return services;
}
```

- **CLI mode** (`RayMigratorHostMode.Cli`): `SingletonMigrationContextAccessor`, one context for the whole process.
- **API mode** (`RayMigratorHostMode.Api`): `AsyncLocalMigrationContextAccessor`, one context per async flow through a static `AsyncLocal<T>`. It is a singleton too; a scoped registration would isolate nothing and would make the singleton factory a captive dependency.
- `IMigrationContextFactory` is a singleton that takes the accessor and a logger; it is the entry point of a host for one run (see [MigrationContext](../02-core-concepts/migration-context.md#hosting-the-engine-in-process)).
- `IRepositorySchemaService` / `RepositorySchemaService` is a singleton that takes the host's `TemplateCache` and a logger; `EnsureAsync(RepositoryOptions, CancellationToken)` creates or upgrades a repository without a run and returns `RepositorySchemaResult` (`DatabaseType`, `VersionBefore`, `VersionAfter`, `MigratorMetaId`, `Created`, `Upgraded`).
- `IRepositoryMaintenanceService` / `RepositoryMaintenanceService` is a singleton that takes the host's `TemplateCache` and a logger; `CleanUpAsync(RepositoryOptions, RetentionRequest, CancellationToken)` deletes old history and log rows of a repository and returns `RetentionResult` (`HistoryRows`, `LogRows`, `Batches`, `Completed`, `LogSkipped`, `Message`); see [Repository Schema: Retention](../03-database-layer/repository-schema.md#retention).
- `ICliToolExecutor` / `CliToolExecutor` is registered as scoped for external SQL tool execution.
- Not registered here and supplied by the host: `TemplateCache` (one per process), `TemplateExecutor` (scoped per run), `DatabaseLogWriter`, logging.

## Related Documentation

- [Template Executor](template-executor.md) - SQL execution
- [File Discovery](file-discovery.md) - Migration discovery
- [Block Execution](block-execution.md) - SQL block parsing
- [RayMigratorService](../05-console-layer/raymigrator-service.md) - CLI bridge
