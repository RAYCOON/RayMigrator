# Chapter 8: When Things Go Wrong

Every migration tool needs a solid error handling strategy. A failing migration in production without a plan is a nightmare scenario. RayMigrator gives you five distinct strategies to handle errors, so you can choose the one that fits your deployment philosophy.

For the complete technical reference of all error action modes, enum values, and state transitions, see [Error Handling](../02-core-concepts/error-handling.md).

---

## MigrationErrorAction Strategies

RayMigrator provides five error handling strategies, configured via the `MigrationErrorAction` property at the product level (or overridden per file via migsettings/TOML). For all enum values and codes, see [Error Handling — Error Action Modes](../02-core-concepts/error-handling.md#error-action-modes).

### Terminate (Default)

**When to use:** When you want manual control over recovery. Best for production environments where you want to assess the situation before acting.

```
Migration 001 ✓ (Migrated)
Migration 002 ✓ (Migrated)
Migration 003 ✗ (Failed)

Result:
  001 → Migrated — kept
  002 → Migrated — kept
  003 → Failed
  004 → never executed
```

> **Tip:** Terminate is the safest default. You can always run rollbacks manually after investigating the failure.

---

### Rollback

**When to use:** When you want all-or-nothing behavior. Either everything succeeds or nothing changes.

```
Migration 001 ✓ (Migrated)
Migration 002 ✓ (Migrated)
Migration 003 ✗ (Failed)

Rollback triggered:
  Rollback 003 (if rollback file exists)
  Rollback 002 ✓ → status: NotMigrated
  Rollback 001 ✓ → status: NotMigrated
```

> **Warning:** Rollback requires matching `.rollback.sql` files. If a rollback file is missing and `RequireRollbackFile` is `true`, the rollback itself will fail.

---

### RollbackErrorOnly

**When to use:** When migrations are independent and you want to keep successful work.

```
Migration 001 ✓ (Migrated — kept)
Migration 002 ✓ (Migrated — kept)
Migration 003 ✗ (Failed)

Rollback triggered:
  Rollback 003 only → status: NotMigrated

Result:
  001 → Migrated — kept
  002 → Migrated — kept
  003 → NotMigrated
```

---

### RollbackRelease

**When to use:** Multi-release runs where you want release-level atomicity.

```
Release 1.0:
  Migration 001 ✓ (Migrated — kept)
  Migration 002 ✓ (Migrated — kept)

Release 1.1:
  Migration 003 ✓ (Migrated)
  Migration 004 ✗ (Failed)

  Rollback triggered (Release 1.1 only):
    Rollback 004 (if exists)
    Rollback 003 ✓ → status: NotMigrated

Result:
  Release 1.0 → fully intact
  Release 1.1 → fully rolled back
```

### Ignore

**When to use:** When migrations are independent and you want the run to complete as much as possible, even if some files fail. Useful for data seeding or non-critical migrations.

```
Migration 001 ✓ (Migrated)
Migration 002 ✓ (Migrated)
Migration 003 ✗ (Failed — error ignored)
Migration 004 ✓ (Migrated — execution continues)
```

> **Warning:** With Ignore, the migration run will report as Error (90) since at least one file failed, but all other files will still be executed.

---

## RollbackErrorAction: When a Rollback Fails

When a rollback itself encounters an error (e.g., a rollback SQL block fails), the `RollbackErrorAction` setting controls what happens next. Since a failed rollback cannot itself be rolled back, only two strategies are available.

For the complete technical reference, see [Error Handling — Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling).

### Terminate (Default)

When a rollback SQL block fails, the entire rollback chain is aborted immediately. No further rollback files are executed.

### Ignore

When a rollback SQL block fails, it is logged as a warning and skipped. The rollback chain continues with the remaining blocks and files.

### Configuration

Configure via `RollbackErrorAction` at the same levels as `MigrationErrorAction`:

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "RollbackErrorAction": "Terminate"
    },
    "Products": [{
      "Alias": "BookStore",
      "RollbackErrorAction": "Ignore"
    }]
  }
}
```

> **Note:** `RollbackErrorAction` inherits through the same hierarchy as `MigrationErrorAction`: ProductDefaults -> Product -> migsettings -> TOML.

---

## Decision Guide

| Scenario | Recommended Strategy |
|----------|---------------------|
| Production deployment, manual oversight | Terminate |
| All-or-nothing deployment | Rollback |
| Independent, additive migrations | RollbackErrorOnly |
| Multi-release batch with release-level safety | RollbackRelease |
| Non-critical migrations, maximize completion | Ignore |

---

## Tutorial: Error Handling in Action

Let's continue with the BookStore project from earlier chapters. We will create a Release 1.1 that contains an intentional error, then observe how different error handling strategies respond.

### Step 1: Create Release 1.1 Migrations

Create the directory `Migrations/Release 1.1/Backend/` and add two migration files.

**File:** `Migrations/Release 1.1/Backend/001_AddCategories.sql`

```sql
/*
[RayMigrator]
Description = "Create Categories table"
UseTransaction = true
*/

