# Repository Schema

The repository database stores migration tracking data. This schema is created automatically by RayMigrator.

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
    MigrationStatus ||--o{ MigrationRecord : "status"

    MigratorMeta {
        int Id PK
        nvarchar RepositoryVersion
        nvarchar RepositoryDatabaseType
        nvarchar CreatedByRayMigratorVersion
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
        int MigrationRecordId FK
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

The `Repository_CheckCreate` template creates **11 tables** (4 lookup + 7 data).

| Table | Purpose | Status |
|-------|---------|--------|
| `MigratorMeta` | Repository version tracking | Created |
| `Product` | Registered products | Created |
| `Environment` | Registered environments | Created |
| `MigrationRun` | Migration run sessions | Created |
| `MigrationRunMeta` | Run metadata (JSON settings) | Created |
| `MigrationRecord` | Individual migration records | Created |
| `MigrationRecordHistory` | Migration archive (records from previous runs) | Created |
| `MigrationRunMode` | Lookup: Validate/Simulate/Migrate | Created |
| `MigrationOperation` | Lookup: Rollback/MigrateDown/MigrateUp | Created |
| `MigrationRunResult` | Lookup: Running/Error/Ok (used by MigrationRun) | Created |
| `MigrationStatus` | Lookup: Pending/Executing/Failed/NotMigrated/Migrated | Created |

## Core Tables

### MigratorMeta

Tracks repository versions for upgrade compatibility.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT | Primary key (auto-increment) |
| `RepositoryVersion` | NVARCHAR(100) | Repository schema version |
| `RepositoryDatabaseType` | NVARCHAR(100) | Database type (SqlServer, etc.) |
| `CreatedByRayMigratorVersion` | NVARCHAR(100) | RayMigrator application version |
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
| `FileUpBlocksMigrated` | INT | Blocks executed |
| `FileUpBlocksTotal` | INT | Total blocks in file |
| `FileUpConfigJson` | NVARCHAR(MAX) | TOML config as JSON |
| `MigrateDownFileExists` | BIT | Has rollback file |
| `FileDownHash` | VARCHAR(100) | SHA256 of down file |
| `FileDownConfigHash` | VARCHAR(100) | SHA256 of down config |
| `FileDownBlocksHash` | VARCHAR(100) | SHA256 of down SQL |
| `FileDownBlocksMigrated` | INT | Down blocks executed |
| `FileDownBlocksTotal` | INT | Total down blocks |
| `FileDownConfigJson` | NVARCHAR(MAX) | Down TOML as JSON |
| `StartedAt` | DATETIME2(3) | Execution start |
| `FinishedAt` | DATETIME2(3) | Execution end |
| `DurationInMs` | BIGINT | Duration in milliseconds |

### MigrationRecordHistory

Audit table for MigrationRecord records. A history snapshot is written inline whenever a migration record reaches a terminal state — specifically when `MigrationStatusId IN (30, 50, 100)` (Failed, NotMigrated, or Migrated). This INSERT INTO MigrationRecordHistory is embedded directly in the `Repository_MigrationRecord_Update.sql` and `Repository_MigrationRecord_UpdateRollback.sql` templates, so historization happens at the moment each record transitions to its final status rather than in a separate bulk-archive step.

Same columns as `MigrationRecord`, plus:
| Column | Type | Description |
|--------|------|-------------|
| `MigrationRecordId` | INT | Foreign key to MigrationRecord.Id of the source record |
| `HistorizedAt` | DATETIME2(3) | UTC timestamp when the history record was written |

## Lookup Tables

All four lookup tables (`MigrationOperation`, `MigrationRunResult`, `MigrationRunMode`, `MigrationStatus`) share the same column structure:

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

### MigrationRunResult

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; result has not been set |
| 10 | Running | Migration process is currently running |
| 90 | Error | Migration(s) stopped due to error(s) |
| 100 | Ok | Migration(s) successfully executed |

### MigrationStatus

| Id | Name | Description |
|----|------|-------------|
| 0 | Undefined | Invalid value; status has not been set |
| 10 | Pending | Record created, execution pending |
| 20 | Executing | SQL blocks are being executed |
| 30 | Failed | Execution failed, DB state unclear |
| 50 | NotMigrated | Not deployed / rolled back |
| 100 | Migrated | Successfully deployed |

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

The following indexes are created on all 5 database engines (shown using SQL Server / SQLite canonical names; PostgreSQL, MariaDB, and MySQL use the snake_case equivalents `uix_product_name_lower`, `uix_environment_name_lower`, `ix_migration_record_history_migration_record_id`):

```sql
-- Product: Unique constraint on lowercase name (case-insensitive deduplication)
CREATE UNIQUE INDEX uix_Product_NameLower ON Product (NameLower);

-- Environment: Unique constraint on lowercase name (case-insensitive deduplication)
CREATE UNIQUE INDEX uix_Environment_NameLower ON Environment (NameLower);

-- MigrationRecordHistory: Fast lookup by MigrationRecordId
CREATE INDEX ix_MigrationRecordHistory ON MigrationRecordHistory (MigrationRecordId);
```

PostgreSQL does not auto-create indexes for FK columns (unlike MySQL/MariaDB InnoDB, which does). The following additional indexes are therefore created on PostgreSQL only (shown with snake_case names used by the PostgreSQL templates):

```sql
-- migration_run FK columns
CREATE INDEX ix_migration_run_migrator_meta_id        ON migration_run (migrator_meta_id);
CREATE INDEX ix_migration_run_product_id              ON migration_run (product_id);
CREATE INDEX ix_migration_run_environment_id          ON migration_run (environment_id);
CREATE INDEX ix_migration_run_migration_run_mode_id   ON migration_run (migration_run_mode_id);
CREATE INDEX ix_migration_run_migration_run_result_id ON migration_run (migration_run_result_id);

-- migration_record FK columns
CREATE INDEX ix_migration_record_product_id              ON migration_record (product_id);
CREATE INDEX ix_migration_record_environment_id          ON migration_record (environment_id);
CREATE INDEX ix_migration_record_migration_run_id        ON migration_record (migration_run_id);
CREATE INDEX ix_migration_record_migration_run_mode_id   ON migration_record (migration_run_mode_id);
CREATE INDEX ix_migration_record_migration_operation_id  ON migration_record (migration_operation_id);
CREATE INDEX ix_migration_record_migration_status_id     ON migration_record (migration_status_id);
```

Actual index names in the template use the `{CFG:TableBaseName}` prefix, e.g. `ix_{CFG:TableBaseName}migration_run_product_id`. SQL Server does not auto-index FK columns either; that is a candidate for a future work item (see DAL-007 and related tickets).

## Foreign Keys

All 15 foreign keys use `NO ACTION` referential behavior (no cascade on delete or update). The logical FK declarations below use SQL Server / canonical names. On PostgreSQL, MariaDB, and MySQL, each FK uses snake_case constraint names (e.g., `fk_migration_run_product` instead of `fk_MigrationRun_Product`); on PostgreSQL each FK is additionally declared with an explicit `ON DELETE NO ACTION ON UPDATE NO ACTION` clause; on MariaDB/MySQL the default `NO ACTION` is implicit.

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
```

> **Note on MigrationLog:** The `MigrationLog` table also has an `EnvironmentId` column but carries **no FK** to the `Environment` table. This matches the existing precedent for `ProductId` in the logging schema — the logging database may be separate from the repository, so FK enforcement would require cross-database references. Pass `null` when `EnvironmentId` is zero or unknown.

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

**Product-name exception:** The brand token `RayMigrator` is treated as a single token rather than two words. `CreatedByRayMigratorVersion` → `created_by_raymigrator_version` (not `created_by_ray_migrator_version`). This exception applies equally to PostgreSQL, MariaDB, and MySQL. See [Naming Conventions per Engine](sql-dialects.md#naming-conventions-per-engine) in `sql-dialects.md` for the full rule.

**`TableBaseName` constraint:** For PostgreSQL, MariaDB, and MySQL, the `Repository.TableBaseName` and `DatabaseLogging.TableBaseName` configuration values must be all-lowercase. The `RayMigratorOptionsValidator` rejects any uppercase character with a descriptive error. Rationale: PostgreSQL folds unquoted identifiers to lowercase; the MariaDB and MySQL snake_case repository schema is stored as lowercase; any uppercase prefix character would break the `information_schema.tables` existence checks in `Repository_CheckCreate` and `DatabaseLogging_CheckCreate`.

## Schema Configuration

Schema and table prefix are configurable:

```json
{
  "Repository": {
    "SchemaName": "migrations",
    "TableBaseName": "Ray"
  }
}
```

**Result**: `migrations.RayMigrationRecord`, `migrations.RayProduct`, etc.

## Related Documentation

- [Logging Schema](logging-schema.md) - Logging tables
- [Template System](template-system.md) - How schema is created
- [Migration State Machine](../02-core-concepts/migration-state-machine.md)
