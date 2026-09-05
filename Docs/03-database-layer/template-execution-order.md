# SQL Template Execution Order on Initial Startup

This document describes the exact order in which SQL templates are executed when RayMigrator starts for the first time with no existing database structures (no repository tables, no logging tables).

**Command:** `raymigrator Migrate-Up --product X --environment Y --run-mode Migrate`

---

## Phase 0: Template Cache Initialization (No SQL Executed)

**Class:** `TemplateCache` (`Raycoon.RayMigrator.Infrastructure`, namespace: `Raycoon.RayMigrator.Core.Templates`)
**Trigger:** DI resolution as singleton (constructor calls `Initialize()`)

- Scans `DataAccessLayers/{DatabaseType}/` for `.sql` files (delivered as `<Content>` items that propagate transitively through ProjectReference)
- Maps filenames to `TemplateType` enum values (skips files whose names do not match any `TemplateType` enum value)
- Replaces `{ENV:VariableName}` environment variable placeholders in SQL content
- Stores all templates in an in-memory `Dictionary<string, Dictionary<TemplateType, Template>>`
- Validates template completeness per database type (all `TemplateType` enum values must have a file)
- Validates configured database types exist in the template cache

**No SQL is executed against any database in this phase.**

---

## Phase 1: Database Logging Infrastructure

**Call site:** `DirectModePipeline.cs` (in Pipeline project) -> `DatabaseLogWriter.InitDatabaseLogger()`
**Target database:** Logging database (from `DatabaseLogging` configuration section)

This phase executes whenever the `DatabaseLogging` section is present in the configuration, regardless of run mode. If the section is omitted, the phase is skipped entirely.

### Template 1: `DatabaseLogging_CheckCreate`

**File:** `DataAccessLayers/{DB}/DatabaseLogging_CheckCreate.sql`
**Purpose:** Checks if logging infrastructure exists; creates it if missing.

**What is created:**

> **Note**: SQL Server wraps this in a transaction. MariaDB and MySQL use `CREATE TABLE IF NOT EXISTS` and `INSERT IGNORE` for idempotency instead, since DDL causes implicit commits in MariaDB/MySQL.

1. **Schema** (e.g., `[ray]`) if it does not exist (SQL Server/PostgreSQL only; MariaDB/MySQL uses the database itself)
2. **`MigrationEvent` table** (lookup) - Defines event types with IDs:
   | Id | Name |
   |----|------|
   | 0 | UnspecifiedEvent |
   | 10 | CommandLineParsing |
   | 20 | EnvironmentVariableReplacement |
   | 31 | CreateDatabaseLogger |
   | 32 | CreateCompositeLogger |
   | 40 | ValidateRayMigratorOptions |
   | 50 | CreateApplicationHost |
   | 60 | InitializeDalSpecificProperties |
   | 70 | ValidateConnectionStrings |
   | 80 | RayMigratorServiceStart |
   | 100 | CreateAndStartRayMigratorService |
   | 1000 | RayMigratorServiceShutdown |
3. **`MigrationLog` table** (data) - Stores all log entries with columns:
   - `Id` (BIGINT IDENTITY), `LogLevelId`, `MigrationEventId`, `RunModeId`, `ProductId`, `EnvironmentId`, `MigrationRunId`, `MigrationRecordId`
   - `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`
   - `Filename`, `FileOrderId`, `FileBlockId`, `Message`, `CreatedAt`

**Return value:** `1` = newly created, `0` = already existed

### Template 2: `DatabaseLogging_Insert` (Loaded, Not Yet Executed)

**File:** `DataAccessLayers/{DB}/DatabaseLogging_Insert.sql`
**Purpose:** INSERT statement for log entries. Loaded into memory by `InitDatabaseLogger()` and stored in `DatabaseLogWriter._templateLoggingInsert`.