CREATE TABLE [dbo].[Categories]
(
    [Id]    INT IDENTITY(1,1) NOT NULL,
    [Name]  NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
```

**File:** `Migrations/Release 1.1/Backend/001_AddCategories.rollback.sql`

```sql
/*
[RayMigrator]
Description = "Drop Categories table"
UseTransaction = true
*/

DROP TABLE IF EXISTS [dbo].[Categories];
```

**File:** `Migrations/Release 1.1/Backend/002_BadMigration.sql`

```sql
/*
[RayMigrator]
Description = "This will fail - references non-existent table"
UseTransaction = true
*/

ALTER TABLE [dbo].[NonExistentTable] ADD [Column1] INT;
```

**File:** `Migrations/Release 1.1/Backend/002_BadMigration.rollback.sql`

```sql
/*
[RayMigrator]
Description = "Rollback for bad migration"
UseTransaction = true
*/

-- Nothing to undo since the forward migration failed
SELECT 1;
```

### Step 2: Configure Rollback Strategy

In your `appsettings.json`, set the error handling strategy to `Rollback`:

```json
{
  "RayMigrator": {
    "Products": [{
      "Alias": "BookStore",
      "MigrationErrorAction": "Rollback",
      "MigrationFilesRootDirectory": "./Migrations",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "SqlServer",
        "Targets": [{
          "Alias": "BookStoreDB",
          "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}"
        }]
      }]
    }]
  }
}
```

### Step 3: Run the Migration

```bash
RayMigrator Migrate-Up -p BookStore -env Development -rm Migrate
```

**Expected result:** Migration 002 fails because the table `NonExistentTable` does not exist. With `Rollback` strategy, RayMigrator automatically rolls back Migration 001 (Categories), leaving the database unchanged from before the run.

### Step 4: Check the Result

```bash
RayMigrator Info -p BookStore -env Development
```

You will see that:
- Migration 002 has status `Failed` (30)
- Migration 001 has status `NotMigrated` (50) because it was rolled back
- The `Categories` table does not exist in the database

### Step 5: Fix and Retry

Fix the broken migration file, then run again:

```sql
/*
[RayMigrator]
Description = "Add CategoryId column to Books"
UseTransaction = true
*/

