# Rollback Files

Rollback files contain SQL to reverse migrations.

## Naming Convention

```
{MigrationBaseName}.{RollbackPreExtension}.{Extension}
```

Where `{MigrationBaseName}` is the migration filename without its file extension (via `Path.GetFileNameWithoutExtension`).

Default: `.rollback.sql`

Rollback files must reside in the **same directory** as their corresponding migration file. File matching is **case-insensitive**.

### Examples

| Migration | Rollback |
|-----------|----------|
| `001_CreateTable.sql` | `001_CreateTable.rollback.sql` |
| `002_AddColumn.sql` | `002_AddColumn.rollback.sql` |

## Custom Extension

Configure via `ProductDefaults` or per product:

```json
{
  "ProductDefaults": {
    "MigrationRollbackFilesPreExtension": "down"
  },
  "Products": [{
    "MigrationRollbackFilesPreExtension": "down"
  }]
}
```

Result: `001_CreateTable.down.sql`

The default value is `"rollback"`. Product-level settings override `ProductDefaults`.

## Rollback File Structure

Same structure as migration files, with optional TOML:

```sql
/*
[RayMigrator]
Description = "Rollback: Drop users table"
*/

DROP INDEX IF EXISTS IX_Users_Email ON Users;
DROP TABLE IF EXISTS Users;
```

## When Rollbacks Execute

### migrate-down Command

```bash
raymigrator migrate-down -p MyProduct -env Prod --to-release "Release 1.0"
```

Executes rollback files in reverse order for all migrations after the target release. Migrations at or before the target release remain intact.

