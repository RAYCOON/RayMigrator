# Resilience and Recovery

> **Implementation Status**: FULLY IMPLEMENTED — see [Open Features](../appendix/open-features.md#f3-recovery-orchestration)
>
> `RetryHelper` with transient error detection is fully functional. Block-level resume (`FindResumableBlock`) is implemented and active in `MigrationService` — on re-run, migrations resume from the last failed block automatically. Interrupted migration detection (`RepositoryMigrationGetInterrupted`) is called during `MigrateUpAsync` startup and logs a warning when detected. Orphaned run detection (`RepositoryMigrationRunSelectOrphaned`) is implemented and available via the `Fix` command for manual cleanup. Automatic orphaned run cleanup is implemented via `RepositoryMigrationRunInsertWithAutoFix` — when a migration run insert fails due to `MigrationAlreadyRunningException`, orphaned runs older than 10 minutes (`AutoFixOrphanedRunsThresholdMinutes`) are automatically fixed and the insert is retried once.

This document describes RayMigrator's fault tolerance mechanisms and automatic recovery capabilities.

## Overview

RayMigrator is designed to handle failures gracefully at multiple levels:

1. **Transient Database Errors**: Automatic retry with linear backoff
2. **Mid-Migration Failures**: Block-level state persistence for recovery
3. **Process Crashes**: Interrupted migration detection and resume capability
4. **Orphaned Runs**: Automatic detection and cleanup of stuck migrations (auto-fix on insert + manual Fix command)

## Block-Level Recovery

### How It Works

RayMigrator tracks execution progress at the SQL block level (blocks separated by `GO` or other database-specific delimiters). Each time a block executes successfully, the progress is persisted to the repository database.

```
Migration File (5 blocks)
├── Block 1 ✓ (persisted)
├── Block 2 ✓ (persisted)
├── Block 3 ✗ (failure here)
├── Block 4   (not executed)
└── Block 5   (not executed)
```

If a migration fails at block 3 of 5:

- Blocks 1-2: State persisted as executed (`FileUpBlocksMigrated = 2`)
- Block 3: Marked as failed, migration status set to `Failed`
- Blocks 4-5: Not executed

### Database Schema Support

The `MigrationRecord` table includes columns for block tracking:

| Column | Description |
|--------|-------------|
| `FileUpBlocksTotal` | Total number of blocks in the file |
| `FileUpBlocksMigrated` | Number of blocks successfully executed |
| `MigrationStatusId` | Current status: Pending (10), Executing (20), Failed (30), NotMigrated (50), Migrated (100) |

### Resume Behavior

On re-run after a failure, RayMigrator automatically resumes from the last failed block. The `FindResumableBlock()` method in `MigrationService` checks the `FileUpBlocksMigrated` count for the migration and skips already-executed blocks.

```
Resume Behavior (implemented):
├── Resume from failed block (default, automatic)
│   └── Re-execute from last failed block (FindResumableBlock)
│
Planned (not yet implemented):
├── Force restart (--force-restart)
│   └── Re-execute entire file from beginning
└── Skip (--skip)
    └── Mark as unclear and continue with next file
```

> **Note**: The `--force-restart` and `--skip` CLI options are **not yet implemented**. Currently only automatic resume from the last failed block is supported.

## Transient Error Handling

### Retry Logic

RayMigrator includes built-in retry logic for transient database errors:

```csharp
// Configuration via TargetOptions / TargetDefaults (in appsettings.json)
DbCommandMaxRetries = 3                    // Maximum retry attempts (default: 0 = disabled)
DbCommandWaitTimeInMsBeforeRetry = 250     // Base delay in ms (linear backoff)
```

**Location**: `RetryHelper` in `Raycoon.RayMigrator.Database.Common/RetryHelper.cs`

### Method Overloads

`RetryHelper` provides three static method overloads. All require a transient error predicate as a parameter:

| Method | Description |
|--------|-------------|
| `ExecuteWithRetryAsync<T>(Func<Task<T>>, maxRetries, retryDelayMs, isTransientPredicate, ...)` | Async operation with return value |
| `ExecuteWithRetryAsync(Func<Task>, maxRetries, retryDelayMs, isTransientPredicate, ...)` | Async void operation (delegates to the generic overload) |
| `ExecuteWithRetry<T>(Func<T>, maxRetries, retryDelayMs, isTransientPredicate, ...)` | Synchronous operation with return value |

The `isTransientPredicate` parameter is a `Func<Exception, (bool isTransient, string? errorCode)>`. Each DAL passes its own `IsTransient` method (overridden from `DalBase`) as this predicate. External DAL implementations can supply their own transient error detection logic. See [External DAL Development](../09-extending/external-dal-development.md) for usage.

All overloads accept an optional `RetryLogCallback` delegate for logging retry attempts:

```csharp
public delegate void RetryLogCallback(
    int attempt, int maxAttempts, string? errorCode,
    string operationDescription, int delayMs);
```

### Recognized Transient Errors

**SQL Server:**
- `-2`: Timeout expired
- `20`: Instance connection error (broken TDS / encryption negotiation failure)
- `64`: Connection established but lost (`ERROR_NETNAME_DELETED`)
- `233`: Connection closed during initialization (pool exhaustion / server busy)
- `10053`, `10054`, `10060`: Network-related errors
- `40197`, `40501`, `40613`: Azure SQL service errors
- `49918`, `49919`, `49920`: Azure SQL resource/throttling errors

**PostgreSQL** (SQLSTATE codes via `Npgsql.PostgresException`):
- `08000`, `08001`, `08003`, `08004`, `08006`: Connection exceptions
- `57P01`, `57P02`, `57P03`: Server shutdown/unavailable
- `40001`, `40P01`: Serialization/deadlock

**MariaDB** (`MySqlConnector.MySqlException`):
- `1040`: Too many connections
- `1205`, `1213`: Lock/deadlock errors
- `1614`: Transaction branch was rolled back
- `2002`, `2003`, `2006`, `2013`, `2055`: Connection errors

**MySQL** (`MySqlConnector.MySqlException`):
- `1040`: Too many connections
- `1205`, `1213`: Lock/deadlock errors
- `1614`: Transaction branch was rolled back
- `2002`, `2003`, `2006`, `2013`, `2055`: Connection errors

**SQLite:**
- `5`: SQLITE_BUSY (database file is locked)
- `6`: SQLITE_LOCKED (table in the database is locked)

**Common (all providers):**
- `TimeoutException`: Treated as transient regardless of database provider

> **Note**: `DalBase.IsTransient` recursively checks inner exceptions. If the top-level exception is not recognized as transient, the inner exception chain is inspected via `base.IsTransient(ex.InnerException)`.

### Retry Behavior

```
Attempt 1: Execute operation
    ├── Success → Return result
    └── Transient error → Wait 250ms, retry

Attempt 2: Execute operation
    ├── Success → Return result
    └── Transient error → Wait 500ms, retry

Attempt 3: Execute operation
    ├── Success → Return result
    └── Transient error → Wait 750ms, retry

Attempt 4: Fail → Throw RetryExhaustedException
```

## Orphaned Run Detection

### What is an Orphaned Run?

An orphaned run occurs when:
- A migration process crashes without cleanup
- The `MigrationRun` record remains in "Running" status
- The `FinishedAt` column is never updated

### Detection Mechanism

On startup, RayMigrator checks for orphaned runs:

```sql
SELECT * FROM MigrationRun
WHERE MigrationRunResultId = 10  -- Running
  AND FinishedAt IS NULL
```

Note: Age-based filtering is not performed at the SQL level. It is handled in the service layer via the `--older-than` CLI option (default 60 minutes).

### Automatic Orphaned Run Cleanup

`MigrationService` wraps the `RepositoryMigrationRunInsert` call in `RepositoryMigrationRunInsertWithAutoFix`. When the insert fails with `MigrationAlreadyRunningException`:

1. The method queries for orphaned runs via `RepositoryMigrationRunSelectOrphaned`
2. Runs older than `AutoFixOrphanedRunsThresholdMinutes` (10 minutes) are auto-fixed (set to `Error` status)
3. The insert is retried once
4. If no orphaned runs are found or the retry fails, the original exception is rethrown

This auto-fix runs on every `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync` call, ensuring that stale orphaned runs do not permanently block new migrations.

### Resolution Options

When an orphaned run is detected and auto-fix does not apply (run is newer than 10 minutes):

1. **Wait**: The run may still be genuinely running
2. **Investigate manually**: Check database state before proceeding
3. **Fix command**: Use `raymigrator Fix --product <alias> --environment <env> --scope OrphanedRuns` for manual cleanup (default threshold: 60 minutes)

## Exception Types

RayMigrator defines specific exceptions for recovery scenarios:

### DatabaseParameterException

Thrown when database parameter conversion fails:

```csharp
throw new DatabaseParameterException(
    "Failed to convert parameters",
    parameterCount: 5);
```

### MigrationAlreadyRunningException

Thrown when attempting concurrent migrations for the same product:

```csharp
throw new MigrationAlreadyRunningException(
    "Another migration is already running",
    productId: 42,
    existingMigrationRunId: 123);
```

### MigrationRecoveryException

Thrown during recovery operations:

```csharp
throw new MigrationRecoveryException(
    "Cannot resume: migration file has changed",
    migrationRunId: (int?)123,
    migrationRecordId: (int?)456);
```

Properties: `MigrationRunId` (int?), `MigrationRecordId` (int?)

### DatabaseTransientException

Thrown when a transient database error occurs and all retry attempts have been exhausted:

```csharp
throw new DatabaseTransientException(
    "Transient error after retries",
    attemptsMade: 3,
    lastErrorCode: "10060");
```

Properties: `AttemptsMade` (int), `LastErrorCode` (string?)

**Location**: `Raycoon.RayMigrator.Shared/Exceptions/CustomExceptions.cs`

### RetryExhaustedException

Thrown when all retry attempts are exhausted (from within the retry loop):

```csharp
throw new RetryExhaustedException(
    "Operation failed after 3 attempts",
    attemptsMade: 3,
    lastErrorCode: "10060");
```

Properties: `AttemptsMade` (int), `LastErrorCode` (string?)

**Location**: `Raycoon.RayMigrator.Database.Common/RetryHelper.cs`

## Configuration

### Retry Settings

Configure retry behavior via TargetDefaults or individual Targets:

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "TargetGroupDefaults": {
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 3,
          "DbCommandWaitTimeInMsBeforeRetry": 250
        }
      }
    }
  }
}
```

The same properties also exist on `RepositoryOptions` for repository database operations, but with different defaults:

| Setting | TargetDefaults | TargetOptions (annotation) | Effective Target Default | RepositoryOptions |
|---------|---------------|---------------------------|-------------------------|-------------------|
| `DbCommandTimeoutInSeconds` | 20 | 20 | 20 | 60 |
| `DbCommandMaxRetries` | 0 (disabled) | 0 (disabled) | 0 (disabled) | 100 |
| `DbCommandWaitTimeInMsBeforeRetry` | 250 | 500 | 250 | 250 |

> **Note**: The "Effective Target Default" column shows the value after `ProductDefaultsPostConfigureOptions` merges `TargetDefaults` into each `TargetOptions`. Since `TargetDefaults` (250) is always present, individual targets effectively default to 250, not the annotation default of 500. See [Target Options](../06-configuration-reference/target-options.md) for details.

Repository retry is active. `RepositoryExtensions.GetDalSettings()` (in `Raycoon.RayMigrator.Infrastructure/RepositoryExtensions.cs`) builds the `DalSettings` passed to every repository template call. After startup validation, `DbCommandMaxRetries` is already populated with the configured value (annotation default: 100), so the effective repository retry count is 100 by default. See [Repository Options](../06-configuration-reference/repository-options.md) for details.

### Orphan Detection

There are two orphan age thresholds:

- **Auto-fix threshold**: 10 minutes (`AutoFixOrphanedRunsThresholdMinutes` in `MigrationService`). Used during automatic orphaned run cleanup on migration start.
- **Fix command threshold**: 60 minutes (default `--older-than` option). Used by the manual `Fix` command.

## Best Practices

### 1. Design for Resumability

Structure migration files so blocks are idempotent where possible:

```sql
-- Good: Can be safely re-run
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (...)
END
GO

-- Avoid: Will fail on retry
CREATE TABLE Users (...)
GO
```

### 2. Use Transactions Appropriately

Enable transactions for related operations:

```sql
/*
[RayMigrator]
UseTransaction = true
*/
```

Note: DDL statements in MariaDB and MySQL have limited transaction support.

### 3. Monitor for Orphaned Runs

Periodically check for orphaned runs, especially in production:

```sql
SELECT * FROM MigrationRun
WHERE MigrationRunResultId = 10
  AND FinishedAt IS NULL;
```

### 4. Test Recovery Scenarios

Include recovery testing in your CI/CD pipeline:
- Simulate process crashes during migration
- Test resume functionality
- Verify block-level tracking accuracy

## Related Topics

- [Migration State Machine](migration-state-machine.md)
- [Error Handling](error-handling.md)
- [Concurrency Control](concurrency-control.md)
- [Fix Command](../08-cli-reference/command-reference.md#fix) - CLI reference for orphaned run cleanup
- [Target Options](../06-configuration-reference/target-options.md) - Retry configuration per target
