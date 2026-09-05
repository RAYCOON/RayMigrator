# Chapter 9: Rolling Back Migrations

The previous chapter covered automatic rollback triggered by errors. This chapter covers explicit, intentional rollbacks using the `Migrate-Down` command -- the controlled way to undo migrations when you need to revert your database to an earlier state.

---

## Explicit Rollback with Migrate-Down

The `Migrate-Down` command rolls back migrations to a specified release:

```bash
raymigrator Migrate-Down -p BookStore -env Development --to-release "Release 1.0" -rm Migrate
```

**Important:** `--to-release "Release 1.0"` means "roll back TO Release 1.0" -- Release 1.0 stays applied. Everything after Release 1.0 is rolled back.

| Parameter | Short | Description |
|-----------|-------|-------------|
| `--product` | `-p` | Product alias |
| `--environment` | `-env` | Environment name |
| `--to-release` | `-tr` | Target release to roll back to (this release remains applied) |
| `--run-mode` | `-rm` | `Validate` (validate rollback file existence and parseability without DB connectivity), `Simulate` (read repository records but do not write records or execute SQL on targets), or `Migrate` (execute rollback SQL) |
| `--target-group` | `-tg` | Filter rollback to specific target groups (can be specified multiple times) |

---

## How Rollback Works

The rollback process follows these steps:

1. RayMigrator reads the repository to find all `Migrated` records after the target release. It also picks up partially-rolled-back records (`Failed` with `FileDownBlocksMigrated > 0` and less than `FileDownBlocksTotal`), allowing a previously interrupted rollback to resume.
2. Records are ordered by `FileOrderId` descending (reverse of migration order).
3. For each record, RayMigrator locates the matching `.rollback.sql` file.
4. Executes the rollback SQL blocks on the target database, tracking progress per block.
5. On success, updates the repository record status to `NotMigrated` (50).

```
Current state: Release 1.0 (3 files) + Release 1.1 (2 files) applied

Migrate-Down --to-release "Release 1.0":
  Rollback Release 1.1/002 ← executed in reverse order
  Rollback Release 1.1/001

Result: Only Release 1.0 migrations remain applied
```

### Missing Rollback Files During Rollback

When a rollback file is not found, the behavior depends on two settings: `RequireRollbackFile` (pre-validation gate) and `StopRollbackOnMissingRollbackFile` (error-recovery chain behavior).

- `RequireRollbackFile` answers: "Do I need rollback files to START the migration?" It is checked during file discovery before any SQL is executed.
- `StopRollbackOnMissingRollbackFile` answers: "During error-recovery rollback, should the chain stop when a rollback file is missing?" It has no effect on Migrate-Down.

| `RequireRollbackFile` | `StopRollbackOnMissingRollbackFile` | Flow | Behavior | Record Status |
|-----------------------|-------------------------------------|------|----------|---------------|
| `true` | N/A | Any | Rollback chain **aborted immediately** | `Failed` (30) |
| `false` | `true` (default) | Error-recovery Rollback | Chain **stopped** with Warning | Unchanged |
| `false` | `false` | Error-recovery Rollback | Chain **continues** with Warning | Unchanged |
| `false` | N/A | Migrate-Down | Chain **continues** with Warning | Unchanged |

> **Warning:** When `RequireRollbackFile=true` and a rollback file is missing, the entire rollback chain stops -- even if subsequent files have valid rollback files. This is treated as a structural error that always aborts regardless of the `RollbackErrorAction` setting.

### Block-Level Rollback Tracking

