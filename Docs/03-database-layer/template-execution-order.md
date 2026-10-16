# SQL Template Execution Order on Initial Startup

This document describes the exact order in which SQL templates are executed when RayMigrator starts for the first time with no existing database structures (no repository tables, no logging tables).

**Command:** `raymigrator migrate-up --product X --environment Y --run-mode migrate`

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

**Call site:** `DirectModePipeline.cs` (in Pipeline project) -> `DatabaseLogWriter.InitDatabaseLoggerAsync()`
**Target database:** Logging database (from `DatabaseLogging` configuration section)

This phase executes unless the `DatabaseLogging` section says `Enabled: false`, regardless of run mode; without a section the log database is the repository (the defaults fill the section from `Repository`). A host such as RayMigrator Studio initializes the writer of its log database the same way.

### Template 1: `DatabaseLogging_CheckCreate`

**File:** `DataAccessLayers/{DB}/DatabaseLogging_CheckCreate.sql`
**Purpose:** Checks if logging infrastructure exists; creates it if missing.

**What is created:**

> **Note**: SQL Server wraps this in a transaction under `sp_getapplock`, PostgreSQL in one `DO` block under an advisory lock; both locks are apart from the repository schema lock. MariaDB and MySQL use `CREATE TABLE IF NOT EXISTS` instead, since DDL causes implicit commits in MariaDB/MySQL; the `MigrationEvent` catalogue is inserted only when the log tables did not exist before the run (`WHERE @v_exists = 0`). MySQL serialises a parallel start with `GET_LOCK()`. MariaDB and SQLite take no lock, so two first starts can both find no log table: MariaDB inserts the catalogue with `ON DUPLICATE KEY UPDATE` and SQLite with `INSERT OR IGNORE`, and the second start keeps the rows of the first. The wait for the lock ends with the command timeout of `DatabaseLogging` (20 seconds by default); only when that timeout is longer do SQL Server and MySQL return `-1` after 60 seconds without the lock, and PostgreSQL waits for its advisory lock until the timeout.

1. **Schema** (e.g., `[ray]`) if it does not exist (SQL Server/PostgreSQL only; MariaDB/MySQL uses the database itself)
2. **`MigrationEvent` table** (lookup) - Defines event types with IDs:
   | Id | Name |
   |----|------|
   | 0 | UnspecifiedEvent |
   | 10 | CommandLineParsing |
   | 20 | EnvironmentVariableReplacement |
   | 31 | CreateDatabaseLogger |
   | 40 | ValidateRayMigratorOptions |
   | 50 | CreateApplicationHost |
   | 60 | InitializeDalSpecificProperties |
   | 70 | ValidateConnectionStrings |
   | 80 | RayMigratorServiceStart |
   | 100 | TemplateExecutionRepositoryCheckCreate |
   | 101 | TemplateExecutionRepositoryUpgrade |
   | 102 | TemplateExecutionRepositoryMigratorMetaCheckInsert |
   | 110 | TemplateExecutionRepositoryMigrationRunInsert |
   | 111 | TemplateExecutionRepositoryMigrationRunUpdate |
   | 112 | TemplateExecutionRepositoryMigrationRunSelectOrphaned |
   | 113 | TemplateExecutionRepositoryMigrationRunFixOrphaned |
   | 114 | TemplateExecutionRepositoryMigrationFixOrphaned |
   | 115 | TemplateExecutionRepositoryMigrationRunHeartbeat |
   | 120 | TemplateExecutionRepositoryProductCheckInsert |
   | 121 | TemplateExecutionRepositoryEnvironmentCheckInsert |
   | 122 | TemplateExecutionRepositoryProductSelect |
   | 123 | TemplateExecutionRepositoryEnvironmentSelect |
   | 130 | TemplateExecutionRepositoryMigrationInsert |
   | 131 | TemplateExecutionRepositoryMigrationUpdate |
   | 132 | TemplateExecutionRepositoryMigrationGetInterrupted |
   | 133 | TemplateExecutionRepositoryMigrationUpdateRollback |
   | 134 | TemplateExecutionRepositoryMigrationSelect |
   | 135 | TemplateExecutionRepositoryMigrationUpdateHash |
   | 136 | TemplateExecutionRepositoryMigrationRunSelect |
   | 137 | TemplateExecutionRepositoryMigrationRecordHistorySelect |
   | 1000 | RayMigratorServiceShutdown |
