# Chapter 8: When Things Go Wrong

Every migration tool needs a solid error handling strategy. A failing migration in production without a plan is a nightmare scenario. RayMigrator gives you five distinct strategies to handle errors, so you can choose the one that fits your deployment philosophy.

For the complete technical reference of all error action modes, enum values, and state transitions, see [Error Handling](../02-core-concepts/error-handling.md).

---

## MigrationErrorAction Strategies

RayMigrator provides five error handling strategies, configured via the `MigrationErrorAction` property at the product level (or overridden per file via migsettings/TOML). For all enum values and codes, see [Error Handling — Error Action Modes](../02-core-concepts/error-handling.md#error-action-modes).

### Terminate

RayMigrator has no built-in default for `MigrationErrorAction`; set it in `ProductDefaults` or on the product (validation rule RULE_8_1).

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

> **Warning:** Rollback requires matching `.rollback.sql` files. If a rollback file is missing and `RequireRollbackFile` is `true`, the rollback chain stops at that file: the file keeps its status (`Migrated` stays `Migrated`), the run ends as Error with the message of the failure, and the log names the missing rollback file. Once the rollback file is there, `migrate-down` rolls the file back.

> **Note:** When the rollback chain completes without a failure or warning, the migration run is persisted as Recovered (80) instead of Error (90): every rolled-back file is `NotMigrated` again and repository and database are consistent. This applies to Rollback, RollbackErrorOnly and RollbackRelease. A failed rollback block, a missing rollback file or a stopped chain keep Error (90). The exit code is 1 in both cases, so schedulers still see the failure; `info` shows which of the two it was.

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

> **Warning:** With Ignore, the migration run is persisted as PartialSuccess (50) because at least one file failed; all other files are still executed and the CLI exit code is 1. An aborted run is Error (90).

A failed file keeps the blocks that committed before its first failed block. The next `migrate-up` resumes at that block, as long as the SQL blocks of the file did not change, and runs the blocks after it again; when the first block failed, or the file was changed, it runs the whole file. A `RunAlways` file always runs as a whole: the next `migrate-up` runs all of its blocks again.

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

> **Note:** `RollbackErrorAction` is resolved from the rollback file's own TOML header, then the Product (which inherits from ProductDefaults), then the default `Terminate`. `migsettings.txt` does not apply to rollback files (#20).

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

**File:** `Migrations/Release 1.1/Backend/002_AddCategoryToBooks.sql`

```sql
/*
[RayMigrator]
Description = "Add CategoryId to Books - fails on purpose: the table is called Books, not Book"
UseTransaction = true
*/

ALTER TABLE [dbo].[Book] ADD [CategoryId] INT NULL;
```

**File:** `Migrations/Release 1.1/Backend/002_AddCategoryToBooks.rollback.sql`

```sql
/*
[RayMigrator]
Description = "Rollback for 002_AddCategoryToBooks"
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
      "MigrationFilesRootDirectory": "{ENV:BOOKSTORE_MIGRATIONS}",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "SqlServer",
        "Targets": [{
          "Alias": "MainDB",
          "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}"
        }]
      }]
    }]
  }
}
```

### Step 3: Run the Migration

```bash
raymigrator migrate-up -p BookStore -env Development -rm migrate
```

**Expected result:** Migration 002 fails because the table `NonExistentTable` does not exist. With `Rollback` strategy, RayMigrator automatically rolls back Migration 001 (Categories), leaving the database unchanged from before the run.

### Step 4: Check the Result

```bash
raymigrator info -p BookStore -env Development
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
raymigrator migrate-up -p BookStore -env Development -rm migrate
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

## Stopping a Run with Ctrl+C

A cancel is not an error. When you press Ctrl+C while a `migrate-up` runs, or a container stop or a cancelled CI job sends SIGTERM (Linux and macOS), RayMigrator does not die in the middle of a block: it stops at the next block boundary, logs a warning (no error and no stack trace), records the run as `Cancelled` (70) and exits with code `130`.

```
Migration 001 ✓ (Migrated)
Migration 002 ■ (cancelled after block 2 of 4)

Result with --cancelled-action terminate (the default):
  001 → Migrated — kept
  002 → Failed with 2 committed blocks — the next migrate-up continues with block 3
  003 → never executed
```

The example assumes a target with a connection of its own. In the BookStore setup of this guide the target uses the connection string of the repository and the files set `UseTransaction = true`, so every file runs on the atomic shared-connection path: its blocks and their record updates share one transaction, a cancel rolls the whole file back, its record is `Failed` with no committed block, and the next `migrate-up` runs it again from block 1 (see [Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection)).

`--cancelled-action` decides what happens after the stop. `terminate` (the default) does nothing else. `rollback-file`, `rollback-run` and `rollback-release` roll back like the error actions `RollbackErrorOnly`, `Rollback` and `RollbackRelease`, with the same rules for missing rollback files and `RollbackErrorAction`. That rollback cannot be cancelled again; when it does not complete cleanly, the run is `Error` and the exit code is `1`.

A cancel does not turn a failure into a cancel. When a file has already failed and Ctrl+C stops the error recovery of its `MigrationErrorAction` (`Rollback`, `RollbackErrorOnly`, `RollbackRelease`), the run stays `Error` with the error code of the failed file and the exit code is `1`, so a pipeline sees the failure. The message names the failure first, then where the cancel stopped the recovery and what that leaves; complete the rollback with `migrate-down`. `--cancelled-action` is ignored then, because a rollback is already running.

Some work always runs to its end, so that the repository never records a step half done:

- The block in progress on MySQL, which interrupts only the statement in progress and runs the rest of the block, and on SQLite, which cannot interrupt a statement. SQL Server, PostgreSQL and MariaDB abort it, except that MariaDB, like MySQL, ends a `SLEEP()` early without an error and runs the rest of the block.
- During a rollback (a `migrate-down`, or the error recovery of a `migrate-up`): the first block of a rollback file, an atomic rollback and a rollback through a CLI tool. A cancelled `migrate-down` leaves the record in progress `Failed` with its rollback blocks, and the next `migrate-down` continues it unless its first rollback block failed under `RollbackErrorAction=Ignore`; until then `migrate-up` refuses to run that file (see [Rolling Back Migrations](09-rollback-guide.md#block-level-rollback-tracking)).

With `MigrationErrorAction` = `Ignore`, the interrupted file is stored like a file with ignored errors: its record is `Failed` with the blocks before the first failed one, and the next `migrate-up` resumes at the failed block.

A second Ctrl+C ends the process at once. That abandons the run like a crash: it stays `Running` until RayMigrator closes it as orphaned (next section) or you run `fix`. The repair keeps what committed: the next `migrate-up` resumes the interrupted file after its committed blocks, and runs at most the block that was running again.

The detailed semantics, also for hosts that cancel a run through the engine API, are in [Error Handling — Cancellation](../02-core-concepts/error-handling.md#cancellation) and [ADR-028](../arc42/09-Architecture-Decisions.md#adr-028-a-run-cancels-at-the-next-block-boundary-with-a-migrationcancelledaction-and-records-cancelled); what every command does when it is cancelled is listed in [Cancelling a Run](../08-cli-reference/global-options.md#cancelling-a-run).

---

## When a Migration Is Already Running

When RayMigrator starts a new migration run, it checks the repository for any existing runs in `Running` state for the same product. If one is found, RayMigrator attempts an automatic recovery before failing.

### Automatic Orphaned Run Recovery

Before throwing an error, RayMigrator checks whether the blocking run is orphaned. A running command refreshes its heartbeat (`MigrationRun.LastHeartbeatAt`) every `Repository.HeartbeatIntervalInSeconds` (default 30 seconds); a run whose last heartbeat, or whose start when it has none, is older than `Repository.OrphanedRunThresholdInMinutes` (default 2 minutes) is orphaned. Orphaned runs are fixed automatically (marked as `Error`; their open migrations keep their progress: `Failed` with the committed blocks, which the new run resumes after, `Migrated` when every block committed, `NotMigrated` without progress), and the migration run is retried. This handles the common case where a previous process crashed or was killed without cleaning up.

If the blocking run still writes heartbeats, RayMigrator assumes it is genuinely active and throws a `MigrationAlreadyRunningException`, exiting with code `1`:

```
RayMigrator aborted because another migration is already running.
```

This can happen in two scenarios:

1. **Legitimate concurrent access**: Another process is genuinely running migrations for the same product. Wait for it to finish before starting a new run.
2. **Recently orphaned run**: A previous migration was killed (or abandoned with a second Ctrl+C) and wrote its last heartbeat less than 2 minutes ago, and the `MigrationRun` record was never closed. The run still appears as `Running` in the repository even though no process is executing it. Wait for the threshold to pass, or use the `fix` command with `--older-than 0`.

RayMigrator also provides a guidance message:

```
To resolve this issue, either wait for the running migration to complete,
or use the Fix command to clean up orphaned runs:
raymigrator fix --product <Product> --environment <Environment> --scope orphanedruns
```

---

## The Fix Command

When a migration run is interrupted unexpectedly, it may leave "orphaned" runs -- `MigrationRun` entries stuck in `Running` state with no process behind them. These orphaned runs block all future migration attempts for the same product (see above). The `fix` command detects and cleans up these orphaned runs.

See [Fix Reference](../08-cli-reference/command-reference.md#fix) for all options.

### Basic Usage

```bash
# Always preview before fixing
raymigrator fix -p BookStore -env Production --run-mode simulate

# Fix orphaned runs whose last heartbeat is older than 2 minutes (default)
raymigrator fix -p BookStore -env Production
```

### Key Options

| Option | Default | Description |
|--------|---------|-------------|
| `--scope` | `OrphanedRuns` | What to fix: `OrphanedRuns` or `All` |
| `--older-than` | `2` | Minutes since the last heartbeat (or the start of a run without one) after which an open run counts as orphaned; `0` fixes every open run |
| `--run-mode` | `migrate` | `simulate` previews what would be fixed without making changes |
| `--last-migration-status` | — | Status to set on every open MigrationRecord row of the run: `not-migrated` or `migrated`; without it each row keeps the progress of its file |

### What Fix Does

For each orphaned `MigrationRun` that matches the age threshold:

1. Settles the open `MigrationRecord` rows (entries stuck in `Pending` or `Executing`): without `--last-migration-status` by the progress of each file, `Failed` with its committed blocks (the next `migrate-up` resumes after them), `Migrated` when every block committed, `NotMigrated` without progress; with the option to the status it names. Rows without an end get `FinishedAt`, the time of the repair; a row whose rollback was running keeps its end and its duration.
2. Marks the `MigrationRun` itself as `Error` and sets its `FinishedAt` timestamp. Its `ErrorCode` becomes `1006` and its `ErrorMessage` `Closed as orphaned by the repair: the run ended without finishing; last heartbeat <yyyy-MM-dd HH:mm:ss> UTC`, so that the run history tells such a run from one that failed; a code or a message that the run has already stays.

### Choosing --last-migration-status

- Leave the option out in the normal case: each open record keeps what committed, so the next `migrate-up` resumes a file after its committed blocks and normally runs only the block that was running again (under `MigrationErrorAction=Ignore` the blocks from the first failed one).
- Use `not-migrated` when you want the files to run again from their first block, for example after you have undone their changes by hand.
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
- Stop a running migration with Ctrl+C rather than killing the process: the run ends `Cancelled`, and with the default `--cancelled-action terminate` the next run continues where it stopped.
- Use the `fix` command to clean up after unexpected interruptions.

---

Next: [Rolling Back Migrations](09-rollback-guide.md)
