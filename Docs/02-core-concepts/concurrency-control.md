# Concurrency Control

This document describes how RayMigrator prevents race conditions and ensures safe concurrent operation.

## Overview

RayMigrator enforces **exclusive migration runs** per product and environment combination. Only one migration process can execute for a given combination at any time. The run mode is not part of the predicate: only Migrate runs write a MigrationRun row (Validate and Simulate never do), so the guard blocks any second run for the same product and environment while one is unfinished. This is enforced at the database level using engine-specific locking mechanisms, ensuring safety across multiple processes and machines. All five supported engines (SQL Server, PostgreSQL, MariaDB, MySQL, SQLite) implement this pattern.

## Exclusive Run Guarantee (Database-Level)

### How It Works

Before starting a migration, RayMigrator:

1. Takes the run lock of its engine for the product and environment (see [Database Enforcement](#database-enforcement))
2. Checks under that lock for an unfinished run (`FinishedAt IS NULL`) of the product and environment
3. Creates the new MigrationRun with "Running" status only when there is none; otherwise the template returns `-2` and the start fails, unless the open run is orphaned and the auto-fix closes it (see [Automatic Cleanup on Migration Start](#automatic-cleanup-on-migration-start))

```
Process A                               Process B
---------                               ---------
Take the run lock ✓
Check for an unfinished run → none
                                        Take the run lock: waits for A (SQL Server,
                                        PostgreSQL, SQLite); MySQL and MariaDB
                                        return -2 at once
Create MigrationRun (Running) ✓
Commit, release the lock
                                        Check for an unfinished run → A's run
                                        Return -2 ✗ (migration already running)
Execute migrations...
Update MigrationRun (Ok) ✓
                                        Next start: check → none,
                                        create MigrationRun (Running) ✓
```

### Database Enforcement

The `Repository_MigrationRun_Insert.sql` template checks for existing unfinished runs before inserting. Each database engine uses its own locking strategy:

**SQL Server** uses `UPDLOCK, HOLDLOCK` table hints within a transaction:

```sql
BEGIN TRANSACTION;
    IF EXISTS (
        SELECT TOP (1) 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] WITH (UPDLOCK, HOLDLOCK)
        WHERE [ProductId] = @ProductId
          AND [EnvironmentId] = @EnvironmentId
          AND [FinishedAt] IS NULL
    )
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT '-2,MigrationRun for Product [...] with Id [...] is currently in progress. Parallel migrations for the same product and environment are not allowed!';
        RETURN;
    END;
    -- INSERT new MigrationRun ...
COMMIT TRANSACTION;
```

**PostgreSQL** uses `pg_advisory_xact_lock` for transaction-scoped advisory locking:

```sql
PERFORM pg_advisory_xact_lock(hashtext('MigrationRun_' || CAST(@ProductId AS TEXT)));
-- Then checks for existing unfinished run and inserts if none found
```

**MariaDB and MySQL** use `GET_LOCK()` for cross-process advisory locking with immediate timeout:

```sql
SET @v_lock_name = CONCAT('RayMigrator_Run_', CAST(@ProductId AS CHAR), '_', CAST(@EnvironmentId AS CHAR));
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 0);
-- Then checks for existing unfinished run and inserts if lock acquired and no run found
DO RELEASE_LOCK(@v_lock_name);
```

`GET_LOCK()` belongs to the session, not to a transaction. When a template fails, times out or is cancelled between its `GET_LOCK()` and its `RELEASE_LOCK()`, the MySQL and MariaDB DALs release the locks of the session (`DO RELEASE_ALL_LOCKS()`) before the connection goes back to the pool, so the lock does not hold back another process until the pool reuses or prunes the connection (up to 180 seconds). The same holds for the `GET_LOCK()` of the repository schema steps and, on MySQL, of the log initialization.

**SQLite** opens a write transaction of its own, because the repository runs its templates without a transaction of the driver (`UseTransaction = false`), where every statement commits on its own:

```sql
BEGIN IMMEDIATE;
-- Then checks for an unfinished run, inserts the run when none was found (the INSERT checks
-- MigrationRun itself as well) and writes its MigrationRunMeta row
COMMIT;
```

`BEGIN IMMEDIATE` takes the write lock of the file before the check reads `MigrationRun`. A parallel start waits for that lock in the busy timeout, the command timeout of the repository (`DbCommandTimeoutInSeconds`, 60 seconds by default), and then finds the open run; a failed statement before the `COMMIT` is rolled back by the DAL, so a pooled connection does not keep the lock. Before 0.20.0 the check and the insert were statements that committed one by one: two starts at the same moment could both find no open run and both migrate the same targets. Two processes on one host are safe through this transaction; a SQLite repository on a network file system stays unsupported.

**Lock names.** The schema steps of the repository (`Repository_CheckCreate`, `Repository_Upgrade_1` to `_3`) take the lock `CONCAT('raymigrator_schema_', DATABASE())`, and `Repository_MigratorMeta_CheckInsert` takes `CONCAT('raymigrator_meta_', DATABASE())`: both names carry the whole name of the repository database. MySQL refuses a lock name of more than 64 characters with error 4163 (`User-level lock name '...' should not exceed 64 characters.`, measured on MySQL 8.4), so on MySQL every command fails at its start when the repository database has a name of 46 characters or more (the version lock alone would allow 47). MariaDB accepts lock names of up to 192 bytes and refuses longer ones with error 1059 (`Identifier name '...' is too long`, measured on MariaDB 11.6); an ASCII database name, at most 64 characters, never reaches that, only a name of more than 173 bytes in UTF-8. Give a MySQL repository a database name of at most 45 characters. The lock of the log initialization on MySQL (`DatabaseLogging_CheckCreate`) cuts the database name to 48 characters (`raymigrator_log_` plus at most 48) and stays within the limit, and the run lock `RayMigrator_Run_{ProductId}_{EnvironmentId}` is short. See [Troubleshooting](../appendix/troubleshooting.md#mysql-database-name-too-long-for-the-schema-lock).

### ResultCode Convention

| ResultCode | Meaning |
|------------|---------|
| `>= 0` | Success (value is e.g. the new MigrationRunId) |
| `-1` | General template error (e.g. INSERT failed) |
| `-2` | Migration already active (parallel run / lock conflict) |

### Optional Schema Enforcement (SQL Server)

For additional safety on SQL Server, you can add a filtered unique index:

```sql
CREATE UNIQUE INDEX UX_MigrationRun_Product_Running
ON [{SchemaName}].[MigrationRun] (ProductId, EnvironmentId)
WHERE FinishedAt IS NULL;
```

Benefits:
- Database engine enforces constraint automatically
- Works across all sessions/connections
- Zero code overhead for locking

Note: Filtered indexes are a SQL Server feature. PostgreSQL supports partial indexes with similar syntax. MariaDB and MySQL do not support filtered/partial indexes.

## Scenarios

### Scenario 1: Parallel Migration Attempts (Same Product)

```
Process A starts migration for ProductA
Process B attempts migration for ProductA
→ Process B receives MigrationAlreadyRunningException (ResultCode -2)
→ Auto-fix checks for an open run whose last heartbeat is older than Repository.OrphanedRunThresholdInMinutes (default 2)
→ If none found (Process A is alive and writes its heartbeat): Process B must wait or use Fix command to clean up
```

### Scenario 2: Parallel Migrations (Different Products)

```
Process A migrates ProductA
Process B migrates ProductB (simultaneously)
→ Both processes run independently
→ No conflicts
```

### Scenario 3: Process Crash

```
Process A starts migration for ProductA
Process A crashes (MigrationRun remains "Running", its heartbeat stops)
Process B attempts migration for ProductA (last heartbeat of A older than 2 minutes)
→ Process B receives MigrationAlreadyRunningException (ResultCode -2)
→ Auto-fix detects the orphaned run, closes it as Error and settles its open records by their progress
  (Failed with the committed blocks, Migrated when every block committed, NotMigrated without progress)
→ Process B retries insert and succeeds
```

If the last heartbeat of the orphaned run is younger than `Repository.OrphanedRunThresholdInMinutes`, Process B receives the exception and the CLI logs a recommendation to use the Fix command. A running migrate-up, migrate-down or baseline refreshes `MigrationRun.LastHeartbeatAt` every `Repository.HeartbeatIntervalInSeconds` (default 30), so a live run is not taken for an orphan, however long it runs, as long as the heartbeat is on and its interval stays well below the threshold (with `HeartbeatIntervalInSeconds` 0, or an interval at or above the threshold, a parallel start closes a live run once it is older than the threshold); a run that never wrote a heartbeat is measured from its start. A CLI process that a second Ctrl+C or SIGTERM ended at once leaves such an orphan, while the first signal stops the run cleanly as `Cancelled` and releases the lock (see [Error Handling: Cancellation](error-handling.md#cancellation)).

### Scenario 4: Same Product, Different Environments

```
Process A migrates ProductA in "DEV" environment
Process B migrates ProductA in "PROD" environment
→ Both processes run independently
→ No conflicts (different ProductId + EnvironmentId combinations)
```

The concurrency check includes the `Environment` parameter, so the same product can be migrated simultaneously in different environments.

## Error Handling

### Guard Error Flow

When a concurrent migration is detected, the SQL template returns ResultCode `-2`. The `TemplateExecutor.ExecuteScalarWithNegativeResultCodeExceptionAsync` method throws a `TemplateResultException` for any negative ResultCode. The `RepositoryMigrationRunInsertAsync` method catches the `TemplateResultException` when `ResultCode == -2` and wraps it in a `MigrationAlreadyRunningException`:

```csharp
// In TemplateExecutor.RepositoryMigrationRunInsertAsync():
catch (TemplateResultException ex) when (ex.ResultCode == -2)
{
    throw new MigrationAlreadyRunningException(
        ex.Message, _ctxAccessor.Current.MigrationState.ProductId);
}
```

The resulting exception message looks like:

```
MigrationAlreadyRunningException: RayMigrator aborted because another migration is already running.
Error executing template Repository_MigrationRun_Insert.
Template-execution returned a negative ResultCode [-2] with ErrorMessage:
MigrationRun for Product [MyProduct] with Id [42] is currently in progress.
Parallel migrations for the same product and environment are not allowed!
```

This exception is initially handled by `RepositoryMigrationRunInsertWithAutoFix` in `MigrationService`, which attempts automatic cleanup of open runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes`. If auto-fix succeeds, the insert is retried and the migration proceeds. If auto-fix does not apply (no orphaned runs, or every candidate wrote a heartbeat meanwhile), the exception propagates to `RayMigratorService` which logs a recommendation to use the Fix command.

### Resolving Blocked Migrations

Options when encountering this error:

1. **Automatic**: `RepositoryMigrationRunInsertWithAutoFix` automatically cleans up orphaned runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` (default 2) and retries the insert. This happens transparently during `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync`.
2. **Wait**: Allow the other process to complete
3. **Investigate**: Check if the other process is actually running
4. **Fix Command**: Use the Fix command to clean up orphaned runs manually (`--older-than` compares the age of the last heartbeat, default 2 minutes, `0` closes every open run):
   ```
   raymigrator fix --product <alias> --environment <env> --scope orphanedruns
   ```

## Implementation Details

### Run Lifecycle

```
1. RepositoryMigrationRunInsertWithAutoFix()
   └── Calls RepositoryMigrationRunInsertAsync()
       └── Creates MigrationRun with MigrationRunResultId = Running (10)
           └── Fails if another Running run exists
   └── On failure: auto-fixes runs whose last heartbeat is older than
       Repository.OrphanedRunThresholdInMinutes, retries once
   └── Starts the heartbeat (every Repository.HeartbeatIntervalInSeconds)

2. Execute migrations...
   └── Each block updates progress

3. RepositoryMigrationRunUpdateAsync()
   └── Updates MigrationRunResultId to Ok (100), PartialSuccess (50), Cancelled (70), Recovered (80) or Error (90)
   └── Sets FinishedAt timestamp
   └── "Releases" the exclusive lock
```

### State Diagram

```
[No Run]
    │
    ▼ (Create with Running status)
[Running]
    │
    ├─── Success ────► [Ok] / [PartialSuccess] ──► (FinishedAt set, lock released)
    │
    ├─── Cancel ─────► [Cancelled] ─────────────► (FinishedAt set, lock released)
    │
    ├─── Error ──────► [Recovered] / [Error] ───► (FinishedAt set, lock released)
    │
    └─── Crash ──────► [Running] (Orphaned: closed as Error by a later start once its
                                  heartbeat is older than the threshold, or by fix)
```

## Cleanup Procedures

### Identifying Stuck Runs

The `Repository_MigrationRun_SelectOrphaned.sql` template returns all orphaned runs for a given product and environment (SQL Server example):

```sql
SELECT
    [Id] AS MigrationRunId,
    [EnvironmentId],
    [StartedAt],
    [MigrationRunModeId],
    DATEDIFF_BIG(SECOND, [StartedAt], SYSUTCDATETIME()) / 60 AS MinutesRunning,
    [LastHeartbeatAt],
    DATEDIFF_BIG(SECOND, COALESCE([LastHeartbeatAt], [StartedAt]), SYSUTCDATETIME()) / 60 AS MinutesSinceHeartbeat,
    [InitiatedBy],
    [Host]
FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
WHERE [ProductId] = @ProductId
    AND [EnvironmentId] = @EnvironmentId
    AND [MigrationRunResultId] = 10  -- Running
    AND [FinishedAt] IS NULL
ORDER BY [StartedAt];
```

Note: The template filters by `ProductId` and `EnvironmentId` and applies no age threshold -- every open run is returned. The service compares `MinutesSinceHeartbeat` with the threshold: the auto-fix with `Repository.OrphanedRunThresholdInMinutes`, the `fix` command with `--older-than`.

For ad-hoc queries to list all running migrations across all products, with the age of their heartbeat that decides whether a run is orphaned:

```sql
SELECT
    mr.Id,
    mr.ProductId,
    mr.EnvironmentId,
    mr.StartedAt,
    DATEDIFF_BIG(SECOND, mr.StartedAt, SYSUTCDATETIME()) / 60 AS MinutesRunning,
    mr.LastHeartbeatAt,
    DATEDIFF_BIG(SECOND, COALESCE(mr.LastHeartbeatAt, mr.StartedAt), SYSUTCDATETIME()) / 60 AS MinutesSinceHeartbeat
FROM MigrationRun mr
WHERE mr.MigrationRunResultId = 10  -- Running
  AND mr.FinishedAt IS NULL
ORDER BY mr.StartedAt;
```

### Manual Cleanup

To manually terminate an orphaned run, provided it is still open and its heartbeat is older than the threshold (2 minutes by default), as `Repository_MigrationRun_FixOrphaned` checks it:

```sql
UPDATE MigrationRun
SET
    MigrationRunResultId = 90,  -- Error
    FinishedAt = SYSUTCDATETIME(),
    DurationInMs = DATEDIFF_BIG(MILLISECOND, StartedAt, SYSUTCDATETIME())
WHERE Id = @OrphanedMigrationRunId
  AND MigrationRunResultId = 10  -- Running
  AND FinishedAt IS NULL
  AND DATEDIFF_BIG(SECOND, COALESCE(LastHeartbeatAt, StartedAt), SYSUTCDATETIME()) / 60 >= 2;
```

The records of the run stay `Pending` or `Executing`; `fix` settles them as well (by their progress, or with `--last-migration-status`), so prefer it.

### Automatic Cleanup on Migration Start

`MigrationService.RepositoryMigrationRunInsertWithAutoFix` automatically detects and cleans up orphaned runs whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` (default 2) when a new migration run is started. This covers the common case where a previous process crashed, or a second Ctrl+C or SIGTERM ended it, and left an orphaned run. The auto-fix uses the same `Repository_MigrationRun_FixOrphaned` template as the manual Fix command, which refuses a run that wrote a heartbeat since the select (result `-31`), and settles the open records of a closed run by the progress of their files (`Repository_MigrationRecord_FixOrphaned` without a status): `Failed` with its committed blocks when some but not all committed, `Migrated` when every block committed, `NotMigrated` without progress; a rollback that was running ends `Failed` with its rollback blocks, or `NotMigrated` when every rollback block ran. The repair sets `FinishedAt` where it is empty and leaves `DurationInMs` as it is: a file that was running gets the time of the repair without a duration, a record whose rollback was running keeps the end and the duration of its last terminal status.

### Manual Cleanup via Fix Command

The `fix` command identifies orphaned runs using `RepositoryMigrationRunSelectOrphanedAsync` and fixes them using `RepositoryMigrationRunFixOrphanedAsync` (sets `MigrationRunResultId` to Error and `FinishedAt` to current UTC time, and, where the row has none, `ErrorCode` to `1006` (`OrphanedRunClosed`) and `ErrorMessage` to `Closed as orphaned by the repair: the run ended without finishing; last heartbeat <yyyy-MM-dd HH:mm:ss> UTC`). It also settles the open MigrationRecord entries of the run via `RepositoryMigrationRecordFixOrphanedAsync`: by their progress as the auto-fix does, or with the status of `--last-migration-status` on every one of them. The default age threshold is 2 minutes since the last heartbeat (`--older-than` option), the default of the auto-fix threshold; `--older-than 0` closes every open run.

## Best Practices

### 1. Coordinate Migration Timing

In CI/CD pipelines, ensure migrations run sequentially:

```yaml
# Good: Sequential migrations
jobs:
  migrate:
    steps:
      - run: raymigrator migrate-up --product MyProduct

# Avoid: Parallel migrations for same product
jobs:
  migrate-1:
    steps:
      - run: raymigrator migrate-up --product MyProduct
  migrate-2:  # Runs in parallel - will fail!
    steps:
      - run: raymigrator migrate-up --product MyProduct
```

### 2. Use Process Managers

In production, use process managers that handle graceful shutdown:

- systemd with `TimeoutStopSec`
- Kubernetes with `terminationGracePeriodSeconds`
- Docker with `stop_grace_period`

On Linux and macOS the CLI handles the SIGTERM of a stop like Ctrl+C: the run stops at the next block boundary, records `Cancelled` and exits with `130`. Give the grace period room for the block in progress (MySQL and SQLite may run it to its end, and MariaDB a block that waits in `SLEEP()`) and for the rollback of `--cancelled-action`; a `SIGKILL` after the grace period ends the process like a crash and leaves an orphaned run.

### 3. Monitor for Orphans

Set up monitoring for orphaned runs:

```sql
-- Alert if an open run wrote no heartbeat for Repository.OrphanedRunThresholdInMinutes (2 by default),
-- measured from its start while it has none (SQL Server example). A live run refreshes its heartbeat
-- every Repository.HeartbeatIntervalInSeconds, however long it runs.
SELECT COUNT(*) AS OrphanedCount
FROM MigrationRun
WHERE MigrationRunResultId = 10  -- Running
  AND FinishedAt IS NULL
  AND DATEDIFF_BIG(SECOND, COALESCE(LastHeartbeatAt, StartedAt), SYSUTCDATETIME()) / 60 >= 2;
```

### 4. Handle Exceptions Gracefully

The actual pattern used by `RayMigratorService`:

```csharp
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
```

## Related Topics

- [Resilience and Recovery](resilience.md)
- [Migration State Machine](migration-state-machine.md)
- [Error Handling](error-handling.md)
- [Fix Command](../08-cli-reference/command-reference.md#fix) - CLI reference for orphaned run cleanup