RayMigrator tracks rollback progress at the SQL block level. Each rollback file is split into blocks (separated by the database engine's statement separator, e.g., `GO` for SQL Server, `;` for PostgreSQL). As each block executes successfully, the repository records `FileDownBlocksMigrated` and `FileDownBlocksTotal`.

If a rollback is interrupted (e.g., by a block error with `RollbackErrorAction=Terminate`), the partially-rolled-back record retains its block progress. A subsequent `Migrate-Down` command will detect this record and **resume from the last successful block**, rather than re-executing blocks that already completed.

### RollbackErrorAction During Migrate-Down

When a rollback SQL block fails during Migrate-Down execution, the `RollbackErrorAction` setting controls the response:

| `RollbackErrorAction` | Behavior |
|-----------------------|----------|
| `Terminate` (default) | The failing block's migration is marked `Failed` (30), and the entire rollback chain is aborted. |
| `Ignore` | The failing block is skipped with a warning. Remaining blocks in the file continue, and the file is marked `Failed` (30). The rollback chain continues with the next file. |

`RollbackErrorAction` can be configured at the product level or overridden per rollback file via TOML metadata. For more details, see [Chapter 8: When Things Go Wrong](08-error-handling.md#rollbackerroraction-when-a-rollback-fails).

---

## Tutorial: Roll Back BookStore

Continuing from the previous chapter, assume BookStore has both Release 1.0 and Release 1.1 applied.

### Step 1: Validate and Preview the Rollback

First, validate that all required rollback files exist and are parseable (no database connectivity needed):

```bash
raymigrator Migrate-Down -p BookStore -env Development -tr "Release 1.0" -rm Validate
```

Then simulate to preview the rollback with repository interaction but without executing SQL on targets:

```bash
raymigrator Migrate-Down -p BookStore -env Development -tr "Release 1.0" -rm Simulate
```

Review the output carefully before proceeding.

### Step 2: Execute the Rollback

```bash
raymigrator Migrate-Down -p BookStore -env Development -tr "Release 1.0" -rm Migrate
```

RayMigrator processes the rollback files in reverse order:
1. `Release 1.1/Backend/002_AddCategoryToBooks.rollback.sql` -- drops the CategoryId column
2. `Release 1.1/Backend/001_AddCategories.rollback.sql` -- drops the Categories table

### Step 3: Verify the Result

```bash
raymigrator Info -p BookStore -env Development
```

You will see:
- All Release 1.0 migrations remain with status `Migrated` (100)
- All Release 1.1 migrations that had rollback files show status `NotMigrated` (50)
- Migrations without rollback files (when `RequireRollbackFile=false`) retain their previous status (e.g., `Migrated` if they were previously applied)

> **Tip:** Always verify with `Info` after a rollback to confirm the expected state.

---

## Writing Effective Rollback Files

Rollback files use the same TOML metadata header as migration files, with the `.rollback.sql` pre-extension. A well-written rollback file cleanly undoes exactly what its corresponding migration did.

### 1. Use IF EXISTS

Always check before dropping to make rollback files idempotent:

```sql
/*
[RayMigrator]
Description = "Drop Categories table"
UseTransaction = true
*/

DROP TABLE IF EXISTS [dbo].[Categories];
```

### 2. Handle Foreign Keys

Drop constraints before dropping the tables they reference:

```sql
/*
[RayMigrator]
Description = "Drop Authors table and related constraints"
UseTransaction = true
*/

ALTER TABLE [dbo].[Books] DROP CONSTRAINT IF EXISTS [FK_Books_Authors];
DROP TABLE IF EXISTS [dbo].[Authors];
```

### 3. Preserve Data Where Possible

For additive changes (adding columns, creating tables), the rollback is straightforward -- drop what was added. For destructive changes (dropping columns, deleting rows), consider backup strategies before the forward migration runs.

### 4. Match the Migration Scope

If the forward migration creates 3 tables, the rollback should drop all 3. If it adds 2 columns and an index, the rollback should drop all of them.

### 5. Test Rollbacks

Always test rollback files in a development environment before relying on them in production:

```bash
# Apply the migration
raymigrator Migrate-Up -p BookStore -env Development -rm Migrate

# Roll it back
raymigrator Migrate-Down -p BookStore -env Development -tr "Release 1.0" -rm Migrate

# Apply again to confirm the cycle works
raymigrator Migrate-Up -p BookStore -env Development -rm Migrate
```

If apply-rollback-apply completes without errors, your rollback files are correct.

---

## Non-Reversible Operations

Some database operations cannot be cleanly reversed:

| Operation | Challenge | Recommendation |
|-----------|-----------|----------------|
| DROP TABLE | Data is permanently lost | Rollback can recreate structure but not data |
| DELETE rows | Data is permanently lost | Consider soft deletes instead |
| ALTER COLUMN (shrink) | Data may be truncated | Backup data before migration |
| DROP COLUMN | Data is permanently lost | Rollback recreates column but it will be empty |
| RENAME TABLE / COLUMN | Generally reversible | Rollback renames back to original |
| INSERT (large dataset) | Reversible but slow | DELETE in rollback may take time |

> **Tip:** For critical production data, always create a database backup before running migrations that modify or delete data. No rollback file can recover data that was permanently removed.

---

## RequireRollbackFile Setting

Control whether rollback files are mandatory. This setting supports the full cross-layer override chain (appsettings → migsettings → TOML). See [Settings Inheritance — RequireRollbackFile](../06-configuration-reference/settings-inheritance-overview.md#requirerollbackfile) for the complete chain.

| Value | Behavior on Migrate-Up | Behavior on Migrate-Down / Error-recovery Rollback |
|-------|------------------------|----------------------------------------------------|
| `true` (default) | Migrate-Up fails during file discovery if any migration is missing its `.rollback.sql` file | Rollback chain is aborted if a rollback file is missing; record is set to `Failed` (30) |
| `false` | Rollback files are optional; migrations without rollback files can be executed | Missing rollback files are skipped with a warning; record status is unchanged. For error-recovery rollback, whether the chain stops or continues depends on `StopRollbackOnMissingRollbackFile` |

## StopRollbackOnMissingRollbackFile Setting

Controls whether an error-recovery rollback chain stops when a rollback file is missing. This setting only applies when `RequireRollbackFile=false` and has no effect on Migrate-Down.

| Value | Behavior |
|-------|----------|
| `true` (default) | The error-recovery rollback chain stops at the first migration with a missing rollback file. A warning is logged and the record status is left unchanged. |
| `false` | The error-recovery rollback chain continues past migrations with missing rollback files. A warning is logged and the record status is left unchanged for each skipped file. |

This setting is configured in appsettings at the `ProductDefaults`, `ProductDefaults.TargetGroupDefaults`, `Product`, or `TargetGroup` level. It can also be set at run time via the `--stop-rollback-on-missing-rollback-file` / `-sromrf` CLI option on the `Migrate-Up` command.

> **Note:** While this setting can also be declared in `migsettings.txt` files and per-file TOML metadata (and is parsed there), those values are not used during the rollback chain execution. Only the appsettings-level and CLI values participate in the runtime resolution.

The setting has no effect on Migrate-Down. Use `RequireRollbackFile = false` in TOML for migrations that intentionally have no rollback file:

```sql
/*
[RayMigrator]
RequireRollbackFile = false
Description = "View recreation - no rollback needed"
UseTransaction = true
*/

CREATE OR ALTER VIEW [dbo].[ActiveBooks] AS
SELECT * FROM [dbo].[Books] WHERE [IsActive] = 1;
```

> **Tip:** Use `RequireRollbackFile = false` for idempotent operations like view or stored procedure recreation, where running the migration again effectively acts as its own rollback.

---

## Rollback Execution Order

Rollbacks always execute in reverse order of the original migration. This is critical for maintaining referential integrity.

```
Migration order (forward):
  001_CreateBooks.sql              (1st)
  002_CreateAuthors.sql            (2nd)
  003_AddForeignKeys.sql           (3rd)

Rollback order (reverse):
  003_AddForeignKeys.rollback.sql  (1st)  ← FK dropped first
  002_CreateAuthors.rollback.sql   (2nd)  ← then parent table
  001_CreateBooks.rollback.sql     (3rd)  ← then other tables
```

This ensures that foreign key constraints are dropped before the tables they reference, preventing dependency errors during rollback.

> **Warning:** Never manually reorder rollback execution. RayMigrator handles the ordering automatically based on the repository records.

---

## Summary

| Topic | Key Point |
|-------|-----------|
| Migrate-Down | Rolls back to a target release; that release stays applied |
| Execution order | Always reverse of the original migration order (`FileOrderId` descending) |
| Validate first | Use `-rm Validate` to check rollback file existence, then `-rm Simulate` to preview with DB, then `-rm Migrate` to execute |
| Target group filter | Use `--target-group` / `-tg` to limit rollback to specific target groups |
| Block-level tracking | Progress is tracked per SQL block; interrupted rollbacks can resume from the last successful block |
| RollbackErrorAction | Controls behavior when a rollback SQL block fails: `Terminate` (abort chain) or `Ignore` (skip and continue) |
| Missing rollback file | `RequireRollbackFile=true` aborts chain with `Failed` (30); `RequireRollbackFile=false` + `StopRollbackOnMissingRollbackFile=true` (default) stops error-recovery chain with Warning; `false`+`false` continues with Warning; status is unchanged for the skipped record |
| IF EXISTS | Always use defensive checks in rollback files |
| Non-reversible ops | Backup data before destructive migrations |
| RequireRollbackFile | Enforce rollback files globally or override per migration via TOML |

---

Next: [Advanced Features](10-advanced-features.md)