This template is **not executed immediately**. It is first used when the Serilog `RayMigratorDatabaseSink` receives a log event after `databaseSink.SetWriter(dbLogWriter)` is called in `DirectModePipeline.cs`. From that point on, log events that meet the configured `MinimumLevel` are enqueued via `DatabaseLoggerQueue` for asynchronous execution.

---

## Phase 2: Repository Infrastructure

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryCheckCreate()`
**Target database:** Repository database (from `Repository` configuration section)

### Template 3: `Repository_CheckCreate`

**File:** `DataAccessLayers/{DB}/Repository_CheckCreate.sql`
**Purpose:** Checks if the repository exists and is complete. Creates the entire RayMigrator infrastructure on first run.

**SQL parameters:**
- `@RepositoryDatabaseType` - The database type (e.g., `SqlServer`)
- `@RayMigratorVersion` - The application version (e.g., `3.0.0`)

**What is created:**

> **Note**: SQL Server wraps this in a transaction with `BEGIN TRY`/`BEGIN CATCH`. PostgreSQL uses a `DO $$` block. MariaDB and MySQL use `CREATE TABLE IF NOT EXISTS` with inline FK constraints and `INSERT IGNORE` for idempotency, since DDL causes implicit commits and cannot be wrapped in a transaction.

1. **Schema** (e.g., `[ray]`) if it does not exist (SQL Server/PostgreSQL only; MariaDB/MySQL uses the database itself)

2. **4 Lookup tables:**
   - `MigrationOperation` (`tinyint Id`, `Name`, `Description`)
   - `MigrationRunResult` (`tinyint Id`, `Name`, `Description`)
   - `MigrationRunMode` (`tinyint Id`, `Name`, `Description`)
   - `MigrationStatus` (`tinyint Id`, `Name`, `Description`)

3. **7 Data tables:**
   - `MigratorMeta` (`Id` IDENTITY, `RepositoryVersion`, `RepositoryDatabaseType`, `CreatedByRayMigratorVersion`, `CreatedAt`)
   - `Product` (`Id` IDENTITY, `Name`, `NameLower` UNIQUE, `CreatedAt`)
   - `Environment` (`Id` IDENTITY, `Name`, `NameLower` UNIQUE, `CreatedAt`)
   - `MigrationRun` (`Id` IDENTITY, `MigratorMetaId` FK, `ProductId` FK, `EnvironmentId` FK, `MigrationRunModeId` FK, `MigrationRunResultId` FK, `FromReleaseVersion`, `ToReleaseVersion`, `StartedAt`, `FinishedAt`, `DurationInMs`)
   - `MigrationRunMeta` (`MigrationRunId` FK, `MigrationRunSettingsJson`, `Description`)
   - `MigrationRecord` (`Id` IDENTITY, `ProductId` FK, `EnvironmentId` FK, `MigrationRunId` FK, various FKs, `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`, `Filename`, `FileOrderId`, hash fields, block tracking fields, timestamps)
   - `MigrationRecordHistory` (mirrors `MigrationRecord` with additional `MigrationRecordId` FK and `HistorizedAt`)

4. **Indexes:** Three indexes are created on all engines — shown here in canonical (SQL Server/SQLite) PascalCase; PostgreSQL, MariaDB, and MySQL use the snake_case equivalents: `uix_product_name_lower`, `uix_environment_name_lower`, `ix_migration_record_history_migration_record_id`.
   - `uix_Product_NameLower` on `Product(NameLower)` (unique, case-insensitive deduplication)
   - `uix_Environment_NameLower` on `Environment(NameLower)` (unique, case-insensitive deduplication)
   - `ix_MigrationRecordHistory` on `MigrationRecordHistory(MigrationRecordId)` (fast lookup by source record)

   **PostgreSQL only** additionally creates FK-column indexes (snake_case names per DAL-017): `ix_migration_run_migrator_meta_id`, `ix_migration_run_product_id`, `ix_migration_run_environment_id`, `ix_migration_run_migration_run_mode_id`, `ix_migration_run_migration_run_result_id`, `ix_migration_record_product_id`, `ix_migration_record_environment_id`, `ix_migration_record_migration_run_id`, `ix_migration_record_migration_run_mode_id`, `ix_migration_record_migration_operation_id`, `ix_migration_record_migration_status_id`. MySQL/MariaDB InnoDB auto-indexes FK columns; PostgreSQL and SQL Server do not (FK indexing on SQL Server is a candidate for future work).

5. **15 Foreign Keys** between the tables

6. **Extended Properties** (SQL Server only) - Documents enum values directly in the database

7. **Master data in lookup tables:**

   **MigrationRunMode:**
   | Id | Name | Description |
   |----|------|-------------|
   | 0 | Undefined | Invalid value; RunMode has not been set |
   | 10 | Validate | Validates configuration and all migration files. Does NOT connect to target databases or repository database. |
   | 20 | Simulate | Validates, checks DB connectivity, reads repository state. Does NOT execute SQL against target databases or write to the repository. |
   | 100 | Migrate | Validates configuration and all migration files. Performs actual migrations against target databases. |

   **MigrationOperation:**
   | Id | Name | Description |
   |----|------|-------------|
   | 0 | Undefined | Invalid value; operation has not been set |
   | 5 | Rollback | Performing Rollback of current MigrationRun |
   | 50 | MigrateDown | Performing Down-Migration |
   | 100 | MigrateUp | Performing Up-Migration |

   **MigrationRunResult** (used by MigrationRun only):
   | Id | Name | Description |
   |----|------|-------------|
   | 0 | Undefined | Invalid value; result has not been set |
   | 10 | Running | Migration process is currently running |
   | 90 | Error | Migration(s) stopped due to error(s) |
   | 100 | Ok | Migration(s) successfully executed |

   **MigrationStatus** (used by Migration):
   | Id | Name | Description |
   |----|------|-------------|
   | 0 | Undefined | Invalid value; status has not been set |
   | 10 | Pending | Record created, execution pending |
   | 20 | Executing | SQL blocks are being executed |
   | 30 | Failed | Execution failed, DB state unclear |
   | 50 | NotMigrated | Not deployed / rolled back |
   | 100 | Migrated | Successfully deployed |

8. **MigratorMeta entry** with current `RepositoryVersion`, `RepositoryDatabaseType`, and `RayMigratorVersion`

**Return value:** VersionId (positive integer), stored in `MigrationContext.MigrationState.MigratorMetaId`

---

## Phase 3: Product Registration

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryProductCheckInsert()`
**Target database:** Repository database

