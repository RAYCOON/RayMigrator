# Resilience and Recovery

> **Implementation Status**: FULLY IMPLEMENTED — see [Open Features](../appendix/open-features.md#f3-recovery-orchestration)
>
> `RetryHelper` with transient error detection is fully functional. Block-level resume (`FindResumableBlock`) is implemented and active in `MigrationService` — on re-run, migrations resume from the last failed block automatically. Interrupted migration detection (`RepositoryMigrationGetInterruptedAsync`) is called during `MigrateUpAsync` startup and logs a warning when detected. Orphaned run detection (`RepositoryMigrationRunSelectOrphanedAsync`) is implemented and available via the `fix` command for manual cleanup. Automatic orphaned run cleanup is implemented via `RepositoryMigrationRunInsertWithAutoFix` — when a migration run insert fails due to `MigrationAlreadyRunningException`, open runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` (default 2) are automatically fixed and the insert is retried once. A host or the CLI can cancel a run, which stops at the next block boundary and resumes like a failed file (see [Error Handling: Cancellation](error-handling.md#cancellation)).

This document describes RayMigrator's fault tolerance mechanisms and automatic recovery capabilities.

## Overview

RayMigrator is designed to handle failures gracefully at multiple levels:

1. **Transient Database Errors**: Automatic retry with linear backoff
2. **Mid-Migration Failures**: Block-level state persistence for recovery
3. **Process Crashes**: Interrupted migration detection and resume capability
4. **Orphaned Runs**: Automatic detection and cleanup of stuck migrations (auto-fix on insert + manual Fix command)
5. **Cancellation**: A cancelled run stops at the next block boundary, keeps its committed blocks and records `Cancelled`; Ctrl+C and SIGTERM cancel the CLI run the same way

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
| `FileUpBlocksMigrated` | Number of leading blocks that committed on the target; it holds them at every moment of a run (a block after an ignored failure does not count, also when the file ends, a file that a CLI tool runs counts 0 while the tool runs, a resumed file shows the blocks it resumes after from its start), so a run that ends abruptly leaves a counter the next start can trust |
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
| `ExecuteWithRetryAsync<T>(Func<Task<T>>, maxRetries, retryDelayMs, isTransientPredicate, ..., cancellationToken)` | Async operation with return value |
| `ExecuteWithRetryAsync(Func<Task>, maxRetries, retryDelayMs, isTransientPredicate, ..., cancellationToken)` | Async void operation (delegates to the generic overload) |
| `ExecuteWithRetry<T>(Func<T>, maxRetries, retryDelayMs, isTransientPredicate, ...)` | Synchronous operation with return value, without a token: it serves the synchronous `IDal.ExecuteNonQuery` of the database log writer's background thread |

The asynchronous overloads take `CancellationToken cancellationToken = default` as their last parameter. Once the token is cancelled, a failure is neither classified nor retried and the wait before a retry ends with an `OperationCanceledException`; the operation receives the token itself, typically through its closure. `DalBase.IsTransient` never counts a cancellation as transient, so no layer starts another attempt for a cancelled call.

The `isTransientPredicate` parameter is a `Func<Exception, (bool isTransient, string? errorCode)>`. Each DAL passes its own `IsTransient` method (overridden from `DalBase`) as this predicate. External DAL implementations can supply their own transient error detection logic. See [External DAL Development](../09-extending/external-dal-development.md) for usage.

All overloads accept an optional `RetryLogCallback` delegate for logging retry attempts:

```csharp
public delegate void RetryLogCallback(
    int attempt, int maxAttempts, string? errorCode,
    string operationDescription, int delayMs);
```

### Recognized Transient Errors

The lists below are the built-in defaults of each DAL. A `TransientErrorCodes.txt` in `DataAccessLayers/{DatabaseType}/` next to the executable replaces the list without a new release; see [Transient Error Codes](../03-database-layer/transient-error-codes.md).

**SQL Server:**
- `-2`: Timeout expired
- `20`: Instance connection error (broken TDS / encryption negotiation failure)
- `64`: Connection established but lost (`ERROR_NETNAME_DELETED`)
- `233`: Connection closed during initialization (pool exhaustion / server busy)
- `10053`, `10054`, `10060`: Network-related errors
- `40197`, `40501`, `40613`: Azure SQL service errors
- `49918`, `49919`, `49920`: Azure SQL resource/throttling errors
- `4021`, `596`: Pooled session killed by the server because the login's state changed after the session logged in (a migration ran `ALTER LOGIN` on the login RayMigrator uses: default language, default database, ...). Error 4021 is raised only on reuse of a pooled connection; a fresh login either succeeds or fails with a different, non-transient code. Besides retrying, the SQL Server DAL clears the connection pool for its connection string when either number appears in the error chain, so the next attempt logs in afresh instead of failing once per poisoned pooled connection.

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
- `TimeoutException`: Treated as transient regardless of database provider; two callers do not retry a timeout all the same (see [Calls That Do Not Retry](#calls-that-do-not-retry))

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

### Calls That Do Not Retry

Two callers bypass these retries, because a retry would wait out a timeout again:

- **Initialization of the database log.** `DatabaseLogWriter.InitDatabaseLoggerAsync` runs `DatabaseLogging_CheckCreate`, which since 0.20.0 also builds the two indexes of the log table over the rows already there. It retries a transient failure up to three times, as every log write, but not a timeout: `DalBase.IsTimeout` (virtual) recognises a `TimeoutException` in the chain of inner exceptions, as Npgsql reports a timeout; the SQL Server DAL adds the error -2 of SqlClient, the MySQL and MariaDB DALs the `CommandTimeoutExpired` of MySqlConnector, and a cancellation is never a timeout. The attempt was rolled back whole, so a retry would build the indexes from the start again and hold back the log writes of other hosts once more. The start fails with a message that names the index build and `DatabaseLogging.DbCommandTimeoutInSeconds` (see [Logging Schema: Indexes](../03-database-layer/logging-schema.md#indexes)).
- **Retention.** `IRepositoryMaintenanceService` runs its templates and its check of the schema version with the command timeout of the repository but without its retries (`DbCommandMaxRetries`): a statement that timed out, and on SQLite one that reports BUSY after its busy timeout, would wait the whole command timeout again on every retry, and the default of 100 retries turned a time budget of a minute into hours. The next batch and the next call are the retry; an error after a batch deleted rows ends the call with `Completed = false` and the error in its message (see [Repository Schema: Retention](../03-database-layer/repository-schema.md#retention)).

## Orphaned Run Detection

### What is an Orphaned Run?

An orphaned run occurs when:
- A migration process crashes without cleanup
- The `MigrationRun` record remains in "Running" status
- The `FinishedAt` column is never updated

### Detection Mechanism

A running migrate-up, migrate-down or baseline refreshes `MigrationRun.LastHeartbeatAt` every `Repository.HeartbeatIntervalInSeconds` (default 30). When a new run finds an open run of the same product and environment, RayMigrator checks for orphaned runs:

```sql
SELECT * FROM MigrationRun
WHERE MigrationRunResultId = 10  -- Running
  AND FinishedAt IS NULL
```

Note: Age-based filtering is not performed at the SQL level. The template returns `MinutesSinceHeartbeat` (the age of the last heartbeat, or of the start for a run without one), and the service compares it with `Repository.OrphanedRunThresholdInMinutes` (default 2) for the auto-fix and with the `--older-than` CLI option (default 2) for the `fix` command.

### Automatic Orphaned Run Cleanup

`MigrationService` wraps the `RepositoryMigrationRunInsertAsync` call in `RepositoryMigrationRunInsertWithAutoFix`. When the insert fails with `MigrationAlreadyRunningException`:

1. The method queries for orphaned runs via `RepositoryMigrationRunSelectOrphanedAsync`
2. Runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` (default 2) are auto-fixed (set to `Error` status, their open records to `NotMigrated`); `Repository_MigrationRun_FixOrphaned` refuses a run that wrote a heartbeat since the select (`-31`), which stays open
3. The insert is retried once
4. If no orphaned run could be fixed, the original exception is rethrown; a retry that fails throws its own exception

This auto-fix runs on every `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync` call, ensuring that stale orphaned runs do not permanently block new migrations.

### Resolution Options

When an open run is detected and auto-fix does not apply (its last heartbeat is younger than the threshold):

1. **Wait**: The run may still be genuinely running
2. **Investigate manually**: Check database state before proceeding
3. **Fix command**: Use `raymigrator fix --product <alias> --environment <env> --scope orphanedruns` for manual cleanup (default threshold: 2 minutes since the last heartbeat, `--older-than 0` closes every open run)

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

Repository retry is active. `RepositoryExtensions.GetDalSettings()` (in `Raycoon.RayMigrator.Infrastructure/RepositoryExtensions.cs`) builds the `DalSettings` passed to every repository template call of a command. After startup validation, `DbCommandMaxRetries` is already populated with the configured value (annotation default: 100), so the effective repository retry count is 100 by default. The retention of `IRepositoryMaintenanceService` takes the same settings without the retries (see [Calls That Do Not Retry](#calls-that-do-not-retry)). See [Repository Options](../06-configuration-reference/repository-options.md) for details.

### Orphan Detection

Both orphan age thresholds measure the age of the last heartbeat (or of the start, for a run without one):

- **Auto-fix threshold**: `Repository.OrphanedRunThresholdInMinutes` (default 2, about four missed heartbeats at the default `Repository.HeartbeatIntervalInSeconds` of 30 seconds; an interval of `0` switches the heartbeat off, and such a run counts from its start). Used during automatic orphaned run cleanup on migration start.
- **Fix command threshold**: the `--older-than` option (default 2). Used by the manual `fix` command.

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