3. **`MigrationLog` table** (data) - Stores all log entries with columns:
   - `Id` (BIGINT IDENTITY), `LogLevelId`, `MigrationEventId`, `RunModeId`, `ProductId`, `EnvironmentId`, `MigrationRunId`, `MigrationRecordId`
   - `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`
   - `Filename`, `FileOrderId`, `FileBlockId`, `Message`, `CreatedAt`
4. **Two indexes** on `MigrationLog`, `ix_{CFG:TableBaseName}MigrationLog_MigrationRunId` and `ix_{CFG:TableBaseName}MigrationLog_CreatedAt` (snake_case on PostgreSQL, MySQL and MariaDB), outside the creation branch: each is created when it is missing, also on a log table an older engine created. The start that adds them needs the right to create indexes once (see [Logging Schema: Indexes](logging-schema.md#indexes)); so even a `Validate` run can execute this DDL once after an update.

**Return value:** `1` = newly created, `0` = already existed (also when only the indexes were added)

**Retries:** `InitDatabaseLoggerAsync` retries a transient failure of the template up to three times, as every log write, but not a timeout (`DalBase.IsTimeout`): an attempt that timed out was rolled back whole, and a retry would build the indexes again. The start then fails with a message that names the index build and `DatabaseLogging.DbCommandTimeoutInSeconds` (see [Logging Schema: Indexes](logging-schema.md#indexes)).

### Template 2: `DatabaseLogging_Insert` (Loaded, Not Yet Executed)

**File:** `DataAccessLayers/{DB}/DatabaseLogging_Insert.sql`
**Purpose:** INSERT statement for log entries. Loaded into memory by `InitDatabaseLoggerAsync()` and stored in `DatabaseLogWriter._templateLoggingInsert`.

This template is **not executed immediately**. It is first used when the Serilog `RayMigratorDatabaseSink` receives a log event after `databaseSink.SetWriter(dbLogWriter)` is called in `DirectModePipeline.cs`. From that point on, log events that meet the configured `MinimumLevel` are enqueued via `DatabaseLoggerQueue` for asynchronous execution.

---

## Phase 2: Repository Infrastructure

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryCheckCreateAsync()` -> `RepositorySchemaExecutor.EnsureAsync()`
**Target database:** Repository database (from `Repository` configuration section)

### Template 3: `Repository_CheckCreate`, `Repository_Upgrade_{n}` and `Repository_MigratorMeta_CheckInsert`

**Files:** `DataAccessLayers/{DB}/Repository_CheckCreate.sql`, `Repository_Upgrade_{n}.sql`, `Repository_MigratorMeta_CheckInsert.sql`
**Purpose:** Brings the repository to the schema version of the engine and registers the engine version (ADR-024). `RepositorySchemaExecutor` orchestrates the three templates; `TemplateExecutor.RepositoryCheckCreateAsync` runs it with the repository, the target version and the engine version of the current context. A host runs the same steps without a run through `IRepositorySchemaService.EnsureAsync(RepositoryOptions)`. The repository writes of a run take no cancellation token, these included, so a cancel never stops a schema step halfway.

1. `Repository_CheckCreate` (no parameters) creates the schema (SQL Server, PostgreSQL) and the `RepositorySchemaVersion` table when they are missing and returns `'schemaVersion,unversionedTables,message'`: the highest applied version (`0` = none) and `1` when the `MigratorMeta` table exists. Version `0` with existing tables marks a repository of a RayMigrator before 0.17.0, which is refused with `RepositorySchemaException`; a version above `RepositorySchema.CurrentVersion` is refused as well. Neither case runs a schema step or writes a row; `Repository_CheckCreate` may have created the empty version table.
2. Every missing `Repository_Upgrade_{n}` from version + 1 to the target version runs in order (parameter `@RayMigratorVersion`, EventId `101`). Each step is idempotent per engine, serialised against a parallel start, returns its own version number and writes its row of `RepositorySchemaVersion`. A fresh repository runs every step from 1. A created or upgraded repository is logged at Information level.
3. `Repository_MigratorMeta_CheckInsert` (parameters `@RayMigratorVersion`, `@RepositoryDatabaseType`, EventId `102`) registers the engine version in `MigratorMeta` and returns the `MigratorMetaId`, stored in `MigrationContext.MigrationState.MigratorMetaId`.

**What schema step 1 (`Repository_Upgrade_1`) creates:**
**What is created:**

> **Note**: SQL Server runs the step in one transaction under `sp_getapplock`, PostgreSQL in one `DO $$` block under `pg_advisory_xact_lock`; both create the tables only when `MigratorMeta` does not exist. MariaDB and MySQL use `CREATE TABLE IF NOT EXISTS` with inline FK constraints and `INSERT IGNORE` for the master data under `GET_LOCK()`, since DDL causes implicit commits and cannot be wrapped in a transaction; SQLite uses `CREATE TABLE IF NOT EXISTS` and `INSERT OR IGNORE`. Every step is idempotent, so a rerun after a failure completes what is missing; on MariaDB and MySQL, where a step that lost its connection midway leaves committed tables behind, `Repository_CheckCreate` treats a repository as pre-0.17.0 only when `MigratorMeta` has rows and no version row exists, so an empty, half-created repository is completed and not refused. On MariaDB and MySQL every schema template reports `-32` when it could not acquire `GET_LOCK()` within 60 seconds, and writes neither the version row nor the `MigratorMeta` row without the lock.

1. **Schema** (e.g., `[ray]`) if it does not exist (SQL Server/PostgreSQL only; MariaDB/MySQL uses the database itself)

2. **4 Lookup tables:**
   - `MigrationOperation` (`tinyint Id`, `Name`, `Description`)
   - `MigrationRunResult` (`tinyint Id`, `Name`, `Description`)
   - `MigrationRunMode` (`tinyint Id`, `Name`, `Description`)
   - `MigrationStatus` (`tinyint Id`, `Name`, `Description`)

3. **7 Data tables:**
   - `MigratorMeta` (`Id` IDENTITY, `RayMigratorVersion`, `RepositoryDatabaseType`, `CreatedAt`)
   - `Product` (`Id` IDENTITY, `Name`, `NameLower` UNIQUE, `CreatedAt`)
   - `Environment` (`Id` IDENTITY, `Name`, `NameLower` UNIQUE, `CreatedAt`)
   - `MigrationRun` (`Id` IDENTITY, `MigratorMetaId` FK, `ProductId` FK, `EnvironmentId` FK, `MigrationRunModeId` FK, `MigrationRunResultId` FK, `FromReleaseVersion`, `ToReleaseVersion`, `StartedAt`, `FinishedAt`, `DurationInMs`)
   - `MigrationRunMeta` (`MigrationRunId` FK, `MigrationRunSettingsJson`, `Description`)
   - `MigrationRecord` (`Id` IDENTITY, `ProductId` FK, `EnvironmentId` FK, `MigrationRunId` FK, various FKs, `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`, `Filename`, `FileOrderId`, hash fields, block tracking fields, timestamps)
   - `MigrationRecordHistory` (mirrors `MigrationRecord` with additional `MigrationRecordId` FK and `HistorizedAt`)

4. **Indexes:** Three indexes are created on all engines — shown here in canonical (SQL Server/SQLite) PascalCase and without the `{CFG:TableBaseName}` prefix that the templates write after every `ix_` and `uix_` (`uix_{CFG:TableBaseName}Product_NameLower`); PostgreSQL, MariaDB, and MySQL use the snake_case equivalents: `uix_product_name_lower`, `uix_environment_name_lower`, `ix_migration_record_history_migration_record_id`.
   - `uix_Product_NameLower` on `Product(NameLower)` (unique, case-insensitive deduplication)
   - `uix_Environment_NameLower` on `Environment(NameLower)` (unique, case-insensitive deduplication)
   - `ix_MigrationRecordHistory` on `MigrationRecordHistory(MigrationRecordId)` (fast lookup by source record)

   **PostgreSQL only** additionally creates FK-column indexes (snake_case names per DAL-017): `ix_migration_run_migrator_meta_id`, `ix_migration_run_product_id`, `ix_migration_run_environment_id`, `ix_migration_run_migration_run_mode_id`, `ix_migration_run_migration_run_result_id`, `ix_migration_record_product_id`, `ix_migration_record_environment_id`, `ix_migration_record_migration_run_id`, `ix_migration_record_migration_run_mode_id`, `ix_migration_record_migration_operation_id`, `ix_migration_record_migration_status_id`. MySQL/MariaDB InnoDB auto-indexes FK columns; PostgreSQL and SQL Server do not (FK indexing on SQL Server is a candidate for future work).

5. **15 Foreign Keys** between the tables, named `fk_{CFG:TableBaseName}...`; SQL Server and PostgreSQL name the primary keys `pk_{CFG:TableBaseName}...` (MySQL, MariaDB and SQLite leave them unnamed)

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
   | 110 | Baseline | Marking migration files as migrated without executing them (baseline command) |

   **MigrationRunResult** (used by MigrationRun only):
   | Id | Name | Description |
   |----|------|-------------|
   | 0 | Undefined | Invalid value; result has not been set |
   | 10 | Running | Migration process is currently running |
   | 50 | PartialSuccess | Migration(s) finished but at least one file was skipped or left Failed |
   | 80 | Recovered | Migration(s) failed and the configured error recovery rolled back cleanly |
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

8. **RepositorySchemaVersion row 1** with `AppliedAt` and the `RayMigratorVersion` that applied the step

**What schema step 2 (`Repository_Upgrade_2`) adds:** the lookup table `MigrationRunSource` (Cli = 10, Ci = 20, Studio = 30) and the nullable run detail columns `MigrationOperationId`, `MigrationRunSourceId`, `InitiatedBy`, `InitiatedBySubject`, `Host`, `ErrorCode`, `ErrorMessage` and `LastHeartbeatAt` on `MigrationRun` with foreign keys to `MigrationOperation` and `MigrationRunSource` (named `fk_{CFG:TableBaseName}...` except on SQLite, which declares them unnamed on the added columns; PostgreSQL also indexes the two columns), plus version row 2.

**What schema step 3 (`Repository_Upgrade_3`) adds:** the row `Cancelled` (70) of `MigrationRunResult` and four indexes for the readers of the repository, `ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt` (`ProductId`, `EnvironmentId`, `StartedAt`), `ix_{CFG:TableBaseName}MigrationRecordHistory_MigrationRunId`, `ix_{CFG:TableBaseName}MigrationRecordHistory_HistorizedAt` and `ix_{CFG:TableBaseName}MigrationRecord_Product_Environment` (`ProductId`, `EnvironmentId`), in snake_case on PostgreSQL, MySQL and MariaDB, plus version row 3. The step is idempotent and serialised like step 2.

**Return values:** `Repository_CheckCreate` returns `'schemaVersion,unversionedTables,message'`; `Repository_Upgrade_{n}` returns `'n,message'`; `Repository_MigratorMeta_CheckInsert` returns the `MigratorMetaId` (positive integer, `-12` when several `MigratorMeta` rows match), stored in `MigrationContext.MigrationState.MigratorMetaId`

---

## Phase 3: Product Registration

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryProductCheckInsertAsync()`
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

**Call site:** `MigrationService` entry point -> `TemplateExecutor.RepositoryEnvironmentCheckInsertAsync()`
**Target database:** Repository database

Called immediately after Product registration by every `MigrationService` entry point whose command profile writes the repository: `MigrateUpAsync` and `MigrateDownAsync` in `Migrate` run mode (directly), and `BaselineAsync`, `UpdateHashAsync` and `FixIssuesAsync` in `Migrate` run mode (via `InitializeRepositoryAsync()`). The read-only paths (`MigrateUpAsync` / `MigrateDownAsync` in `Simulate` run mode, `ValidateHashAsync`, `GetStatusAsync`, `GetHistoryAsync`, `FixIssuesAsync` in `Simulate` run mode) execute `Repository_Product_Select` / `Repository_Environment_Select` instead, so they leave no rows behind (#6).

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

**Call site:** `MigrationService.MigrateUpAsync()` -> `TemplateExecutor.RepositoryMigrationGetInterruptedAsync()`
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

**Call site:** `MigrationService.MigrateUpAsync()` -> `MigrationService.RepositoryMigrationRunInsertWithAutoFix()` -> `TemplateExecutor.RepositoryMigrationRunInsertAsync()`
**Target database:** Repository database

`MigrateUpAsync` does not call `RepositoryMigrationRunInsertAsync` directly. Instead, it calls the internal wrapper `RepositoryMigrationRunInsertWithAutoFix(settingsJson)`, which first attempts the insert. If a `MigrationAlreadyRunningException` is thrown (error code `-2`), the wrapper checks for open runs whose last heartbeat (or start, without one) is older than `Repository.OrphanedRunThresholdInMinutes` (default 2) and auto-fixes them before retrying the insert once. If no orphaned runs are found, the original exception is rethrown.

### Template 5: `Repository_MigrationRun_Insert`

**File:** `DataAccessLayers/{DB}/Repository_MigrationRun_Insert.sql`
**Purpose:** Creates a new MigrationRun record. Prevents parallel migrations for the same product/environment (the run mode is stored, not checked).

**SQL parameters:**
- `@ProductId` - From `MigrationState.ProductId`
- `@EnvironmentId` - From `MigrationState.EnvironmentId`
- `@MigrationRunModeId` - From console options `RunMode` (cast to byte)
- `@MigratorMetaId` - From `MigrationState.MigratorMetaId`
- `@MigrationRunResultId` - Set to `Running` (10) from `MigrationState.MigrationRunResult`
- `@FromReleaseVersion` - From `MigrationState.FromReleaseVersion`: the highest Migrated release of the product and environment when the run started, read right before the insert; `NULL` when nothing was migrated yet
- `@ToReleaseVersion` - From console options `TargetReleaseVersion`
- `@MigrationRunSettingsJson` - JSON snapshot of all RayMigrator settings at migration start
- `@MigrationOperationId` - What the run does: 50=MigrateDown, 100=MigrateUp, 110=Baseline (schema version 2)
- `@MigrationRunSourceId` - Where the run was started: 10=Cli, 20=Ci, 30=Studio; NULL when the host did not say
- `@InitiatedBy`, `@InitiatedBySubject`, `@Host` - Who started the run where (the CLI binds the OS user and the machine name; Studio the signed-in user)

**Logic:**
1. Checks if an unfinished MigrationRun exists (`FinishedAt IS NULL`) for the same product/environment
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
| 1 | `DatabaseLogging_CheckCreate` | Logging DB | `DirectModePipeline.cs` startup (when `DatabaseLogging` configured) | Creates schema + `MigrationEvent` + `MigrationLog` tables, and the two log indexes when they are missing |
| 2 | `DatabaseLogging_Insert` | Logging DB | From startup onward, async (when `DatabaseLogging` configured) | Writes log entries (loaded in Phase 1, executed after `SetWriter`) |
| 3 | `Repository_CheckCreate`, `Repository_Upgrade_{n}`, `Repository_MigratorMeta_CheckInsert` | Repository DB | `MigrateUpAsync` (`TemplateExecutor.RepositoryCheckCreateAsync` -> `RepositorySchemaExecutor`) | Reports the schema version, applies the missing steps (step 1: schema + 11 tables + 15 FKs + master data; steps 2 and 3 up to version 3), registers the engine version, returns MigratorMetaId |
| 4 | `Repository_Product_CheckInsert` | Repository DB | `MigrateUpAsync` | Registers the product, returns ProductId |
| 4b | `Repository_Environment_CheckInsert` | Repository DB | All repository-writing entry points (after Product check-insert): `MigrateUpAsync` / `MigrateDownAsync` / `BaselineAsync` / `UpdateHashAsync` / `FixIssuesAsync` in `Migrate` run mode | Registers the environment, returns EnvironmentId |
| 4a/4b (read-only) | `Repository_Product_Select`, `Repository_Environment_Select` | Repository DB | `MigrateUpAsync` / `MigrateDownAsync` / `FixIssuesAsync` in `Simulate` run mode, `ValidateHashAsync`, `GetStatusAsync`, `GetHistoryAsync`, instead of the two CheckInsert templates | Resolves ProductId/EnvironmentId read-only; `0` when not registered, in which case all files are treated as pending (#6, #7) |
| 4c | `Repository_MigrationRecord_GetInterrupted` | Repository DB | `MigrateUpAsync` | Checks for interrupted migrations (informational) |
| 5 | `Repository_MigrationRun_Insert` | Repository DB | `MigrateUpAsync` (via `RepositoryMigrationRunInsertWithAutoFix`) | Creates MigrationRun record, returns MigrationRunId |

---

## Templates NOT Executed on Initial Startup

These templates exist in the template cache but are only used during specific operations:

| Template | When Used |
|----------|-----------|
| `Repository_Drop` | Explicit repository deletion |
| `Repository_MigrationRun_Heartbeat` | Every `Repository.HeartbeatIntervalInSeconds` while a migrate-up, migrate-down or baseline runs: refreshes `LastHeartbeatAt` of the open run row (its own connection, failures are warnings) |
| `Repository_MigrationRun_Update` | After completion, error or cancel of a MigrationRun: stores the result, `FinishedAt`, `DurationInMs` and, since schema version 2, `@ErrorCode` and `@ErrorMessage` of the failure that decided a result other than Ok (a cancelled run: `Cancelled` with `1004`, or `Error` with `1005`) |
| `Repository_MigrationRecord_Insert` | Per migration file during actual migration execution |
| `Repository_MigrationRecord_Update` | Block progress or completion per file |
| `Repository_MigrationRecord_UpdateRollback` | Update migration record with rollback (FileDown) metadata and progress |
| `Repository_MigrationRecord_UpdateHash` | Update hash fields (used by update-hash command) |
| `Repository_MigrationRecord_Select` | Query existing migration records for filtering, rollback and the status (pending list, `LastMigrationDate` from `FinishedAt`) |
| `Repository_MigrationRun_SelectOrphaned` | `FixIssuesAsync` (fix command): select orphaned runs (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix) |
| `Repository_MigrationRun_FixOrphaned` | `FixIssuesAsync` in `Migrate` run mode: mark orphaned MigrationRun as Error (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix) |
| `Repository_MigrationRecord_FixOrphaned` | `FixIssuesAsync` in `Migrate` run mode: settle the open Migration entries of the run by their progress, or set the status of `--last-migration-status` on all of them (also used by `RepositoryMigrationRunInsertWithAutoFix` for auto-fix, always by progress) |
| `Repository_MigrationRun_Select` | Query MigrationRun records (`GetStatusAsync` reads the latest run, `GetHistoryAsync` the last `limit` runs; both back the info command) |
| `Repository_MigrationRecordHistory_Select` | Query `MigrationRecordHistory` rows (`GetHistoryAsync`, info command); returns the same columns as `Repository_MigrationRecord_Select`, `StartedAt` and `FinishedAt` included |
| `Repository_MigrationRecordHistory_Delete`, `Repository_MigrationRecordHistory_Count`, `DatabaseLogging_Delete`, `DatabaseLogging_Count` | Never in a command: `IRepositoryMaintenanceService.CleanUpAsync` runs them through `RepositoryMaintenanceExecutor` against the repository database, without a migration context and without the retries of the repository, to delete or count old history and log rows batch by batch; before the cleanup deletes from the log, `DatabaseLogging_Count` with `@ExistsOnly` = 1 only reports whether the log table is there (see [Repository Schema: Retention](repository-schema.md#retention)) |

---

## Related Documentation

- [Template System](template-system.md) - Template types, placeholders, and conventions
- [Repository Schema](repository-schema.md) - Table structures
- [Logging Schema](logging-schema.md) - Logging table structures
- [DAL Architecture](dal-architecture.md) - Database access layer
- [Data Flow](../01-architecture/data-flow.md) - Overall data flow