ALTER TABLE [dbo].[Books] ADD [CategoryId] INT NULL;
```

```bash
RayMigrator Migrate-Up -p BookStore -env Development -rm Migrate
```

Both migrations now succeed, and all records show status `Migrated` (100).

---

## Transaction Behavior per Database Engine

Understanding transaction support is critical for choosing your error handling strategy. For the complete database support matrix, see [SQL Dialects](../03-database-layer/sql-dialects.md).

Key takeaway: On **SQL Server**, **PostgreSQL**, and **SQLite**, DDL can be rolled back within a transaction. On **MariaDB** and **MySQL**, DDL causes an implicit commit — always write thorough rollback files for these engines.

---

## Migration Status After Errors

For the complete status values and state transition diagram, see [Migration State Machine](../02-core-concepts/migration-state-machine.md).

## Detailed Error Scenario Reference

For a complete matrix of all error scenarios — including every combination of `MigrationErrorAction`, error position, rollback chain behavior, multi-target modes, and `RunAlways` — with concrete status outcomes and step-by-step recovery procedures, see [Error Scenarios and Recovery](../02-core-concepts/error-scenarios-and-recovery.md).

---

## When a Migration Is Already Running

When RayMigrator starts a new migration run, it checks the repository for any existing runs in `Running` state for the same product. If one is found, RayMigrator attempts an automatic recovery before failing.

### Automatic Orphaned Run Recovery

Before throwing an error, RayMigrator checks if the blocking run is an orphaned run (older than 10 minutes). If orphaned runs are found, they are automatically fixed (marked as `Error` with orphaned migrations set to `NotMigrated`), and the migration run is retried. This handles the common case where a previous process crashed without cleaning up.

If no orphaned runs older than 10 minutes are found, RayMigrator assumes the blocking run is genuinely active and throws a `MigrationAlreadyRunningException`, exiting with code `1`:

```
RayMigrator aborted because another migration is already running.
```

This can happen in two scenarios:

1. **Legitimate concurrent access**: Another process is genuinely running migrations for the same product. Wait for it to finish before starting a new run.
2. **Recently orphaned run**: A previous migration was interrupted less than 10 minutes ago and the `MigrationRun` record was never closed. The run still appears as `Running` in the repository even though no process is executing it. Wait for the 10-minute threshold to pass, or use the `Fix` command.

RayMigrator also provides a guidance message:

```
To resolve this issue, either wait for the running migration to complete,
or use the Fix command to clean up orphaned runs:
RayMigrator Fix --product <Product> --environment <Environment> --scope OrphanedRuns
```

---

## The Fix Command

When a migration run is interrupted unexpectedly, it may leave "orphaned" runs -- `MigrationRun` entries stuck in `Running` state with no process behind them. These orphaned runs block all future migration attempts for the same product (see above). The `Fix` command detects and cleans up these orphaned runs.

See [Fix Reference](../08-cli-reference/command-reference.md#fix) for all options.

### Basic Usage

```bash
# Always preview before fixing
RayMigrator Fix -p BookStore -env Production --dry-run

# Fix orphaned runs older than 60 minutes (default)
RayMigrator Fix -p BookStore -env Production
```

### Key Options

| Option | Default | Description |
|--------|---------|-------------|
| `--scope` | `OrphanedRuns` | What to fix: `OrphanedRuns` or `All` |
| `--older-than` | `60` | Minimum age in minutes for a run to be considered orphaned |
| `--dry-run` | `false` | Preview what would be fixed without making changes |
| `--last-migration-status` | `not-migrated` | Status to assign to orphaned Migration records: `not-migrated` or `migrated` |

### What Fix Does

For each orphaned `MigrationRun` that matches the age threshold:

1. Updates orphaned `Migration` entries (records stuck in `Pending` or `Executing`) to the status specified by `--last-migration-status` (default: `NotMigrated`).
2. Marks the `MigrationRun` itself as `Error` and sets its `FinishedAt` timestamp.

### Choosing --last-migration-status

- Use `not-migrated` (default) when you are **unsure** whether the interrupted migrations completed successfully. This causes RayMigrator to re-execute them on the next run.
- Use `migrated` when you have **verified** that the interrupted migrations did complete on the database (e.g., by checking that the tables/data exist). This prevents re-execution.

---

## Summary

| MigrationErrorAction | Scope of Rollback | Use Case |
|----------------------|-------------------|----------|
| Terminate | None | Manual recovery |
| Rollback | All migrations in run | All-or-nothing |
| RollbackErrorOnly | Failed migration only | Independent migrations |
| RollbackRelease | Failed release only | Multi-release batches |
| Ignore | None (continues execution) | Non-critical migrations |

| RollbackErrorAction | Behavior on Rollback Failure | Use Case |
|---------------------|------------------------------|----------|
| Terminate | Abort rollback chain | Prevent cascading rollback errors |
| Ignore | Skip failed block, continue | Best-effort rollback |

Key takeaways:
- Choose your `MigrationErrorAction` based on how independent your migrations are and how much manual control you want.
- Choose your `RollbackErrorAction` based on whether you prefer a safe abort or a best-effort rollback when rollback scripts fail.
- Always write rollback files, especially for MariaDB and MySQL where DDL cannot be transactionally rolled back.
- Use `Simulate` run mode to preview what will happen before committing to a real migration run.
- Use the `Fix` command to clean up after unexpected interruptions.

---

Next: [Rolling Back Migrations](09-rollback-guide.md)
