# Concurrency Control

This document describes how RayMigrator prevents race conditions and ensures safe concurrent operation.

## Overview

RayMigrator enforces **exclusive migration runs** per product, environment, and run mode combination. Only one migration process can execute for a given combination at any time. This is enforced at the database level using engine-specific locking mechanisms, ensuring safety across multiple processes and machines. All five supported engines (SQL Server, PostgreSQL, MariaDB, MySQL, SQLite) implement this pattern.

## Exclusive Run Guarantee (Database-Level)

### How It Works

Before starting a migration, RayMigrator:

1. Checks for existing "Running" status migrations for the product
2. Attempts to create a new MigrationRun with "Running" status
3. If another run exists, the operation fails immediately

```
Process A                          Process B
---------                          ---------
Check for Running run → None
                                   Check for Running run → None
Create MigrationRun (Running) ✓
                                   Create MigrationRun (Running) ✗
                                   (Fails: Another run already exists)
Execute migrations...
Update MigrationRun (Ok) ✓
                                   Retry: Check for Running run → None
                                   Create MigrationRun (Running) ✓
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
          AND [MigrationRunModeId] = @MigrationRunModeId
          AND [FinishedAt] IS NULL
    )
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT '-2,MigrationRun for Product [...] with Id [...] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=...] are not allowed!';
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
ON [{SchemaName}].[MigrationRun] (ProductId, Environment, MigrationRunModeId)
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
→ Auto-fix checks for orphaned runs older than 10 minutes
→ If none found: Process B must wait or use Fix command to clean up
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
Process A crashes (MigrationRun remains "Running")
Process B attempts migration for ProductA (after > 10 minutes)
→ Process B receives MigrationAlreadyRunningException (ResultCode -2)
→ Auto-fix detects orphaned run older than 10 minutes, fixes it automatically
→ Process B retries insert and succeeds
```

If the orphaned run is newer than 10 minutes, Process B receives the exception and the CLI logs a recommendation to use the Fix command.

### Scenario 4: Same Product, Different Environments

```
Process A migrates ProductA in "DEV" environment
Process B migrates ProductA in "PROD" environment
→ Both processes run independently
→ No conflicts (different ProductId + Environment + RunMode combinations)
```

The concurrency check includes the `Environment` parameter, so the same product can be migrated simultaneously in different environments.

## Error Handling

### Guard Error Flow

When a concurrent migration is detected, the SQL template returns ResultCode `-2`. The `TemplateExecutor.ExecuteScalarWithNegativeResultCodeException` method throws a `TemplateResultException` for any negative ResultCode. The `RepositoryMigrationRunInsert` method catches the `TemplateResultException` when `ResultCode == -2` and wraps it in a `MigrationAlreadyRunningException`:

```csharp
// In TemplateExecutor.RepositoryMigrationRunInsert():
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
Parallel migrations for the same product with MigrationRunModeId [Migrate=100] are not allowed!
```

This exception is initially handled by `RepositoryMigrationRunInsertWithAutoFix` in `MigrationService`, which attempts automatic cleanup of orphaned runs older than 10 minutes. If auto-fix succeeds, the insert is retried and the migration proceeds. If auto-fix does not apply (no orphaned runs or runs are too recent), the exception propagates to `RayMigratorService` which logs a recommendation to use the Fix command.

### Resolving Blocked Migrations

Options when encountering this error:

1. **Automatic**: `RepositoryMigrationRunInsertWithAutoFix` automatically cleans up orphaned runs older than 10 minutes and retries the insert. This happens transparently during `MigrateUpAsync`, `MigrateDownAsync`, and `BaselineAsync`.
2. **Wait**: Allow the other process to complete
3. **Investigate**: Check if the other process is actually running
4. **Fix Command**: Use the Fix command to clean up orphaned runs manually (especially those newer than 10 minutes):
   ```
   RayMigrator Fix --product <alias> --environment <env> --scope OrphanedRuns
   ```

## Implementation Details

### Run Lifecycle