### Template 4: `Repository_Product_CheckInsert`

**File:** `DataAccessLayers/{DB}/Repository_Product_CheckInsert.sql`
**Purpose:** Checks if the product (e.g., "RayMigratorTests") already exists. Creates it if missing.

**SQL parameters:**
- `@Name` - The product name in original casing (from console options)
- `@NameLower` - The product name in lowercase (pre-computed in C# via `ToLowerInvariant()`)

**Logic:**
1. Validates that `@Name` is not NULL or empty (returns error code `-20` if empty)
2. SELECT on `Product` table by `NameLower`
3. If found -> returns existing `ProductId`
4. If not found -> INSERT with `Name`, `NameLower`, `CreatedAt` -> returns new `ProductId`

**Return value:** ProductId (positive integer), stored in `MigrationContext.MigrationState.ProductId`

---

## Phase 3b: Environment Registration

**Call site:** `MigrationService` entry point -> `TemplateExecutor.RepositoryEnvironmentCheckInsert()`
**Target database:** Repository database

Called immediately after Product registration at all 8 `MigrationService` entry points (e.g., `MigrateUpAsync`, `MigrateDownAsync`, `BaselineAsync`, `ValidateHashAsync`, `UpdateHashAsync`, `InfoAsync`, `FixAsync`).

### Template 4b: `Repository_Environment_CheckInsert`

**File:** `DataAccessLayers/{DB}/Repository_Environment_CheckInsert.sql`
**Purpose:** Checks if the environment (e.g., "Docker") already exists by `NameLower`. Creates it if missing.

**SQL parameters:**
- `@Name` - The environment name in original casing (from console options)
- `@NameLower` - The environment name in lowercase (pre-computed in C# via `ToLowerInvariant()`)

**Logic:**
1. Validates that `@Name` is not NULL or empty (returns error code `-50` if empty)
2. SELECT on `Environment` table by `NameLower`
3. If found -> returns existing `EnvironmentId`
4. If not found -> INSERT with `Name`, `NameLower`, `CreatedAt` -> returns new `EnvironmentId`

**Return value:** EnvironmentId (positive integer), stored in `MigrationContext.MigrationState.EnvironmentId`

---

## Phase 3c: Interrupted Migration Check

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryMigrationGetInterrupted()`
**Target database:** Repository database

### Template 4c: `Repository_MigrationRecord_GetInterrupted`

**File:** `DataAccessLayers/{DB}/Repository_MigrationRecord_GetInterrupted.sql`
**Purpose:** Checks for interrupted migrations (status `Executing`) from a previous aborted run. If an interrupted migration is found, a warning is logged with the MigrationRecordId, filename, and block progress. This is an informational check only; execution continues regardless of the result.

**SQL parameters:**
- `@ProductId` - From `MigrationState.ProductId`
- `@EnvironmentId` - From `MigrationState.EnvironmentId`

**Return value:** `0` if no interrupted migration found, otherwise a pipe-separated string with `MigrationRecordId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias`, parsed into an `InterruptedMigrationInfo` object.

---

## Phase 4: Migration Run Creation

**Call site:** `MigrationService.MigrateUpAsync()` -> `MigrationService.RepositoryMigrationRunInsertWithAutoFix()` -> `TemplateExecutor.RepositoryMigrationRunInsert()`
**Target database:** Repository database

`MigrateUpAsync` does not call `RepositoryMigrationRunInsert` directly. Instead, it calls the internal wrapper `RepositoryMigrationRunInsertWithAutoFix(settingsJson)`, which first attempts the insert. If a `MigrationAlreadyRunningException` is thrown (error code `-2`), the wrapper checks for orphaned runs older than `AutoFixOrphanedRunsThresholdMinutes` and auto-fixes them before retrying the insert once. If no orphaned runs are found, the original exception is rethrown.

### Template 5: `Repository_MigrationRun_Insert`

**File:** `DataAccessLayers/{DB}/Repository_MigrationRun_Insert.sql`
**Purpose:** Creates a new MigrationRun record. Prevents parallel migrations for the same product/environment/run mode.

**SQL parameters:**
- `@ProductId` - From `MigrationState.ProductId`
- `@EnvironmentId` - From `MigrationState.EnvironmentId`
- `@MigrationRunModeId` - From console options `RunMode` (cast to byte)
- `@MigratorMetaId` - From `MigrationState.MigratorMetaId`
- `@MigrationRunResultId` - Set to `Running` (10) from `MigrationState.MigrationRunResult`
- `@FromReleaseVersion` - Currently hardcoded as `"FromReleaseVersion"`
- `@ToReleaseVersion` - From console options `TargetReleaseVersion`
- `@MigrationRunSettingsJson` - JSON snapshot of all RayMigrator settings at migration start

**Logic:**
1. Checks if an unfinished MigrationRun exists (`FinishedAt IS NULL`) for the same product/environment/run mode
2. If found -> returns error `-2` (parallel migration not allowed)
3. If not found -> INSERT into `MigrationRun` with provided parameters and `SYSUTCDATETIME()` as `StartedAt`, then INSERT settings JSON into `MigrationRunMeta`

**Return value:** MigrationRunId (positive integer), stored in `MigrationContext.MigrationState.MigrationRunId`

---

## Phase 5: Ongoing Log Writes (Asynchronous, Throughout Entire Run)

### Template 2 (Repeated): `DatabaseLogging_Insert`

**Purpose:** Called asynchronously via `DatabaseLoggerQueue` for every Serilog log event that reaches the configured `MinimumLevel`.

**Flow:**
1. Serilog emits a `LogEvent`
2. `MigrationContextEnricher` adds migration-specific properties (`ProductId`, `EnvironmentId`, `MigrationRunId`, etc.) from `MigrationLoggingContext.Current`
3. `RayMigratorDatabaseSink.Emit()` extracts properties and calls `DatabaseLogWriter.EnqueueLogEntry()`
4. `DatabaseLoggerQueue` processes the action on a background thread
5. `DatabaseLogWriter.WriteToDatabase()` executes the `DatabaseLogging_Insert` template via `IDal.ExecuteNonQuery()`

**Parameters per call:** `LogLevelId`, `MigrationEventId`, `RunModeId`, `ProductId`, `EnvironmentId`, `MigrationRunId`, `MigrationRecordId`, `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`, `Filename`, `FileOrderId`, `FileBlockId`, `Message`

---

## Summary: Execution Order

| # | Template | Target DB | Trigger | Purpose |
|---|----------|-----------|---------|---------|
| 1 | `DatabaseLogging_CheckCreate` | Logging DB | `DirectModePipeline.cs` startup (when `DatabaseLogging` configured) | Creates schema + `MigrationEvent` + `MigrationLog` tables |
| 2 | `DatabaseLogging_Insert` | Logging DB | From startup onward, async (when `DatabaseLogging` configured) | Writes log entries (loaded in Phase 1, executed after `SetWriter`) |
| 3 | `Repository_CheckCreate` | Repository DB | `MigrateUpAsync` | Creates schema + 11 tables + 15 FKs + master data + VersionId |
| 4 | `Repository_Product_CheckInsert` | Repository DB | `MigrateUpAsync` | Registers the product, returns ProductId |
| 4b | `Repository_Environment_CheckInsert` | Repository DB | All 8 entry points (after Product check-insert) | Registers the environment, returns EnvironmentId |
| 4c | `Repository_MigrationRecord_GetInterrupted` | Repository DB | `MigrateUpAsync` | Checks for interrupted migrations (informational) |
| 5 | `Repository_MigrationRun_Insert` | Repository DB | `MigrateUpAsync` (via `RepositoryMigrationRunInsertWithAutoFix`) | Creates MigrationRun record, returns MigrationRunId |

---

## Templates NOT Executed on Initial Startup

These templates exist in the template cache but are only used during specific operations:

| Template | When Used |
|----------|-----------|
| `Repository_Drop` | Explicit repository deletion |
| `Repository_MigrationRun_Update` | After completion or error of a MigrationRun |
| `Repository_MigrationRecord_Insert` | Per migration file during actual migration execution |
| `Repository_MigrationRecord_Update` | Block progress or completion per file |
| `Repository_MigrationRecord_UpdateRollback` | Update migration record with rollback (FileDown) metadata and progress |
| `Repository_MigrationRecord_UpdateHash` | Update hash fields (used by Update-Hash command) |
| `Repository_MigrationRecord_Select` | Query existing migration records for filtering and rollback |
| `Repository_MigrationRun_SelectOrphaned` | Fix command: select orphaned runs (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix) |
| `Repository_MigrationRun_FixOrphaned` | Fix command: mark orphaned MigrationRun as Error (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix) |
| `Repository_MigrationRecord_FixOrphaned` | Fix command: update orphaned Migration entries (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix) |
| `Repository_MigrationRun_Select` | Query MigrationRun records (used by Info command) |

---

## Related Documentation

- [Template System](template-system.md) - Template types, placeholders, and conventions
- [Repository Schema](repository-schema.md) - Table structures
- [Logging Schema](logging-schema.md) - Logging table structures
- [DAL Architecture](dal-architecture.md) - Database access layer
- [Data Flow](../01-architecture/data-flow.md) - Overall data flow
