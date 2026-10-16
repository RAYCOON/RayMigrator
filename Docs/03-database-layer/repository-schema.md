# Repository Schema

The repository database stores migration tracking data. This schema is created automatically by RayMigrator and, since the schema is versioned, upgraded automatically by the first command of a newer RayMigrator (ADR-024, see [Schema versioning](#schema-versioning)).

> **Breaking change (2026-10-13):** The repository schema is versioned. `Repository_CheckCreate` creates only the new `RepositorySchemaVersion` table and reports the version found; the numbered steps `Repository_Upgrade_{n}` create the schema (step 1 = the 11 tables below with their master data) and the engine applies every missing step on the next command. `Repository_MigratorMeta_CheckInsert` registers the engine version. Repositories created by 0.16.0 or earlier have no schema version and are refused with `RepositorySchemaException`: drop their tables once and let this version create them again. The table count checks and their codes `-10` and `-11` are gone. From this version on a schema change keeps the data.
>
> **Schema version 2 (2026-10-13):** `MigrationRun` records what a run does and who started it where: `MigrationOperationId`, `MigrationRunSourceId` (new lookup table `MigrationRunSource`: Cli = 10, Ci = 20, Studio = 30), `InitiatedBy`, `InitiatedBySubject`, `Host`, `ErrorCode`, `ErrorMessage` and `LastHeartbeatAt`. All eight columns are nullable; `Repository_Upgrade_2` adds them to a repository of version 1 and keeps its rows.
>
> **Schema version 3 (2026-10-16):** `Repository_Upgrade_3` adds the run result `Cancelled` (70) to the `MigrationRunResult` lookup and four indexes for the readers of the repository: runs per product, environment and start, history rows per run and per time, records per product and environment (see [Indexes](#indexes)). The step keeps the rows of version 2; engines before 0.20.0 refuse a repository of version 3.

> **Breaking change (2026-04-16):** A new `Environment` repository table was added (table count: 10 → 11). Existing repositories must be dropped and recreated.
>
> **Breaking change (2026-04-17):** The `Product` table was refactored: the `Description` column was removed and a new `NameLower` column with a unique index (`uix_Product_NameLower`) was added for case-insensitive deduplication. The inline unique constraint on `Name` was removed. Existing repositories must be dropped and recreated.
>
> **Breaking change (2026-04-17):** PostgreSQL repositories — audit columns changed from `TIMESTAMP` to `TIMESTAMPTZ`; all string columns changed from `VARCHAR(n)` to `TEXT`; writes simplified from `NOW() AT TIME ZONE 'UTC'` to `NOW()`. `RepositoryVersion` bumped to `2026-04-17.3`. Existing PG repositories must be dropped and recreated (see DAL-012 + DAL-013 in the audit log).
>
> **Breaking change (2026-04-17):** MySQL/MariaDB repositories — audit columns changed from `DATETIME` to `TIMESTAMP`; all `CREATE TABLE` statements now pin `DEFAULT CHARSET=utf8mb4` with engine-specific collation (`utf8mb4_0900_ai_ci` on MySQL, `utf8mb4_unicode_ci` on MariaDB); writes simplified from `UTC_TIMESTAMP()` to `CURRENT_TIMESTAMP` (the DAL enforces session `time_zone='+00:00'` on every connection open, so `CURRENT_TIMESTAMP` writes UTC). `RepositoryVersion` bumped to `2026-04-17.2` for both engines. Existing MySQL/MariaDB repositories must be dropped and recreated (see DAL-014 + DAL-015 in the audit log).
>
> **Breaking change (2026-04-17):** SqlServer repositories — 12 columns changed from `VARCHAR(n)`/`VARCHAR(MAX)` to `NVARCHAR(n)`/`NVARCHAR(MAX)` for Unicode safety: 5 lookup `Name` columns (`MigrationOperation`, `MigrationRunResult`, `MigrationRunMode`, `MigrationStatus`, `MigrationEvent`), 3 `MigratorMeta` metadata columns, and 4 `*ConfigJson` TOML payloads on `MigrationRecord` + `MigrationRecordHistory`. SHA256 hash columns remain `VARCHAR(100)` (ASCII-hex). `RepositoryVersion` bumped to `2026-04-17.3`. Existing SqlServer repositories must be dropped and recreated (see DAL-016 in the audit log).
>
> **Breaking change (2026-04-17 or later):** PostgreSQL repositories — all tables, columns, constraints, and indexes converted from quoted PascalCase to unquoted snake_case for community-standard compliance. `RepositoryVersion` bumped to `2026-04-17.4`. Existing PostgreSQL repositories must be dropped and recreated (see DAL-017 in the audit log).
>
> **Breaking change (2026-04-17):** MySQL and MariaDB repositories — all 36 SQL templates converted from backtick-quoted PascalCase (`` `MigrationRecord` ``) to unquoted snake_case (`migration_record`). Final MySQL/MariaDB table and column names match PostgreSQL exactly, including the `RayMigrator` brand-token exception (`created_by_raymigrator_version`). `RepositoryVersion` bumped to `2026-04-17.3` for both engines. `RayMigratorOptionsValidator` now rejects any uppercase character in `TableBaseName` for MariaDB and MySQL (same rule as PostgreSQL). Existing MySQL/MariaDB repositories must be dropped and recreated (see DAL-018 in the audit log).
>
> **Breaking change (2026-09-09):** Lookup master data (`MigrationRunMode`, `MigrationOperation`, `MigrationRunResult`, `MigrationStatus`) is written only when the repository is created, on all 5 DALs. The idempotent upgrade blocks that back-filled rows added after the initial release (`Baseline`, `PartialSuccess`, `Recovered`) into existing SqlServer/PostgreSQL repositories were removed, and MySQL, MariaDB and SQLite no longer re-seed on every start. `MigratorMeta` lost `CreatedByRayMigratorVersion`; its `RepositoryVersion` column (formerly a hand-maintained constant) became `RayMigratorVersion` and holds the RayMigrator version that used the repository, the first row being the version that created it and therefore its schema. Existing repositories must be dropped and recreated.
>
> **Breaking change (2026-04-18):** The `Environment` text column was removed from `MigrationRun`, `MigrationRecord`, `MigrationRecordHistory`, and `MigrationLog` (all 5 DALs). It is replaced by an `EnvironmentId` INT FK column positioned immediately after `ProductId` in each table. The FK references the `Environment` lookup table and carries the constraint name `fk_MigrationRun_Environment`, `fk_MigrationRecord_Environment`, or `fk_MigrationRecordHistory_Environment` (SQL Server / SQLite PascalCase; PostgreSQL / MariaDB / MySQL use the snake_case equivalents `fk_migration_run_environment`, etc.). `MigrationLog` has the `EnvironmentId` column but carries no FK (consistent with the `ProductId` precedent in the logging schema). PostgreSQL creates an additional index `ix_{TableBaseName}migration_run_environment_id` and `ix_{TableBaseName}migration_record_environment_id` on the new FK columns. `RepositoryVersion` bumped on all 5 engines to trigger the `-12 Multiple MigratorMeta-entries` guard path. Existing repositories must be dropped and recreated.

## Entity Relationship Diagram

```mermaid
erDiagram
    MigratorMeta ||--o{ MigrationRun : "tracks version"
    Product ||--o{ MigrationRun : "has runs"
    Product ||--o{ MigrationRecord : "has migrations"
    Environment ||--o{ MigrationRun : "environment"
    Environment ||--o{ MigrationRecord : "environment"
    Environment ||--o{ MigrationRecordHistory : "environment"
    MigrationRun ||--o{ MigrationRecord : "contains"
    MigrationRun ||--|| MigrationRunMeta : "has metadata"
    MigrationRecord ||--o{ MigrationRecordHistory : "has history"

    MigrationRunMode ||--o{ MigrationRun : "mode"
    MigrationRunMode ||--o{ MigrationRecord : "mode"
    MigrationOperation ||--o{ MigrationRecord : "operation"
    MigrationRunResult ||--o{ MigrationRun : "result"
    MigrationOperation ||--o{ MigrationRun : "operation"
    MigrationRunSource ||--o{ MigrationRun : "source"
    MigrationStatus ||--o{ MigrationRecord : "status"

    RepositorySchemaVersion {
        int Version PK
        datetime2 AppliedAt
        nvarchar RayMigratorVersion
    }

    MigratorMeta {
        int Id PK
        nvarchar RayMigratorVersion
        nvarchar RepositoryDatabaseType
        datetime2 CreatedAt
    }

    Product {
        int Id PK
        nvarchar Name
        nvarchar NameLower UK
        datetime2 CreatedAt
    }

    Environment {
        int Id PK
        nvarchar Name
        nvarchar NameLower UK
        datetime2 CreatedAt
    }

    MigrationRun {
        int Id PK
        int MigratorMetaId FK
        int ProductId FK
        int EnvironmentId FK
        tinyint MigrationRunModeId FK
        tinyint MigrationRunResultId FK
        nvarchar FromReleaseVersion
        nvarchar ToReleaseVersion
        datetime2 StartedAt
        datetime2 FinishedAt
        bigint DurationInMs
    }

    MigrationRunMeta {
        int MigrationRunId PK_FK
        nvarchar MigrationRunSettingsJson
        nvarchar Description
    }

    MigrationRecord {
        int Id PK
        int ProductId FK
        int EnvironmentId FK
        int MigrationRunId FK
        tinyint MigrationRunModeId FK
        tinyint MigrationOperationId FK
        tinyint MigrationStatusId FK
        nvarchar ReleaseVersion
        nvarchar TargetGroupAlias
        nvarchar TargetAlias
        nvarchar Filename
        int FileOrderId
        varchar FileUpHash
        varchar FileUpConfigHash
        varchar FileUpBlocksHash
        int FileUpBlocksMigrated
        int FileUpBlocksTotal
        nvarchar FileUpConfigJson
        bit MigrateDownFileExists
        varchar FileDownHash
        varchar FileDownConfigHash
        varchar FileDownBlocksHash
        int FileDownBlocksMigrated
        int FileDownBlocksTotal
        nvarchar FileDownConfigJson
        datetime2 StartedAt
        datetime2 FinishedAt
        bigint DurationInMs
    }

    MigrationRecordHistory {
        int Id PK
        int MigrationRecordId
        int ProductId FK
        int EnvironmentId FK
        int MigrationRunId FK
        tinyint MigrationRunModeId FK
        tinyint MigrationOperationId FK
        tinyint MigrationStatusId FK
        nvarchar ReleaseVersion
        nvarchar TargetGroupAlias
        nvarchar TargetAlias
        nvarchar Filename
        int FileOrderId
        varchar FileUpHash
        varchar FileUpConfigHash
        varchar FileUpBlocksHash
        int FileUpBlocksMigrated
        int FileUpBlocksTotal
        nvarchar FileUpConfigJson
        bit MigrateDownFileExists
        varchar FileDownHash
        varchar FileDownConfigHash
        varchar FileDownBlocksHash
        int FileDownBlocksMigrated
        int FileDownBlocksTotal
        nvarchar FileDownConfigJson
        datetime2 StartedAt
        datetime2 FinishedAt
        bigint DurationInMs
        datetime2 HistorizedAt
    }

    MigrationRunMode {
        tinyint Id PK
        nvarchar Name
        nvarchar Description
    }

    MigrationOperation {
        tinyint Id PK
        nvarchar Name
        nvarchar Description
    }

    MigrationRunResult {
        tinyint Id PK
        nvarchar Name
        nvarchar Description
    }

    MigrationStatus {
        tinyint Id PK
        nvarchar Name
        nvarchar Description
    }
```

## Tables Overview

`Repository_CheckCreate` creates the `RepositorySchemaVersion` table; the schema steps `Repository_Upgrade_{n}` create the **12 repository tables** (5 lookup + 7 data): step 1 the 11 tables of the first schema, step 2 the `MigrationRunSource` lookup and the run detail columns of `MigrationRun`, step 3 the run result `Cancelled` and four indexes (ADR-024).

| Table | Purpose | Status |
|-------|---------|--------|
| `RepositorySchemaVersion` | One row per applied schema version (ADR-024) | Created by `Repository_CheckCreate` |
| `MigratorMeta` | RayMigrator versions that used the repository | Created |
| `Product` | Registered products | Created |
| `Environment` | Registered environments | Created |
| `MigrationRun` | Migration run sessions | Created |
| `MigrationRunMeta` | Run metadata (JSON settings) | Created |
| `MigrationRecord` | Individual migration records | Created |
| `MigrationRecordHistory` | Terminal state transitions of the records (Failed, NotMigrated, Migrated), read by the `info` run history; old rows go only on a host's request ([Retention](#retention)) | Created |
| `MigrationRunMode` | Lookup: Validate/Simulate/Migrate | Created |
| `MigrationOperation` | Lookup: Rollback/MigrateDown/MigrateUp | Created |
| `MigrationRunResult` | Lookup: Running/PartialSuccess/Cancelled/Recovered/Error/Ok (used by MigrationRun; `Cancelled` since schema version 3) | Created |
| `MigrationRunSource` | Lookup: Cli/Ci/Studio (where a run was started; schema version 2) | Created by `Repository_Upgrade_2` |
| `MigrationEvent`, `MigrationLog` | The structured run log; in the repository by default (see [Logging schema](logging-schema.md)) | Created by `DatabaseLogging_CheckCreate` |
| `MigrationStatus` | Lookup: Pending/Executing/Failed/NotMigrated/Migrated | Created |

## Core Tables

### RepositorySchemaVersion

One row per applied schema version (ADR-024). `Repository_CheckCreate` creates the table when it is missing and reports `MAX(Version)`; each step `Repository_Upgrade_{n}` writes its row after its DDL. The highest `Version` is the schema the repository has; `RepositorySchema.CurrentVersion` in `Raycoon.RayMigrator.Core` is the schema the engine expects. The table has no foreign keys.

| Column | Type | Description |
|--------|------|-------------|
| `Version` | INT | Primary key: the schema version the step created |
| `AppliedAt` | DATETIME2(3) | When the step ran (UTC) |
| `RayMigratorVersion` | NVARCHAR(100) | RayMigrator version that applied the step |

### MigratorMeta

Lists the RayMigrator versions that have used the repository. `Repository_MigratorMeta_CheckInsert` inserts a row on the first run of each RayMigrator version (per `RepositoryDatabaseType`) after the schema steps ran, and every `MigrationRun` references the row of the version that executed it. The first row (lowest `Id`) is the version that created the repository; the schema is identified by `RepositorySchemaVersion`, not by this table. Release builds write the bare version (`0.14.0`); builds outside the release workflows write the pre-release form with the commit hash (`0.14.0-dev+39b6fa3`), so a repository created from `develop` names the exact commit whose templates created it.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `RayMigratorVersion` | NVARCHAR(100) | RayMigrator version that used the repository; the first row is the version that created it |
| `RepositoryDatabaseType` | NVARCHAR(100) | Database type (SqlServer, etc.) |
| `CreatedAt` | DATETIME2(3) | Creation timestamp (UTC) |

### Product

Registered products for migration tracking. Populated automatically via `Repository_Product_CheckInsert` at the start of each operation.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `Name` | NVARCHAR(100) | Product alias in original casing |
| `NameLower` | NVARCHAR(100) | Product alias in lowercase (unique index) |
| `CreatedAt` | DATETIME2(3) | Registration timestamp |

The `NameLower` column carries a unique index (`uix_Product_NameLower`) to prevent duplicate product registrations regardless of casing. The `Id` is stored in `MigrationState.ProductId` after registration.

### Environment

Registered deployment environments for migration tracking. Populated automatically via `Repository_Environment_CheckInsert` at the start of each operation.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `Name` | NVARCHAR(100) | Environment name in original casing (e.g., `Docker`) |
| `NameLower` | NVARCHAR(100) | Environment name in lowercase (unique index) |
| `CreatedAt` | DATETIME2(3) | Registration timestamp |

The `NameLower` column carries a unique index (`uix_Environment_NameLower`) to prevent duplicate environment registrations regardless of casing. The `Id` is stored in `MigrationState.EnvironmentId` after registration.

### MigrationRun

Migration execution sessions.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `MigratorMetaId` | INT | FK to MigratorMeta |
| `ProductId` | INT | FK to Product |
| `EnvironmentId` | INT | FK to Environment |
| `MigrationRunModeId` | TINYINT | FK to MigrationRunMode |
| `MigrationRunResultId` | TINYINT | FK to MigrationRunResult |
| `FromReleaseVersion` | NVARCHAR(100) | Starting release version |
| `ToReleaseVersion` | NVARCHAR(100) | Target release version |
| `StartedAt` | DATETIME2(3) | Run start timestamp |
| `FinishedAt` | DATETIME2(3) | Run completion timestamp |
| `DurationInMs` | BIGINT | Total duration in milliseconds |
| `MigrationOperationId` | TINYINT NULL | FK to MigrationOperation: what the run does (MigrateUp, MigrateDown, Baseline); schema version 2 |
| `MigrationRunSourceId` | TINYINT NULL | FK to MigrationRunSource: where the run was started (Cli, Ci, Studio); NULL when the host did not say |
| `InitiatedBy` | NVARCHAR(200) NULL | Display name of the person or service account that started the run (the OS user for the CLI) |
| `InitiatedBySubject` | NVARCHAR(200) NULL | Stable identifier of the initiator, for example the subject claim of an identity provider (Studio) |
| `Host` | NVARCHAR(200) NULL | The machine the run executed on (the machine name unless the host names another) |
| `ErrorCode` | NVARCHAR(100) NULL | Engine error code of the failure that decided a result other than Ok, also when a cancel stopped the error recovery of a failed migrate-up; `1004` for a cancelled run, `1005` when the rollback of a cancelled migrate-up did not complete (`TemplateResultCode`) |
| `ErrorMessage` | NVARCHAR(MAX) NULL | Message of that failure, cut to 4000 characters; for a cancelled run `Cancelled on request (<action>): <outcome>` with the reason the host gave |
| `LastHeartbeatAt` | DATETIME2(3) NULL | When the run last reported that it was alive: the start at insert, refreshed by `Repository_MigrationRun_Heartbeat` every `Repository.HeartbeatIntervalInSeconds`; an open run whose heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` is orphaned |

The run row is the only place these details live: the records of a run say nothing about who started it, and the `info` run history reads the operation from the row instead of deriving it from the records.

### MigrationRunMeta

Extended metadata for migration runs.

| Column | Type | Description |
|--------|------|-------------|
| `MigrationRunId` | INT | PK/FK to MigrationRun |
| `MigrationRunSettingsJson` | NVARCHAR(MAX) | Serialized RayMigrator settings |
| `Description` | NVARCHAR(MAX) | Run description |

### MigrationRecord

Individual migration file records.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `ProductId` | INT | FK to Product |
| `EnvironmentId` | INT | FK to Environment |
| `MigrationRunId` | INT | FK to MigrationRun |
| `MigrationRunModeId` | TINYINT | FK to MigrationRunMode |
| `MigrationOperationId` | TINYINT | FK to MigrationOperation |
| `MigrationStatusId` | TINYINT | FK to MigrationStatus |
| `ReleaseVersion` | NVARCHAR(100) | Release version (path component) |
| `TargetGroupAlias` | NVARCHAR(100) | Target group alias |
| `TargetAlias` | NVARCHAR(100) | Target database alias |
| `Filename` | NVARCHAR(200) | Migration filename |
| `FileOrderId` | INT | Execution order |
| `FileUpHash` | VARCHAR(100) | SHA256 of up file |
| `FileUpConfigHash` | VARCHAR(100) | SHA256 of config section |
| `FileUpBlocksHash` | VARCHAR(100) | SHA256 of SQL blocks |
| `FileUpBlocksMigrated` | INT | Leading blocks committed on the target (see below) |
| `FileUpBlocksTotal` | INT | Total blocks in file |
| `FileUpConfigJson` | NVARCHAR(MAX) | TOML config as JSON |
| `MigrateDownFileExists` | BIT | Has rollback file |
| `FileDownHash` | VARCHAR(100) | SHA256 of down file |
| `FileDownConfigHash` | VARCHAR(100) | SHA256 of down config |
| `FileDownBlocksHash` | VARCHAR(100) | SHA256 of down SQL |
| `FileDownBlocksMigrated` | INT | Leading rollback blocks that ran (see below) |
| `FileDownBlocksTotal` | INT | Total down blocks |
| `FileDownConfigJson` | NVARCHAR(MAX) | Down TOML as JSON |
| `StartedAt` | DATETIME2(3) | Execution start |
| `FinishedAt` | DATETIME2(3) | Execution end |
| `DurationInMs` | BIGINT | Duration in milliseconds |

The two block counters count the leading blocks that committed on the target at every moment of a run, so that a run that ends abruptly leaves a counter the next start can trust. A block that commits after a failed one (`MigrationErrorAction=Ignore`, `RollbackErrorAction=Ignore`) does not count: the record of such a file keeps the blocks before the first failed one also when the file ends or a cancel stops it, so the next migrate-up resumes at that block, and a `FileUpBlocksMigrated` equal to `FileUpBlocksTotal` always means that every block committed; a rollback that ran to its end with an ignored failure stores its block count in `FileDownBlocksMigrated` with `Failed`; a file that a CLI tool runs counts 0 until the tool has finished; a resumed file and a continued rollback carry the blocks of their earlier attempt from their start (the record insert of a new attempt resets `FileUpBlocksMigrated` to 0 and the `FileDown*` columns to NULL).

`Repository_MigrationRecord_Select` and `Repository_MigrationRecordHistory_Select` return `StartedAt` and `FinishedAt` since 0.20.0 (`MigrationRecord.StartedAt`/`FinishedAt`, UTC); `TargetGroupStatus.LastMigrationDate` of the status is the latest `FinishedAt` of the Migrated records of a target group. A select without the two columns (an external DAL with the older templates) maps its records with null dates.

### MigrationRecordHistory

Audit table for MigrationRecord records. A history snapshot is written inline whenever a migration record reaches a terminal state — specifically when `MigrationStatusId IN (30, 50, 100)` (Failed, NotMigrated, or Migrated). This INSERT INTO MigrationRecordHistory is embedded directly in the `Repository_MigrationRecord_Update.sql` and `Repository_MigrationRecord_UpdateRollback.sql` templates, so historization happens at the moment each record transitions to its final status rather than in a separate bulk-archive step.

Same columns as `MigrationRecord`, plus:
| Column | Type | Description |
|--------|------|-------------|
| `MigrationRecordId` | INT | `Id` of the source record in `MigrationRecord` (no foreign key, see [Foreign Keys](#foreign-keys)) |
| `HistorizedAt` | DATETIME2(3) | UTC timestamp when the history record was written |

The rows carry the run that caused each transition, so the `info` run history (`MigrationService.GetHistoryAsync`) counts what a run did from them; no decision of the engine reads the table (ADR-027). A host may delete old rows through `IRepositoryMaintenanceService` ([Retention](#retention)); a run whose rows are gone reports no migrations in that history.

## Lookup Tables

All five lookup tables (`MigrationOperation`, `MigrationRunResult`, `MigrationRunMode`, `MigrationStatus`, `MigrationRunSource`) share the same column structure:

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | TINYINT | NOT NULL | Primary key |
| `Name` | NVARCHAR(100) | NOT NULL | Enum name (e.g., `MigrateUp`, `Migrated`) |
| `Description` | NVARCHAR(1000) | NULL | Human-readable description |

`Name` is `NOT NULL` across all 5 database engines. `Description` is intentionally nullable.

### MigrationRunMode

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; RunMode has not been set |
| 10 | Validate | Validates configuration and all migration files. Does NOT connect to target databases or repository database. |
| 20 | Simulate | Validates, checks DB connectivity, reads repository state. Does NOT execute SQL against target databases or write to the repository. |
| 100 | Migrate | Validates configuration and all migration files. Performs actual migrations against target databases. |

### MigrationOperation

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; operation has not been set |
| 5 | Rollback | Performing Rollback of current MigrationRun |
| 50 | MigrateDown | Performing Down-Migration |
| 100 | MigrateUp | Performing Up-Migration |
| 110 | Baseline | Marking migration files as migrated without executing them (baseline command) |

### MigrationRunResult

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; result has not been set |
| 10 | Running | Migration process is currently running |
| 50 | PartialSuccess | Migration(s) finished but at least one file was skipped or left Failed |
| 70 | Cancelled | Migration(s) stopped on request before completion (schema version 3) |
| 80 | Recovered | Migration(s) failed and the configured error recovery rolled back cleanly |
| 90 | Error | Migration(s) stopped due to error(s) |
| 100 | Ok | Migration(s) successfully executed |

A run the host cancelled is `Cancelled` with the engine code `1004`, or `Error` with `1005` when the rollback its `MigrationCancelledAction` started did not complete cleanly; a migrate-up that failed at a file before the cancel stopped its error recovery stays `Error` with the code of the failure (see [Error handling: Cancellation](../02-core-concepts/error-handling.md#cancellation), ADR-028).

### MigrationStatus

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; status has not been set |
| 10 | Pending | Record created, execution pending |
| 20 | Executing | SQL blocks are being executed |
| 30 | Failed | Execution failed, DB state unclear |
| 50 | NotMigrated | Not deployed / rolled back |
| 100 | Migrated | Successfully deployed |

### MigrationRunSource

Where a run was started from (`MigrationRun.MigrationRunSourceId`, schema version 2). The CLI reports `Cli`, or `Ci` when one of the pipeline variables `CI`, `GITHUB_ACTIONS`, `GITLAB_CI`, `TF_BUILD`, `JENKINS_URL` or `TEAMCITY_VERSION` is set to a value other than `false` or `0`; RayMigrator Studio reports `Studio`.

| Id | Name | Description |
|----|------|-------------|
| 10 | Cli | Started by the RayMigrator command line on a workstation or server |
| 20 | Ci | Started by the RayMigrator command line in a CI/CD pipeline |
| 30 | Studio | Started by RayMigrator Studio |

## Cross-Engine Type Mapping

The documentation above uses SQL Server types as the canonical reference. The actual column types vary by database engine:

| Logical Type | SQL Server | PostgreSQL | MariaDB / MySQL | SQLite |
|-------------|------------|------------|-----------------|--------|
| Auto-increment PK | `INT IDENTITY(1,1)` | `INT GENERATED ALWAYS AS IDENTITY` | `INT AUTO_INCREMENT` | `INTEGER PRIMARY KEY` |
| Lookup PK/FK | `TINYINT` | `SMALLINT` | `TINYINT UNSIGNED` | `INTEGER` |
| Boolean | `BIT` | `BOOLEAN` | `TINYINT(1)` | `INTEGER` |
| Timestamp | `DATETIME2(3)` | `TIMESTAMPTZ` | `TIMESTAMP` (session `time_zone='+00:00'`) | `TEXT` |
| Unicode string | `NVARCHAR(n)` | `TEXT` | `VARCHAR(n)` | `TEXT` |
| Large text | `NVARCHAR(MAX)` / `VARCHAR(MAX)` | `TEXT` | `TEXT` | `TEXT` |
| Non-unicode string | `VARCHAR(n)` | `TEXT` | `VARCHAR(n)` | `TEXT` |
| Large integer | `BIGINT` | `BIGINT` | `BIGINT` | `INTEGER` |

**Notable differences:**

- **PostgreSQL, MariaDB, and MySQL** use unquoted snake_case identifiers (e.g., `migration_run_result_id`). PostgreSQL follows the community naming convention; MariaDB and MySQL adopted the same convention in DAL-018. See the [snake_case table names (PostgreSQL, MariaDB, MySQL)](#snake_case-table-names-postgresql-mariadb-mysql) section below for the full table mapping.
- **PostgreSQL** uses plain `NOW()` for audit writes (columns are `TIMESTAMPTZ`, so `NOW()` correctly stores UTC); SQL Server uses `SYSUTCDATETIME()`; MariaDB/MySQL use `CURRENT_TIMESTAMP` on `TIMESTAMP` columns (the DAL pins session `time_zone='+00:00'` on every connection open, so `CURRENT_TIMESTAMP` writes UTC); SQLite uses `datetime('now')`.
- **MariaDB/MySQL** `CREATE TABLE` statements explicitly pin `DEFAULT CHARSET=utf8mb4` with engine-specific collation: MySQL uses `utf8mb4_0900_ai_ci` (available from MySQL 8.0+), MariaDB uses `utf8mb4_unicode_ci` (stable from MariaDB 10.5+ LTS). The two collation names are not interchangeable — MariaDB does not ship `utf8mb4_0900_*` collations, and specifying the MySQL name on MariaDB raises `ERROR 1273`.
- **MariaDB/MySQL** DDL causes implicit commits, so tables are created with `IF NOT EXISTS` individually (not inside a transaction).
- **SQLite** has no schema support; the `SchemaName` config placeholder is not used. Repository tables use `STRICT` (DAL-022, SQLite 3.37+), so column types (`TEXT`, `INTEGER`) are enforced at INSERT time rather than coerced via type affinity.

## Indexes

The following indexes are created on all 5 database engines (shown using SQL Server / SQLite canonical names; PostgreSQL, MariaDB, and MySQL use the snake_case equivalents `uix_product_name_lower`, `uix_environment_name_lower`, `ix_migration_record_history_migration_record_id`, `ix_migration_run_product_environment_started_at`, `ix_migration_record_history_migration_run_id`, `ix_migration_record_history_historized_at`, `ix_migration_record_product_environment`):

```sql
-- Product: Unique constraint on lowercase name (case-insensitive deduplication)
CREATE UNIQUE INDEX uix_Product_NameLower ON Product (NameLower);

-- Environment: Unique constraint on lowercase name (case-insensitive deduplication)
CREATE UNIQUE INDEX uix_Environment_NameLower ON Environment (NameLower);

-- MigrationRecordHistory: Fast lookup by MigrationRecordId
CREATE INDEX ix_MigrationRecordHistory ON MigrationRecordHistory (MigrationRecordId);

-- Schema version 3 (Repository_Upgrade_3): the filters of the readers and of the history cleanup
CREATE INDEX ix_MigrationRun_Product_Environment_StartedAt ON MigrationRun (ProductId, EnvironmentId, StartedAt);
CREATE INDEX ix_MigrationRecordHistory_MigrationRunId      ON MigrationRecordHistory (MigrationRunId);
CREATE INDEX ix_MigrationRecordHistory_HistorizedAt        ON MigrationRecordHistory (HistorizedAt);
CREATE INDEX ix_MigrationRecord_Product_Environment        ON MigrationRecord (ProductId, EnvironmentId);
```

Step 3 creates each index only when it is missing: SQL Server looks it up in `sys.indexes`, PostgreSQL, MariaDB and SQLite use `CREATE INDEX IF NOT EXISTS`, MySQL, which has no such clause, looks it up in `information_schema` and runs the `CREATE INDEX` through a prepared statement. The log table has two indexes of its own, `ix_{TableBaseName}MigrationLog_MigrationRunId` and `ix_{TableBaseName}MigrationLog_CreatedAt`, which `DatabaseLogging_CheckCreate` creates outside the repository schema version (see [Logging Schema](logging-schema.md#indexes)).

PostgreSQL does not auto-create indexes for FK columns (unlike MySQL/MariaDB InnoDB, which does). The following additional indexes are therefore created on PostgreSQL only (shown with snake_case names used by the PostgreSQL templates):

```sql
-- migration_run FK columns
CREATE INDEX ix_migration_run_migrator_meta_id        ON migration_run (migrator_meta_id);
CREATE INDEX ix_migration_run_product_id              ON migration_run (product_id);
CREATE INDEX ix_migration_run_environment_id          ON migration_run (environment_id);
CREATE INDEX ix_migration_run_migration_run_mode_id   ON migration_run (migration_run_mode_id);
CREATE INDEX ix_migration_run_migration_run_result_id ON migration_run (migration_run_result_id);

-- migration_run FK columns of schema version 2 (Repository_Upgrade_2)
CREATE INDEX ix_migration_run_migration_operation_id  ON migration_run (migration_operation_id);
CREATE INDEX ix_migration_run_migration_run_source_id ON migration_run (migration_run_source_id);

-- migration_record FK columns
CREATE INDEX ix_migration_record_product_id              ON migration_record (product_id);
CREATE INDEX ix_migration_record_environment_id          ON migration_record (environment_id);
CREATE INDEX ix_migration_record_migration_run_id        ON migration_record (migration_run_id);
CREATE INDEX ix_migration_record_migration_run_mode_id   ON migration_record (migration_run_mode_id);
CREATE INDEX ix_migration_record_migration_operation_id  ON migration_record (migration_operation_id);
CREATE INDEX ix_migration_record_migration_status_id     ON migration_record (migration_status_id);
```

Actual index names in the template use the `{CFG:TableBaseName}` prefix, e.g. `ix_{CFG:TableBaseName}migration_run_product_id`, and so do the primary key and foreign key names since 0.18.0 (`pk_{CFG:TableBaseName}migration_run`, `fk_{CFG:TableBaseName}migration_run_product`): constraint names are scoped per schema on SQL Server, per schema for the index behind a primary key on PostgreSQL and per database on MySQL and MariaDB, and the prefix keeps two repositories in one place apart. A repository created by 0.17.0 with a prefix still owns the unprefixed key names, so a new repository without a prefix in the same schema collides with it until the old one is recreated. The prefix has at most 16 characters (RULE_4_4) so that the longest name fits the identifier limits. A repository created by 0.17.0 keeps its unprefixed key names. SQL Server does not auto-index FK columns either; that is a candidate for a future work item (see DAL-007 and related tickets).

## Foreign Keys

All 17 foreign keys (15 from schema step 1, 2 from step 2) use `NO ACTION` referential behavior (no cascade on delete or update). The logical FK declarations below use SQL Server / canonical names without the prefix; the templates write `fk_{CFG:TableBaseName}...`, and the guards of `Repository_Upgrade_2` on SQL Server and PostgreSQL look for the prefixed names. On PostgreSQL, MariaDB, and MySQL, each FK uses snake_case constraint names (e.g., `fk_migration_run_product` instead of `fk_MigrationRun_Product`); on PostgreSQL each FK is additionally declared with an explicit `ON DELETE NO ACTION ON UPDATE NO ACTION` clause; on MariaDB/MySQL the default `NO ACTION` is implicit. On SQLite, step 2 declares its two references on the added columns without a constraint name.

```sql
-- MigrationRecord table
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_Product
    FOREIGN KEY (ProductId) REFERENCES Product(Id);
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_Environment
    FOREIGN KEY (EnvironmentId) REFERENCES Environment(Id);
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_MigrationRun
    FOREIGN KEY (MigrationRunId) REFERENCES MigrationRun(Id);
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_MigrationRunMode
    FOREIGN KEY (MigrationRunModeId) REFERENCES MigrationRunMode(Id);
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_MigrationOperation
    FOREIGN KEY (MigrationOperationId) REFERENCES MigrationOperation(Id);
ALTER TABLE MigrationRecord ADD CONSTRAINT fk_MigrationRecord_MigrationStatus
    FOREIGN KEY (MigrationStatusId) REFERENCES MigrationStatus(Id);

-- MigrationRecordHistory table
-- Note: No FK from MigrationRecordHistory.MigrationRecordId to MigrationRecord.Id.
-- MigrationRecordHistory stores audit snapshots of MigrationRecord records. Because the
-- same MigrationRecord.Id may be referenced many times across runs, a FK is not
-- defined to avoid false constraint violations.
-- Note: No FKs to lookup tables (MigrationRunMode, MigrationOperation, MigrationStatus).
ALTER TABLE MigrationRecordHistory ADD CONSTRAINT fk_MigrationRecordHistory_MigrationRun
    FOREIGN KEY (MigrationRunId) REFERENCES MigrationRun(Id);
ALTER TABLE MigrationRecordHistory ADD CONSTRAINT fk_MigrationRecordHistory_Product
    FOREIGN KEY (ProductId) REFERENCES Product(Id);
ALTER TABLE MigrationRecordHistory ADD CONSTRAINT fk_MigrationRecordHistory_Environment
    FOREIGN KEY (EnvironmentId) REFERENCES Environment(Id);

-- MigrationRun table
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_Product
    FOREIGN KEY (ProductId) REFERENCES Product(Id);
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_Environment
    FOREIGN KEY (EnvironmentId) REFERENCES Environment(Id);
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_MigrationRunResult
    FOREIGN KEY (MigrationRunResultId) REFERENCES MigrationRunResult(Id);
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_MigrationRunMode
    FOREIGN KEY (MigrationRunModeId) REFERENCES MigrationRunMode(Id);
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_MigratorMeta
    FOREIGN KEY (MigratorMetaId) REFERENCES MigratorMeta(Id);

-- MigrationRunMeta table
ALTER TABLE MigrationRunMeta ADD CONSTRAINT fk_MigrationRunMeta_MigrationRun
    FOREIGN KEY (MigrationRunId) REFERENCES MigrationRun(Id);

-- MigrationRun table, schema version 2 (Repository_Upgrade_2)
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_MigrationOperation
    FOREIGN KEY (MigrationOperationId) REFERENCES MigrationOperation(Id);
ALTER TABLE MigrationRun ADD CONSTRAINT fk_MigrationRun_MigrationRunSource
    FOREIGN KEY (MigrationRunSourceId) REFERENCES MigrationRunSource(Id);
```

> **Note on MigrationLog:** The `MigrationLog` table also has an `EnvironmentId` column but carries **no FK** to the `Environment` table. This matches the existing precedent for `ProductId` in the logging schema — the logging database may be separate from the repository, so FK enforcement would require cross-database references. Pass `null` when `EnvironmentId` is zero or unknown. For the same reason the cleanup of the log reaches only a log table in the repository database under the schema and prefix of the repository ([Retention](#retention)).

## snake_case table names (PostgreSQL, MariaDB, MySQL)

PostgreSQL (DAL-017), MariaDB (DAL-018), and MySQL (DAL-018) use unquoted snake_case identifiers throughout (tables, columns, constraints, and indexes). The table names below are the on-disk names for all three engines; column names follow the same mechanical PascalCase-to-snake_case rule (e.g., `MigrationRunResultId` → `migration_run_result_id`). SQL Server and SQLite retain PascalCase.

| Canonical name (SQL Server / SQLite) | PostgreSQL / MariaDB / MySQL name |
|--------------------------------------|-----------------------------------|
| `MigratorMeta` | `migrator_meta` |
| `Product` | `product` |
| `Environment` | `environment` |
| `MigrationRun` | `migration_run` |
| `MigrationRunMeta` | `migration_run_meta` |
| `MigrationRecord` | `migration_record` |
| `MigrationRecordHistory` | `migration_record_history` |
| `MigrationRunMode` | `migration_run_mode` |
| `MigrationOperation` | `migration_operation` |
| `MigrationRunResult` | `migration_run_result` |
| `MigrationStatus` | `migration_status` |

The two logging tables follow the same convention: `MigrationEvent` → `migration_event`, `MigrationLog` → `migration_log`.

**Product-name exception:** The brand token `RayMigrator` is treated as a single token rather than two words. `RayMigratorVersion` → `raymigrator_version` (not `ray_migrator_version`), and the token stays merged in the middle of an identifier as well. This exception applies equally to PostgreSQL, MariaDB, and MySQL. See [Naming Conventions per Engine](sql-dialects.md#naming-conventions-per-engine) in `sql-dialects.md` for the full rule.

**`TableBaseName` constraint:** For PostgreSQL, MariaDB, and MySQL, the `Repository.TableBaseName` and `DatabaseLogging.TableBaseName` configuration values must be all-lowercase. The `RayMigratorOptionsValidator` rejects any uppercase character with a descriptive error. Rationale: PostgreSQL folds unquoted identifiers to lowercase; the MariaDB and MySQL snake_case repository schema is stored as lowercase; any uppercase prefix character would break the `information_schema.tables` existence checks in `Repository_CheckCreate` and `DatabaseLogging_CheckCreate`.

## Read model package

`Raycoon.RayMigrator.Repository.ReadModel` (ADR-025) is the contract for everything that reads the repository without the engine's templates: RayMigrator Studio, reports, scripts. It depends on `Raycoon.RayMigrator.Core` for the enums and the schema version and on nothing else; it contains no EF Core and no database driver.

| Member | Purpose |
|--------|---------|
| `RepositoryTable` | The 15 tables of this page, the two log tables included (database logging goes into the repository by default). |
| `RepositoryNaming(databaseType, schemaName, tableBaseName)` | `TableName`, `ColumnName`, `QualifiedTableName`, `QualifiedColumnName`, `Quote` and `SelectStatement` as the engine templates write them: PascalCase on SQL Server and SQLite, snake_case on PostgreSQL, MySQL and MariaDB; schema only on SQL Server and PostgreSQL; brackets, backticks, double quotes, or nothing on PostgreSQL. `ToSnakeCase` is the rule of this page, product-name exception included. |
| `*Row` records | One positional record per table (`MigrationRunRow`, `MigrationRecordRow`, `LookupRow` for the five lookups, `MigrationLogRow`, ...). The constructor parameters are the columns in PascalCase in table order, the parameter types the .NET types: lookup ids as their enums, timestamps as UTC `DateTime`. |
| `RepositoryRows.ColumnsOf(table)` | The column list derived from the record, so statements and records cannot drift. |
| `RepositoryRowMapper.Map` | Builds a record from an ADO.NET row (column values by name): integer widths, `tinyint`/`sbyte` lookup ids, `bit`/`0`/`1` booleans, SQLite text timestamps and timestamps without a kind (all UTC), `DBNull`. |
| `RepositoryReadModel.SchemaVersion` | `RepositorySchema.CurrentVersion` of the engine release the package ships with; a reader compares it with `MAX(Version)` of `RepositorySchemaVersion` before trusting the names. |

`ReadModelContractTests` on all five engines read every table after a real migrate-up with the package's statements through the engine DAL and map every row; `RepositoryQueryHelper` of the Testing package takes its snake_case rule from the package.

> **MySQL and MariaDB session time zone:** the timestamp columns are `TIMESTAMP`, which the server converts to the session time zone on read. The engine DAL runs every session in UTC (`SET time_zone = '+00:00'` after each open); a reader with its own connection (RayMigrator Studio, a script) must do the same, or `RepositoryRowMapper` labels local wall time as UTC and every heartbeat age is off by the server's offset.

## Schema versioning

Since 0.17.0 the schema is versioned and upgraded in numbered steps (ADR-024):

1. `Repository_CheckCreate` creates the schema (SQL Server, PostgreSQL) and the `RepositorySchemaVersion` table if they are missing and returns `'schemaVersion,unversionedTables,message'`: the highest applied version (`0` when none) and `1` when the `MigratorMeta` table exists.
2. `RepositorySchemaExecutor.EnsureAsync` compares the version with the target version and executes every missing `Repository_Upgrade_{n}` in order. For a run, `TemplateExecutor.RepositoryCheckCreateAsync` passes the target of the context (`MigrationContext.RepositorySchemaTargetVersion`: `RepositorySchema.CurrentVersion`, unless an engine test pins an older version). Each step creates its version on top of the one before, returns its own number and writes its row of `RepositorySchemaVersion`. Step 1 creates the 11 tables of this page with their master data, so a fresh repository runs every step from 1 and the steps are the single source of the schema. The current version is 3.
3. `Repository_MigratorMeta_CheckInsert` registers the engine version in `MigratorMeta` and returns the `MigratorMetaId` every run row references.

A host that manages repositories runs the same steps without a product, an environment or a run: `IRepositorySchemaService.EnsureAsync(RepositoryOptions, CancellationToken)` (registered by `AddRayMigratorServices`) validates the repository options, executes the steps up to `RepositorySchema.CurrentVersion` through `RepositorySchemaExecutor` and returns the versions before and after and the `MigratorMetaId` (see [MigrationContext](../02-core-concepts/migration-context.md#creating-or-upgrading-a-repository-without-a-run)); the token reaches the DAL call of every step. `RepositorySchemaExecutor.CheckVersionAsync` runs only the first part: it reads the version through `Repository_CheckCreate`, applies no step and refuses the same two cases as `EnsureAsync`. `IRepositoryMaintenanceService` checks a repository with it before it cleans up ([Retention](#retention)).

Every step is idempotent per engine: SQL Server runs it in one transaction with `sp_getapplock`, PostgreSQL in one `DO` block with `pg_advisory_xact_lock`, SQLite in one `BEGIN IMMEDIATE` transaction (`CREATE TABLE IF NOT EXISTS`, `INSERT OR IGNORE`), MySQL and MariaDB statement by statement (`CREATE TABLE IF NOT EXISTS`, `INSERT IGNORE`) under `GET_LOCK()`, because their DDL commits implicitly. A step that fails, times out or is cancelled between its `GET_LOCK()` and its `RELEASE_LOCK()` leaves no lock behind: the DAL releases the locks of the session (`DO RELEASE_ALL_LOCKS()`) before the connection goes back to the pool, where the lock would hold back every other host until the pool reused or pruned the connection (up to 180 seconds). `Repository_CheckCreate` takes the same lock (or waits in the SQLite busy timeout), so a check that runs while another process applies a step never sees the tables without their version row. A parallel start on an empty repository therefore creates it once. Both processes may still read version 0 before either applies a step; a step that fails on what the other process already did (SQLite has no `ADD COLUMN IF NOT EXISTS`) is accepted when a re-read of the version shows it applied, and the DAL rolls back the failed script's own `BEGIN IMMEDIATE` so that a pooled connection does not keep the write lock.

Two cases are refused with `RepositorySchemaException` before a schema step runs or a row is written (`Repository_CheckCreate` may have created the empty version table): a repository with `MigratorMeta` but no schema version was created by a RayMigrator before 0.17.0 and must be recreated once; a repository whose version is higher than the engine supports needs a newer RayMigrator. A later schema version ships as a new `Repository_Upgrade_{n}` template in every DAL plus a bump of `RepositorySchema.CurrentVersion`. An existing step changes only as ADR-024 allows: before 1.0 a correction that does not alter the data of a repository that already has the step (0.18.0 put the table prefix into the key names), after 1.0 never.

## Retention

`MigrationRecordHistory` and `MigrationLog` grow with every run, and no decision of the engine reads them (ADR-027, see [Architecture decisions](../arc42/09-Architecture-Decisions.md#adr-027-the-engine-deletes-log-and-history-rows-on-a-hosts-request-and-never-reads-them-for-a-decision)): every command decides on `MigrationRecord`, and only the run history of `info` reads `MigrationRecordHistory`. A host that applies a retention policy (RayMigrator Studio) deletes their old rows through `IRepositoryMaintenanceService` (`Raycoon.RayMigrator.Services.Abstractions`, implemented by `RepositoryMaintenanceService`, registered by `AddRayMigratorServices` as a singleton next to `IRepositorySchemaService`; it needs the host's `TemplateCache`), from the repository options alone, without a product, an environment or a migration context. The engine itself never deletes these rows, and the CLI has no command for it.

```csharp
Task<RetentionResult> CleanUpAsync(RepositoryOptions repository, RetentionRequest request, CancellationToken cancellationToken = default);

public sealed record RetentionRequest(
    DateTime? HistoryCutoffUtc,                           // history rows historized before it; null leaves the history alone
    DateTime? LogCutoffUtc,                               // log rows created before it; null leaves the log alone
    int BatchSize = RetentionRequest.DefaultBatchSize,    // 2000
    TimeSpan? TimeBudget = null,                          // null: RetentionRequest.DefaultTimeBudget (60 seconds)
    bool DryRun = false);

public sealed record RetentionResult(long HistoryRows, long LogRows, int Batches, bool Completed, bool LogSkipped, string? Message);
```

| Topic | Behaviour |
|-------|-----------|
| Request | At least one cutoff is set; both are `DateTimeKind.Utc` and count to the whole second, so a row is never deleted unless it is older. `BatchSize` is at least 1; `TimeBudget` is not negative, `Timeout.InfiniteTimeSpan` means no limit. A request that breaks these rules is an `ArgumentException` before the database is touched. |
| Options | Validated like those of `IRepositorySchemaService` (placeholders, data annotations with their defaults, database type, RULE_4_2 to RULE_4_5, schema name); both services share that code. The templates and the check of the schema version run with the command timeout of the repository (`DbCommandTimeoutInSeconds`) but without its retries (`DbCommandMaxRetries`): a statement that timed out, and on SQLite one that reports BUSY after its busy timeout (Microsoft.Data.Sqlite sets it to the command timeout), would wait the whole command timeout again on every retry, and the default of 100 retries turned a budget of a minute into hours. The next batch and the next call are the retry. |
| Schema version | Read through `RepositorySchemaExecutor.CheckVersionAsync`, that is through `Repository_CheckCreate` as every command reads it, which creates the empty `RepositorySchemaVersion` table (on SQL Server and PostgreSQL also the schema) when it is missing. A database without a repository, a repository without a schema version and one newer than the engine are refused with `RepositorySchemaException`. An older repository is cleaned as it is and not upgraded, because the templates read only columns of schema version 1 and an upgrade would lock out the engines that still use it; without the indexes of version 3 a warning says that every history batch reads the whole table. |
| Order and batches | The history first, then the log. A batch is one statement that deletes at most `BatchSize` rows, the oldest first, and commits on its own; the cleanup of a table ends when a batch deletes fewer rows than `BatchSize`. Before it deletes from the log, the service asks `DatabaseLogging_Count` with `@ExistsOnly` = 1 whether the log table is there (`RepositoryMaintenanceExecutor.LogTableExistsAsync`), which counts nothing, so a large log is not read whole before its first batch. The rows deleted so far stay deleted when the cleanup stops early, and a later call continues. `Batches` counts the batches that deleted rows. |
| Time budget | Checked before every batch once a batch has deleted rows, so a call always deletes one batch when there is one, also with a budget of zero. When it runs out, the result is `Completed = false` with a message to run the cleanup again. The budget does not stop a batch in progress: a batch that starts in time may take up to the command timeout, once, since it is not retried. |
| Failure | An error after a batch deleted rows ends the call with `Completed = false`, the rows deleted so far and the error in `Message`, with the messages of its inner exceptions (the provider's report among them), and is logged as a warning with the exception: those rows are gone, and the host must learn of them. An error before a batch deleted rows throws, as nothing changed. |
| Cancellation | The token is checked before every batch and reaches the batch in progress: SQL Server, PostgreSQL, MySQL and MariaDB abort the statement, which rolls back whole; SQLite completes it. A cancel while the cleanup deletes returns the rows deleted so far with `Completed = false`; on SQL Server, MySQL and MariaDB a cancel between the `DELETE` of a batch and its count leaves that batch uncounted, so the counts are then a lower bound. A cancel during the checks of the options and the schema version, and in a dry run, throws `OperationCanceledException`; after those checks, the check of the log table between history and log included, the call returns the rows deleted so far with `Completed = false`, possibly none. |
| Dry run | Counts in full the rows each named table would lose (`DatabaseLogging_Count` with `@ExistsOnly` = 0) and deletes nothing; `Batches` is 0 and the result is always complete. |
| Open runs | Every batch spares the rows of a run that is still open (`MigrationRun.FinishedAt IS NULL`), so a running or orphaned run keeps its history and its log until a later start or `fix` closes it. A log row without a run (`MigrationRunId` null: update-hash, fix and the early pipeline events log without one) falls by its age. |
| Log location | The log is cleaned only where it shares the repository database and the schema and prefix of the repository, its default home. A log of its own elsewhere is left alone and reported as `LogSkipped = true`. |
| Kept tables | `MigrationRecord`, `MigrationRun` and `MigrationRunMeta` are never deleted, so status, pending migrations and every decision of the engine stay as they were. The `info` run history reports no migrations for a run whose history rows are gone. |
| Parallel cleanups | Serialising the cleanups of a repository is the host's business. Two at once delete only rows that match; a batch whose rows the other one deleted first deletes fewer, which may end that cleanup as completed while the other one deletes the rest. |
| Shared log table | The guard reads the open runs of this repository only. When another repository writes into the same log table (same database, schema and prefix), its rows fall by their age even while its run is open. |

### Retention templates

`RepositoryMaintenanceExecutor` (`Raycoon.RayMigrator.Infrastructure`) runs four templates per DAL without a migration context, with the command timeout of the repository and without its retries. The repository settings open no transaction, so a batch is one statement in autocommit, applied whole or not at all. `@CutoffUtc` is UTC text `yyyy-MM-dd HH:mm:ss`, `@BatchSize` the most rows of one batch, and `@ExistsOnly` (`1` or `0`) tells `DatabaseLogging_Count` whether to report the existence of the log table alone. The log templates run with the schema and prefix of the `Repository` section, not of `DatabaseLogging`, and join its `MigrationRun`.

| Template | Result |
|----------|--------|
| `Repository_MigrationRecordHistory_Delete` | Deletes one batch of the history rows historized before the cutoff; `'n,message'` with the rows deleted |
| `Repository_MigrationRecordHistory_Count` | `'n,message'` with the rows the delete would take |
| `DatabaseLogging_Delete` | Deletes one batch of the log rows created before the cutoff; `'n,message'` |
| `DatabaseLogging_Count` | `'n,1,message'`, or `'0,0,message'` when the repository database has no log table under the schema and prefix of the repository; with `@ExistsOnly` = 1 it counts nothing and returns `'0,1,message'` when the table is there (`LogTableExistsAsync`, before the cleanup deletes from the log), with `0` it counts (`CountLogAsync`, the dry run) |

A count may exceed the `int` range of the result contract and is read as a `long`. One batch per engine:

| Engine | Batch |
|--------|-------|
| SQL Server | `DELETE` through a CTE with `TOP (@BatchSize)` in the order of the clustered key, with `OPTION (LOOP JOIN)`. Ordered by the time column, SQL Server read every matching row for each batch, so that a cleanup grew with the square of its rows; in key order with nested loops a batch stops after its own rows (1,000,000 log rows took 51 instead of 170 seconds). |
| PostgreSQL | A data-modifying CTE deletes the ids of a `LIMIT` subquery ordered by the time column and counts them in the same statement. |
| MySQL, MariaDB | `DELETE … ORDER BY … LIMIT`; the cutoff text is read in the session time zone, which the DAL sets to UTC. |
| SQLite | Deletes the rowids of a `LIMIT` subquery, because `DELETE … LIMIT` needs a build with `SQLITE_ENABLE_UPDATE_DELETE_LIMIT`. SQLite resolves the tables of a statement when it prepares it, so `DatabaseLogging_Count` creates an empty log table for the count when the table is missing, inside a transaction that it rolls back. |

The default batch size of 2,000 keeps SQL Server from escalating a batch to a table lock, which would hold back the log writes of running hosts for the length of the batch: SQL Server escalates a statement at 5,000 locks on one index, and a delete takes a key lock per row and a page lock per page it touches in every index. Measured with the indexes of schema version 3, history batches of 4,800 rows and log batches of 5,000 rows escalated, while 2,000 rows take at most 4,000 locks per index even with one row per page.

Every DAL ships the four templates; when a DAL folder under `DataAccessLayers/` lacks one, the `TemplateCache` constructor throws and no command starts, whatever database it uses (see [External DAL development](../09-extending/external-dal-development.md#required-templates)).

## Schema Configuration

Schema and table prefix are configurable:

```json
{
  "Repository": {
    "SchemaName": "migrations",
    "TableBaseName": "rm_"
  }
}
```

**Result**: `migrations.rm_MigrationRecord`, `migrations.rm_Product`, etc. on SQL Server, `migrations.rm_migration_record`, `migrations.rm_product`, etc. on PostgreSQL (the prefix must be lowercase on PostgreSQL, MySQL and MariaDB, RULE_4_3).

## Related Documentation

- [Logging Schema](logging-schema.md) - Logging tables
- [Template System](template-system.md) - How schema is created
- [Migration State Machine](../02-core-concepts/migration-state-machine.md)
- [Architecture decisions](../arc42/09-Architecture-Decisions.md) - ADR-024 (schema steps), ADR-025 (read model), ADR-027 (retention)