migrate-down queries the repository for records with status `Migrated`, records of a file that stopped after committed blocks without a rollback (`Failed`, or `Executing` in a run that has ended, with `FileUpBlocksMigrated > 0` and no `FileDownHash`), and partially-rolled-back records (`Failed`, or `Executing` in a run that has ended, where `FileDownBlocksMigrated > 0` and `FileDownBlocksMigrated < FileDownBlocksTotal`), filters to releases after the target, and processes them in reverse `FileOrderId` order. The rollback file of a file that stopped runs from its first rollback block on a partially applied file ([Rollback of a Partially Applied File](#rollback-of-a-partially-applied-file)). A record that stopped without a committed block is not rolled back; migrate-down names it in a warning.

### Error Recovery (MigrationErrorAction)

The `MigrationErrorAction` setting controls what happens when a migration fails during `migrate-up`. The following values are supported:

#### Terminate (value 10)

No rollback is performed. The migration run is aborted immediately. The database may be left in an inconsistent state.

#### Rollback (value 20)

```
Migration A ✓
Migration B ✓
Migration C ✗ (error)

Rollback:
Rollback C
Rollback B
Rollback A
```

Rolls back all migrations performed by the current MigrationRun (the failed file first when it has a record, then the files the run migrated and the files it left `Failed` under `Ignore`, in reverse order).

#### RollbackErrorOnly (value 21)

```
Migration A ✓
Migration B ✓
Migration C ✗ (error)

Rollback:
Rollback C only
```

Rolls back only the failed migration file. Migrations A and B remain intact.

#### RollbackRelease (value 22)

```
Release 1.0:
  Migration A ✓
  Migration B ✓
Release 2.0:
  Migration C ✓
  Migration D ✗ (error)

Rollback:
Rollback D
Rollback C
(Release 1.0 migrations A and B remain intact)
```

Rolls back the current run's migrations of the release that caused the error, those it left `Failed` under `Ignore` included. Migrations from earlier releases remain intact.

#### Ignore (value 30)

No rollback is performed. The failed SQL blocks are skipped, the migration file is marked as `Failed`, and the migration run continues with the next file. When the same run is rolled back later, by a `Rollback` or `RollbackRelease` of a later file or by a cancel with a rollback action, the chain rolls the file back as well, from its first rollback block ([Rollback of a Partially Applied File](#rollback-of-a-partially-applied-file)).

### Multi-Target Rollback Tracking

When `Rollback` or `RollbackRelease` triggers a rollback chain, each record of the run stores its own `TargetAlias` at the time of execution. This ensures that rollback SQL is executed against the correct target database, even when multiple targets exist within a target group.

## Execution Order

Rollbacks execute in **reverse** order of migrations:

**Migration order**:
```
001_CreateTable.sql
002_InsertData.sql
003_AddIndex.sql
```

**Rollback order**:
```
003_AddIndex.rollback.sql
002_InsertData.rollback.sql
001_CreateTable.rollback.sql
```

## MigrationStatus After Rollback

After a rollback, each migration record's `MigrationStatusId` reflects the outcome:

| Scenario | MigrationStatus | Value |
|----------|-----------------|-------|
| Rollback file exists and all blocks succeed | `NotMigrated` | 50 |
| Rollback file missing (`RequireRollbackFile=false`) | unchanged | — |
| Rollback file missing (`RequireRollbackFile=true`) | unchanged | — (chain aborted) |
| Rollback file missing, `StopRollbackOnMissingRollbackFile=true` (error recovery) | unchanged | — (chain stopped) |
| Rollback block fails (`RollbackErrorAction=Ignore`) | `Failed` | 30 |
| Rollback block fails (`RollbackErrorAction=Terminate`) | `Failed` | 30 (chain aborted) |

When the status is "unchanged", the record keeps its status from before the rollback attempt. For a successfully executed migration this is `Migrated` (100), for a file that stopped after committed blocks `Failed` (30) with those blocks, which the next migrate-up resumes after, unless it runs the file in full (a RunAlways file, changed SQL blocks, a CLI tool). The repository entry is not updated when a rollback file is skipped.

This applies equally to migrate-down and error recovery rollbacks, as both use the same shared `ExecuteRollbackForMigrations` method.

**Example**: A migrate-up run with `MigrationErrorAction=Rollback` and `RequireRollbackFile=false` where some schema migrations lack rollback files:

- Schema migrations (no rollback file): status = `Migrated` (100) — unchanged, the migration remains in the database
- Data migrations (with rollback file): status = `NotMigrated` (50)

## Examples

### Create Table → Drop Table

**Migration**: `001_CreateUsers.sql`
```sql
/*
[RayMigrator]
Description = "Create users table"
*/

CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL
);

CREATE INDEX IX_Users_Email ON Users(Email);
```

**Rollback**: `001_CreateUsers.rollback.sql`
```sql
/*
[RayMigrator]
Description = "Drop users table"
*/

DROP INDEX IF EXISTS IX_Users_Email ON Users;
DROP TABLE IF EXISTS Users;
```

### Add Column → Drop Column

**Migration**: `002_AddUserStatus.sql`
```sql
/*
[RayMigrator]
Description = "Add status column to users"
*/

ALTER TABLE Users ADD Status NVARCHAR(20) DEFAULT 'Active';
```

**Rollback**: `002_AddUserStatus.rollback.sql`
```sql
/*
[RayMigrator]
Description = "Remove status column"
*/

ALTER TABLE Users DROP COLUMN Status;
```

### Insert Data → Delete Data

**Migration**: `003_InsertRoles.sql`
```sql
/*
[RayMigrator]
Description = "Insert default roles"
*/

INSERT INTO Roles (Id, Name) VALUES (1, 'Admin'), (2, 'User');
```

**Rollback**: `003_InsertRoles.rollback.sql`
```sql
/*
[RayMigrator]
Description = "Remove default roles"
*/

DELETE FROM Roles WHERE Id IN (1, 2);
```

## RollbackErrorAction

The `RollbackErrorAction` enum controls what happens when a rollback SQL block itself fails during execution. Since a failed rollback cannot itself be rolled back, only two actions are meaningful:

| Value | Behavior |
|-------|----------|
| `Terminate` (10) | Abort the entire rollback chain immediately. No further rollbacks are executed. This is the default. |
| `Ignore` (30) | Skip the failed SQL block and continue with remaining blocks in the same file. The rollback file is marked as `Failed`, but the chain continues with the next file. |

`RollbackErrorAction` can be configured at the following levels:

1. `ProductDefaults.RollbackErrorAction` in `appsettings.json`
2. `Products[].RollbackErrorAction` in `appsettings.json` (per product)
3. TOML `RollbackErrorAction` in individual **rollback** file (per-file override)

The effective value is resolved as: rollback file TOML > Product > ProductDefaults > `Terminate` (hardcoded fallback).

**Note**: Unlike forward migration files, rollback files are not subject to migsettings inheritance. When a rollback file is parsed, migsettings defaults are not applied.

## UseCliToolAlias in Rollback Files

When a rollback file is executed, the `UseCliToolAlias` is resolved via `ResolveUseCliToolAlias`: the rollback file's own TOML `UseCliToolAlias` (if set) takes precedence, otherwise the Target-level `UseCliToolAlias` from the configuration cascade is used. Since rollback files are parsed without migsettings inheritance, directory-level `UseCliToolAlias` from migsettings files does not apply to rollback files. To use a CLI tool for rollback execution, set `UseCliToolAlias` either in the rollback file's TOML header or at the Target/TargetGroup/Product/ProductDefaults level in `appsettings.json`.

## Missing Rollback Files

The behavior when a rollback file is missing depends on the `RequireRollbackFile` setting. Both migrate-down and error recovery share the same logic (via the shared `ExecuteRollbackForMigrations` method).

**During rollback execution**, only the product-level `RequireRollbackFile` setting (`Products[].RequireRollbackFile` or `ProductDefaults.RequireRollbackFile`) is evaluated. Per-file TOML and migsettings overrides are not considered at this stage:

- **`RequireRollbackFile = true`** (default): The rollback chain is aborted immediately. The migration status is **not changed**: nothing of its rollback ran, a file that stopped after committed blocks keeps them, and once the rollback file is there, migrate-down rolls the file back. The run ends as `Error`; the error message of a migrate-down and of the rollback of a cancel action names the missing rollback file, while the error recovery of a migrate-up logs it and its run keeps the message of the failure that started the recovery. This applies regardless of `RollbackErrorAction`. The rollback of a stopped migrate-up can also reach a file that stopped without a committed block; it keeps its status as well, migrate-down does not roll it back, and the next migrate-up runs it from its first block.
- **`RequireRollbackFile = false`**: A warning is logged, the migration status is **not changed** (it retains its previous value, e.g., `Migrated`; a file that stopped after committed blocks keeps them, which the warning names, and stays pending for the next migrate-up, which resumes it after them unless it runs the file in full), and the rollback chain continues with the next file. For error-recovery rollback, whether the chain stops or continues also depends on `StopRollbackOnMissingRollbackFile`.

### RequireRollbackFile Option

When `RequireRollbackFile = true` (default), RayMigrator validates during the file discovery phase that every migration file has a corresponding rollback file. If any rollback file is missing, migration is aborted with a `MigrationFileParsingException` (error code `RequireRollbackFileValidationFailed = 1001`) **before** any SQL is executed.

This prevents the scenario where a failed migration triggers a rollback chain, but some earlier migrations cannot be rolled back because their rollback files are missing.

**Configuration hierarchy** (lowest to highest priority):

1. `ProductDefaults.RequireRollbackFile` in `appsettings.json`
2. `Products[].RequireRollbackFile` in `appsettings.json` (per product)
3. `migsettings.txt` / `migsettings.{Env}.txt` (directory-level override)
4. TOML `RequireRollbackFile` in individual migration file (per-file override)

The effective value is resolved as: TOML > migsettings > Product > ProductDefaults > `true` (hardcoded fallback).

**Example - disable for a specific migration**:

```sql
/*
[RayMigrator]
Description = "Initial schema - no rollback possible (destructive)"
RequireRollbackFile = false
*/

DROP TABLE IF EXISTS LegacyData;
```

**Example - disable for a product**:

```json
{
  "Products": [{
    "Alias": "LegacyImport",
    "RequireRollbackFile": false,
    "MigrationFilesRootDirectory": "/migrations/legacy"
  }]
}
```

## Non-Reversible Operations

Some operations cannot be rolled back:

| Operation | Rollback Possible |
|-----------|-------------------|
| CREATE TABLE | Yes (DROP TABLE) |
| DROP TABLE | No (data lost) |
| ADD COLUMN | Yes (DROP COLUMN) |
| DROP COLUMN | No (data lost) |
| INSERT DATA | Yes (DELETE) |
| DELETE DATA | Depends (need backup) |
| TRUNCATE | No (data lost) |

### Handling Non-Reversible

**Option 1**: Empty rollback with warning

```sql
/*
[RayMigrator]
Description = "WARNING: Data deleted, cannot restore"
*/

-- Manual restoration required
-- See backup: backup_20250129.bak
SELECT 1;  -- No-op
```

**Option 2**: No rollback file (requires `RequireRollbackFile = false`)

Document in migration that rollback is not possible. Note that this only works when `RequireRollbackFile = false` is set (at the file, directory, product, or global level), otherwise the file discovery phase will reject the migration.

## Rollback of a Partially Applied File

A rollback file does not only run after its migration file completed. The error recovery of a `MigrationErrorAction` and the rollback of a `MigrationCancelledAction` roll back the file that stopped first, with the blocks it committed, and then the files the run left `Failed` under `MigrationErrorAction=Ignore` with the files it migrated, also a file without a committed block, and migrate-down rolls back a file that a failed, cancelled or abruptly ended migrate-up left with committed blocks ([migrate-down — Rollback Order](../08-cli-reference/migrate-down.md#rollback-order)). The rollback file then runs from its first rollback block on a target where only the leading blocks of the file committed, with `MigrationErrorAction=Ignore` also blocks after a failed one. A rollback file must tolerate that: `DROP TABLE IF EXISTS`, `DROP INDEX IF EXISTS`, `DELETE … WHERE` on the rows the file inserted. A rollback file that fails on a partially applied file leaves the record `Failed` with the rollback blocks that ran, as every failed rollback does: the next migrate-down continues it at the failed rollback block, and migrate-up refuses the file while some rollback blocks ran ([Partial Rollback Resume](#partial-rollback-resume)).

## Partial Rollback Resume

If a rollback is interrupted (e.g., `RollbackErrorAction=Terminate` aborted the chain), the repository records how many rollback blocks were already executed (`FileDownBlocksMigrated`). On a subsequent migrate-down, RayMigrator resumes from the next unexecuted block rather than re-executing already-completed blocks. Until then, migrate-up refuses to run the file, because it would run it from its first block on a target that kept part of it (see [Error Handling: A Rollback That Stopped in the Middle](../02-core-concepts/error-handling.md#a-rollback-that-stopped-in-the-middle)).

## Best Practices

1. **Always create rollback files** for production migrations
2. **Test rollbacks** before production deployment
3. **Consider data loss** when designing rollbacks
4. **Keep rollbacks simple** - reverse the migration, nothing more
5. **Use transactions** in rollbacks when possible
6. **Tolerate a partially applied file** - `IF EXISTS`, `DELETE … WHERE` ([Rollback of a Partially Applied File](#rollback-of-a-partially-applied-file))

## Related Documentation

- [File Naming](file-naming.md)
- [Error Handling](../02-core-concepts/error-handling.md)
- [Migration State Machine](../02-core-concepts/migration-state-machine.md)
- [migrate-down Command](../08-cli-reference/migrate-down.md)
