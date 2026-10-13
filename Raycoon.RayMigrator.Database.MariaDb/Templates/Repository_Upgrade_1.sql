/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_1"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Schema version 1 (ADR-024): creates the 11 repository tables with their indexes, foreign
keys and lookup master data, and writes row 1 of the repository_schema_version table.
Runs on a repository that has no schema version yet.
"""

Behaviour = """
- Return value 1: schema version 1 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent per statement (CREATE TABLE IF NOT EXISTS, INSERT IGNORE), because MariaDB DDL
  causes implicit commits; a parallel start on the same repository is serialised with GET_LOCK()
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MariaDB schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MariaDB (DAL-018)"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1  = "1,RayMigrator repository schema version [1] applied: 11 tables with master data"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "Use CURRENT_TIMESTAMP for all audit timestamps (the DAL sets the session time zone to UTC)"
Note4 = "Tables created: migrator_meta, product, environment, migration_run, migration_run_meta, migration_record, migration_record_history, migration_run_mode, migration_operation, migration_run_result, migration_status"
Note5 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note6 = "DAL-018: All identifiers use unquoted snake_case; MariaDB DDL causes implicit commits"
================================================================================
*/

SET @v_lock_name = CONCAT('raymigrator_schema_', DATABASE());
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

-- Lookup tables (no FK dependencies)
CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_operation (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_result (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_mode (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_status (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migrator_meta (
    id                             INT          NOT NULL AUTO_INCREMENT,
    raymigrator_version            VARCHAR(100) NOT NULL,
    repository_database_type       VARCHAR(100) NOT NULL,
    created_at                     TIMESTAMP    NOT NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}product (
    id                     INT          NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(100) NOT NULL,
    name_lower             VARCHAR(100) NOT NULL,
    created_at             TIMESTAMP    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uix_{CFG:TableBaseName}product_name_lower (name_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}environment (
    id                     INT          NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(100) NOT NULL,
    name_lower             VARCHAR(100) NOT NULL,
    created_at             TIMESTAMP    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uix_{CFG:TableBaseName}environment_name_lower (name_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tables with FK dependencies (created after their referenced tables)
CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run (
    id                      INT          NOT NULL AUTO_INCREMENT,
    migrator_meta_id        INT          NOT NULL,
    product_id              INT          NOT NULL,
    environment_id          INT          NOT NULL,
    migration_run_mode_id   TINYINT UNSIGNED NOT NULL,
    migration_run_result_id TINYINT UNSIGNED NOT NULL,
    from_release_version    VARCHAR(100)     NULL,
    to_release_version      VARCHAR(100)     NULL,
    started_at              TIMESTAMP    NOT NULL,
    finished_at             TIMESTAMP        NULL,
    duration_in_ms          BIGINT           NULL,
    PRIMARY KEY (id),
    INDEX ix_{CFG:TableBaseName}migration_run_environment_id (environment_id),
    CONSTRAINT fk_migration_run_product FOREIGN KEY (product_id) REFERENCES {CFG:TableBaseName}product(id),
    CONSTRAINT fk_migration_run_environment FOREIGN KEY (environment_id) REFERENCES {CFG:TableBaseName}environment(id),
    CONSTRAINT fk_migration_run_migrator_meta FOREIGN KEY (migrator_meta_id) REFERENCES {CFG:TableBaseName}migrator_meta(id),
    CONSTRAINT fk_migration_run_migration_run_result FOREIGN KEY (migration_run_result_id) REFERENCES {CFG:TableBaseName}migration_run_result(id),
    CONSTRAINT fk_migration_run_migration_run_mode FOREIGN KEY (migration_run_mode_id) REFERENCES {CFG:TableBaseName}migration_run_mode(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_meta (
    migration_run_id            INT          NOT NULL,
    migration_run_settings_json TEXT             NULL,
    description                 TEXT             NULL,
    PRIMARY KEY (migration_run_id),
    CONSTRAINT fk_migration_run_meta_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:TableBaseName}migration_run(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_record (
    id                        INT          NOT NULL AUTO_INCREMENT,
    product_id                INT          NOT NULL,
    environment_id            INT          NOT NULL,
    migration_run_id          INT          NOT NULL,
    migration_run_mode_id     TINYINT UNSIGNED NOT NULL,
    migration_operation_id    TINYINT UNSIGNED NOT NULL,
    migration_status_id       TINYINT UNSIGNED NOT NULL,
    release_version           VARCHAR(100) NOT NULL,
    target_group_alias        VARCHAR(100) NOT NULL,
    target_alias              VARCHAR(100) NOT NULL,
    filename                  VARCHAR(200) NOT NULL,
    file_order_id             INT          NOT NULL,
    file_up_hash              VARCHAR(100) NOT NULL,
    file_up_config_hash       VARCHAR(100)     NULL,
    file_up_blocks_hash       VARCHAR(100) NOT NULL,
    file_up_blocks_migrated   INT          NOT NULL,
    file_up_blocks_total      INT          NOT NULL,
    file_up_config_json       TEXT             NULL,
    migrate_down_file_exists  BOOLEAN      NOT NULL,
    file_down_hash            VARCHAR(100)     NULL,
    file_down_config_hash     VARCHAR(100)     NULL,
    file_down_blocks_hash     VARCHAR(100)     NULL,
    file_down_blocks_migrated INT              NULL,
    file_down_blocks_total    INT              NULL,
    file_down_config_json     TEXT             NULL,
    started_at                TIMESTAMP        NULL,
    finished_at               TIMESTAMP        NULL,
    duration_in_ms            BIGINT           NULL,
    PRIMARY KEY (id),
    INDEX ix_{CFG:TableBaseName}migration_record_environment_id (environment_id),
    CONSTRAINT fk_migration_record_product FOREIGN KEY (product_id) REFERENCES {CFG:TableBaseName}product(id),
    CONSTRAINT fk_migration_record_environment FOREIGN KEY (environment_id) REFERENCES {CFG:TableBaseName}environment(id),
    CONSTRAINT fk_migration_record_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:TableBaseName}migration_run(id),
    CONSTRAINT fk_migration_record_migration_run_mode FOREIGN KEY (migration_run_mode_id) REFERENCES {CFG:TableBaseName}migration_run_mode(id),
    CONSTRAINT fk_migration_record_migration_operation FOREIGN KEY (migration_operation_id) REFERENCES {CFG:TableBaseName}migration_operation(id),
    CONSTRAINT fk_migration_record_migration_status FOREIGN KEY (migration_status_id) REFERENCES {CFG:TableBaseName}migration_status(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_record_history (
    id                        INT          NOT NULL AUTO_INCREMENT,
    migration_record_id       INT          NOT NULL,
    product_id                INT          NOT NULL,
    environment_id            INT          NOT NULL,
    migration_run_id          INT          NOT NULL,
    migration_run_mode_id     TINYINT UNSIGNED NOT NULL,
    migration_operation_id    TINYINT UNSIGNED NOT NULL,
    migration_status_id       TINYINT UNSIGNED NOT NULL,
    release_version           VARCHAR(100) NOT NULL,
    target_group_alias        VARCHAR(100) NOT NULL,
    target_alias              VARCHAR(100) NOT NULL,
    filename                  VARCHAR(200) NOT NULL,
    file_order_id             INT          NOT NULL,
    file_up_hash              VARCHAR(100) NOT NULL,
    file_up_config_hash       VARCHAR(100)     NULL,
    file_up_blocks_hash       VARCHAR(100) NOT NULL,
    file_up_blocks_migrated   INT          NOT NULL,
    file_up_blocks_total      INT          NOT NULL,
    file_up_config_json       TEXT             NULL,
    migrate_down_file_exists  BOOLEAN      NOT NULL,
    file_down_hash            VARCHAR(100)     NULL,
    file_down_config_hash     VARCHAR(100)     NULL,
    file_down_blocks_hash     VARCHAR(100)     NULL,
    file_down_blocks_migrated INT              NULL,
    file_down_blocks_total    INT              NULL,
    file_down_config_json     TEXT             NULL,
    started_at                TIMESTAMP        NULL,
    finished_at               TIMESTAMP        NULL,
    duration_in_ms            BIGINT           NULL,
    historized_at             TIMESTAMP    NOT NULL  DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    INDEX ix_{CFG:TableBaseName}migration_record_history_migration_record_id (migration_record_id),
    INDEX ix_{CFG:TableBaseName}migration_record_history_environment_id (environment_id),
    CONSTRAINT fk_migration_record_history_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:TableBaseName}migration_run(id),
    CONSTRAINT fk_migration_record_history_product FOREIGN KEY (product_id) REFERENCES {CFG:TableBaseName}product(id),
    CONSTRAINT fk_migration_record_history_environment FOREIGN KEY (environment_id) REFERENCES {CFG:TableBaseName}environment(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- Master data (idempotent: existing rows are kept)
INSERT IGNORE INTO {CFG:TableBaseName}migration_run_mode (id, name, description) VALUES
    (10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
    (20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
    (100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.');

INSERT IGNORE INTO {CFG:TableBaseName}migration_operation (id, name, description) VALUES
    (5, 'Rollback', 'Performing Rollback of current MigrationRun'),
    (50, 'MigrateDown', 'Performing Down-Migration'),
    (100, 'MigrateUp', 'Performing Up-Migration'),
    (110, 'Baseline', 'Marking migration files as migrated without executing them (baseline command)');

INSERT IGNORE INTO {CFG:TableBaseName}migration_run_result (id, name, description) VALUES
    (10, 'Running', 'Migration process is currently running'),
    (50, 'PartialSuccess', 'Migration(s) finished but at least one file was skipped or left Failed'),
    (80, 'Recovered', 'Migration(s) failed and the configured error recovery rolled back cleanly'),
    (90, 'Error', 'Migration(s) stopped due to error(s)'),
    (100, 'Ok', 'Migration(s) successfully executed');

INSERT IGNORE INTO {CFG:TableBaseName}migration_status (id, name, description) VALUES
    (10, 'Pending', 'Record created, execution pending'),
    (20, 'Executing', 'SQL blocks are being executed'),
    (30, 'Failed', 'Execution failed, DB state unclear'),
    (50, 'NotMigrated', 'Not deployed / rolled back'),
    (100, 'Migrated', 'Successfully deployed');

INSERT IGNORE INTO {CFG:TableBaseName}repository_schema_version (version, applied_at, raymigrator_version)
VALUES (1, CURRENT_TIMESTAMP, @RayMigratorVersion);

DO RELEASE_LOCK(@v_lock_name);

SELECT '1,RayMigrator repository schema version [1] applied: 11 tables with master data';