```
1. RepositoryMigrationRunInsertWithAutoFix()
   └── Calls RepositoryMigrationRunInsert()
       └── Creates MigrationRun with MigrationRunResultId = Running (10)
           └── Fails if another Running run exists
   └── On failure: auto-fixes orphaned runs > 10 min, retries once

2. Execute migrations...
   └── Each block updates progress

3. RepositoryMigrationRunUpdate()
   └── Updates MigrationRunResultId to Ok (100) or Error (90)
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
    ├─── Success ────► [Ok] ────► (FinishedAt set, lock released)
    │
    ├─── Error ──────► [Error] ─► (FinishedAt set, lock released)
    │
    └─── Crash ──────► [Running] (Orphaned - requires manual cleanup)
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
    DATEDIFF(MINUTE, [StartedAt], SYSUTCDATETIME()) AS MinutesRunning
FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
WHERE [ProductId] = @ProductId
    AND [EnvironmentId] = @EnvironmentId
    AND [MigrationRunResultId] = 10  -- Running
    AND [FinishedAt] IS NULL
ORDER BY [StartedAt];
```

Note: The template filters by `ProductId` and `EnvironmentId` and does not apply a timeout threshold -- all orphaned runs are returned regardless of how long they have been running.

For ad-hoc queries to list all running migrations across all products:

```sql
SELECT
    mr.Id,
    mr.ProductId,
    mr.EnvironmentId,
    mr.StartedAt,
    DATEDIFF(MINUTE, mr.StartedAt, SYSUTCDATETIME()) AS MinutesRunning
FROM MigrationRun mr
WHERE mr.MigrationRunResultId = 10  -- Running
  AND mr.FinishedAt IS NULL
ORDER BY mr.StartedAt;
```

### Manual Cleanup

To manually terminate an orphaned run:

```sql
UPDATE MigrationRun
SET
    MigrationRunResultId = 90,  -- Error
    FinishedAt = SYSUTCDATETIME(),
    DurationInMs = DATEDIFF(MILLISECOND, StartedAt, SYSUTCDATETIME())
WHERE Id = @OrphanedMigrationRunId;
```

### Automatic Cleanup on Migration Start

`MigrationService.RepositoryMigrationRunInsertWithAutoFix` automatically detects and cleans up orphaned runs older than 10 minutes when a new migration run is started. This covers the common case where a previous process crashed and left an orphaned run. The auto-fix uses the same `RepositoryMigrationRunFixOrphaned` template as the manual Fix command.

### Manual Cleanup via Fix Command

The `Fix` command identifies orphaned runs using `RepositoryMigrationRunSelectOrphaned` and fixes them using `RepositoryMigrationRunFixOrphaned` (sets `MigrationRunResultId` to Error and `FinishedAt` to current UTC time). It also fixes orphaned MigrationRecord entries via `RepositoryMigrationRecordFixOrphaned`. The default age threshold is 60 minutes (`--older-than` option), which is more conservative than the 10-minute auto-fix threshold.

## Best Practices

### 1. Coordinate Migration Timing

In CI/CD pipelines, ensure migrations run sequentially:

```yaml
# Good: Sequential migrations
jobs:
  migrate:
    steps:
      - run: RayMigrator Migrate-Up --product MyProduct

# Avoid: Parallel migrations for same product
jobs:
  migrate-1:
    steps:
      - run: RayMigrator Migrate-Up --product MyProduct
  migrate-2:  # Runs in parallel - will fail!
    steps:
      - run: RayMigrator Migrate-Up --product MyProduct
```

### 2. Use Process Managers

In production, use process managers that handle graceful shutdown:

- systemd with `TimeoutStopSec`
- Kubernetes with `terminationGracePeriodSeconds`
- Docker with `stop_grace_period`

### 3. Monitor for Orphans

Set up monitoring for orphaned runs:

```sql
-- Alert if any run has been "Running" for > 1 hour (SQL Server example)
SELECT COUNT(*) AS OrphanedCount
FROM MigrationRun
WHERE MigrationRunResultId = 10
  AND FinishedAt IS NULL
  AND DATEDIFF(MINUTE, StartedAt, SYSUTCDATETIME()) > 60;
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
        "RayMigrator Fix --product {Product} --environment {Environment} --scope OrphanedRuns",
        _consoleOptions.Product, _consoleOptions.Environment);
    return 1;
}
```

## Related Topics

- [Resilience and Recovery](resilience.md)
- [Migration State Machine](migration-state-machine.md)
- [Error Handling](error-handling.md)
- [Fix Command](../08-cli-reference/command-reference.md#fix) - CLI reference for orphaned run cleanup
