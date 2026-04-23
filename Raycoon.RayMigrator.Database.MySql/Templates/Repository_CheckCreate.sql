/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Checks for repository existence and completeness. Creates RayMigrator
infrastructure on the target database if necessary. Returns the VersionId.
"""

Behaviour = """
- Return value >= 0: Success (logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Creates all 11 repository tables with master data
- Inserts new MigratorMeta record on first run or version change
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
RayMigratorVersion     = "VARCHAR(20) | REQUIRED | The RayMigrator application version (e.g., '3.0.0')"
RepositoryDatabaseType = "VARCHAR(20) | REQUIRED | The database type for the repository (e.g., 'MySql')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N           = "N (VersionId),RayMigrator repository already exists. Using VersionId [N]."
Success_N_Created   = "N (VersionId),RayMigrator repository-tables with master data and new VersionId [N] successfully created"
Success_N_NewVer    = "N (VersionId),RayMigrator repository already exists. New VersionId [N] created."
Error_-10_Incomplete        = "-10,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of [11]."
Error_-11_PartialNoVersion  = "-11,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of the expected amount of [0]."
Error_-12_MultipleVersions  = "-12,Multiple [migrator_meta]-entries found for RepositoryVersion [...] RepositoryDatabaseType [...] RayMigratorVersion [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for all timestamps (session time_zone='+00:00' ensures UTC)"
Note4 = "RepositoryVersion constant MUST match Version in header"
Note5 = "MySQL DDL causes implicit commit - tables are created individually with IF NOT EXISTS"
Note6 = "Uses idempotent CREATE TABLE IF NOT EXISTS and INSERT IGNORE"
Note7 = "Tables must be created in FK dependency order"
Note8 = "Tables created: migrator_meta, product, environment, migration_run, migration_run_meta, migration_record, migration_record_history, migration_run_mode, migration_operation, migration_run_result, migration_status"
Note9 = "DAL-018: All identifiers (tables, columns, constraints, indexes) use unquoted snake_case; only reserved-word collisions keep backticks"
Note10 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET @v_repository_version = '2026-04-18.1';

-- Capture pre-DDL state
SET @v_number_of_tables_found = (
    SELECT COUNT(*) FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME IN (
          '{CFG:TableBaseName}migrator_meta',
          '{CFG:TableBaseName}product',
          '{CFG:TableBaseName}migration_run',
          '{CFG:TableBaseName}migration_run_meta',
          '{CFG:TableBaseName}migration_record',
          '{CFG:TableBaseName}migration_record_history',
          '{CFG:TableBaseName}migration_run_mode',
          '{CFG:TableBaseName}migration_operation',
          '{CFG:TableBaseName}migration_run_result',
          '{CFG:TableBaseName}migration_status',
          '{CFG:TableBaseName}environment'
      )
);

SET @v_version_table_exists = (
    SELECT COUNT(*) FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migrator_meta'
);

-- Lookup tables (no FK dependencies)
CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_operation (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_result (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_mode (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_status (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migrator_meta (
    id                             INT          NOT NULL AUTO_INCREMENT,
    repository_version             VARCHAR(100) NOT NULL,
    repository_database_type       VARCHAR(100) NOT NULL,
    created_by_raymigrator_version VARCHAR(100) NOT NULL,
    created_at                     TIMESTAMP    NOT NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}product (
    id                     INT          NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(100) NOT NULL,
    name_lower             VARCHAR(100) NOT NULL,
    created_at             TIMESTAMP    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uix_{CFG:TableBaseName}product_name_lower (name_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}environment (
    id                     INT          NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(100) NOT NULL,
    name_lower             VARCHAR(100) NOT NULL,
    created_at             TIMESTAMP    NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uix_{CFG:TableBaseName}environment_name_lower (name_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_meta (
    migration_run_id            INT          NOT NULL,
    migration_run_settings_json TEXT             NULL,
    description                 TEXT             NULL,
    PRIMARY KEY (migration_run_id),
    CONSTRAINT fk_migration_run_meta_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:TableBaseName}migration_run(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Master data (INSERT IGNORE is idempotent)
INSERT IGNORE INTO {CFG:TableBaseName}migration_run_mode (id, name, description) VALUES
    (10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
    (20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
    (100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.');

INSERT IGNORE INTO {CFG:TableBaseName}migration_operation (id, name, description) VALUES
    (5, 'Rollback', 'Performing Rollback of current MigrationRun'),
    (50, 'MigrateDown', 'Performing Down-Migration'),
    (100, 'MigrateUp', 'Performing Up-Migration');

INSERT IGNORE INTO {CFG:TableBaseName}migration_run_result (id, name, description) VALUES
    (10, 'Running', 'Migration process is currently running'),
    (90, 'Error', 'Migration(s) stopped due to error(s)'),
    (100, 'Ok', 'Migration(s) successfully executed');

INSERT IGNORE INTO {CFG:TableBaseName}migration_status (id, name, description) VALUES
    (10, 'Pending', 'Record created, execution pending'),
    (20, 'Executing', 'SQL blocks are being executed'),
    (30, 'Failed', 'Execution failed, DB state unclear'),
    (50, 'NotMigrated', 'Not deployed / rolled back'),
    (100, 'Migrated', 'Successfully deployed');

-- Handle version logic
SET @v_version_id = NULL;
SET @v_number_of_rows = 0;

SET @v_version_id = (SELECT id FROM {CFG:TableBaseName}migrator_meta
     WHERE repository_version = @v_repository_version
       AND repository_database_type = @RepositoryDatabaseType
       AND created_by_raymigrator_version = @RayMigratorVersion
     LIMIT 1);

SET @v_number_of_rows = (SELECT COUNT(*) FROM {CFG:TableBaseName}migrator_meta
     WHERE repository_version = @v_repository_version
       AND repository_database_type = @RepositoryDatabaseType
       AND created_by_raymigrator_version = @RayMigratorVersion);

-- Insert version if not found (idempotent via WHERE clause)
INSERT INTO {CFG:TableBaseName}migrator_meta
    (repository_version, repository_database_type, created_by_raymigrator_version, created_at)
SELECT @v_repository_version, @RepositoryDatabaseType, @RayMigratorVersion, CURRENT_TIMESTAMP
FROM DUAL
WHERE @v_number_of_rows = 0;

SET @v_final_version_id = IF(@v_version_id IS NOT NULL, @v_version_id, LAST_INSERT_ID());

SELECT CASE
    -- Repository existed before but is incomplete/corrupt
    WHEN @v_version_table_exists > 0 AND @v_number_of_tables_found != 11 THEN
        CONCAT('-10,RayMigrator repository incomplete or corrupt. Repository contains [', CAST(@v_number_of_tables_found AS CHAR), '] tables instead of [11].')

    -- Repository exists and matching version found
    WHEN @v_version_table_exists > 0 AND @v_number_of_rows = 1 THEN
        CONCAT(CAST(@v_version_id AS CHAR), ',RayMigrator repository already exists. Using VersionId [', CAST(@v_version_id AS CHAR), '].')

    -- Repository exists but version not found (new version inserted above)
    WHEN @v_version_table_exists > 0 AND @v_number_of_rows = 0 THEN
        CONCAT(CAST(@v_final_version_id AS CHAR), ',RayMigrator repository already exists. New VersionId [', CAST(@v_final_version_id AS CHAR), '] created.')

    -- Repository exists but multiple matching versions (error)
    WHEN @v_version_table_exists > 0 AND @v_number_of_rows > 1 THEN
        CONCAT('-12,Multiple [migrator_meta]-entries found for RepositoryVersion [', IFNULL(@v_repository_version, 'NULL'), '] RepositoryDatabaseType [', IFNULL(@RepositoryDatabaseType, 'NULL'), '] RayMigratorVersion [', IFNULL(@RayMigratorVersion, 'NULL'), '].')

    -- No version table but some tables exist (corrupt - before DDL ran)
    WHEN @v_version_table_exists = 0 AND @v_number_of_tables_found != 0 THEN
        CONCAT('-11,RayMigrator repository incomplete or corrupt. Repository contains [', CAST(@v_number_of_tables_found AS CHAR), '] tables instead of the expected amount of [0].')

    -- No repository existed - everything was just created
    ELSE
        CONCAT(CAST(@v_final_version_id AS CHAR), ',RayMigrator repository-tables with master data and new VersionId [', CAST(@v_final_version_id AS CHAR), '] successfully created')
END;
