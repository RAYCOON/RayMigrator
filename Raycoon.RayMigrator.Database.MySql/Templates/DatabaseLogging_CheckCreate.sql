/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist, and the indexes the
readers of the log filter by (rows per run, rows per time) when they are missing.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists (also when only the indexes were added)
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Inserts master data for MigrationEvent types
- Creates the indexes on migration_log (migration_run_id) and (created_at) when missing, also on a log
  table an older engine created; a start that finds both runs no DDL
- Idempotent per statement, because MySQL DDL causes implicit commits; serialised with GET_LOCK()
  against a parallel start
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
# No SQL parameters required for this template

[ReturnValues]
# Format: SELECT 'code,message'
Success_0_Exists  = "0,Database logging infrastructure already exists"
Success_1_Created = "1,Database logging infrastructure successfully created"
Error_-1_Lock     = "-1,RayMigrator could not acquire the database logging lock within 60 seconds"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: migration_event (lookup), migration_log (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "migration_log.created_at defaults to CURRENT_TIMESTAMP (UTC via session time_zone)"
Note6 = "MySQL DDL causes implicit commit - no explicit transaction wrapping"
Note7 = "Uses idempotent CREATE TABLE IF NOT EXISTS; the MigrationEvent catalogue is inserted only when the log tables are created in this run (gated on @v_exists = 0)"
Note8 = "DAL-018: All identifiers (tables, columns) use unquoted snake_case"
Note9 = "The indexes stand outside the creation gate and run through PREPARE/EXECUTE, because MySQL has no CREATE INDEX IF NOT EXISTS: each is created only when information_schema does not list it; the no-op branch is DO 0, never a SELECT, so the final SELECT stays the only result set"
Note10 = "GET_LOCK() serialises a parallel start of another host: without it two starts that both miss an index both create it and the second fails (1061); an index is created only under the lock, a start that could not acquire it reports -1"
Note11 = "The lock name is not the repository schema lock, so the log never waits for a repository step; it is cut to the 64 characters GET_LOCK() accepts, so databases whose names share the first 48 characters share the lock"
Note12 = "The longer index name has 33 characters plus the prefix: with the longest prefix RULE_4_4 allows (16 bytes) it has 49 of the 64 characters MySQL allows"
================================================================================
*/

-- A parallel start of another host on the same log database waits here, then finds the tables and the indexes.
SET @v_lock_name = CONCAT('raymigrator_log_', LEFT(DATABASE(), 48));
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

SET @v_exists = (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log');

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_event (
    id                     INT          NOT NULL,
    name                   VARCHAR(100) NOT NULL,
    description            VARCHAR(1000)    NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_log (
    id                     BIGINT       NOT NULL AUTO_INCREMENT,
    log_level_id           TINYINT UNSIGNED NOT NULL,
    migration_event_id     INT              NULL,
    run_mode_id            TINYINT UNSIGNED     NULL,
    product_id             INT              NULL,
    environment_id         INT              NULL,
    migration_run_id       INT              NULL,
    migration_record_id           INT              NULL,
    release_version        VARCHAR(100)     NULL,
    target_group_alias     VARCHAR(100)     NULL,
    target_alias           VARCHAR(100)     NULL,
    filename               VARCHAR(300)     NULL,
    file_order_id          INT              NULL,
    file_block_id          INT              NULL,
    message                TEXT             NULL,
    created_at             TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO {CFG:TableBaseName}migration_event (id, name, description)
SELECT v.id, v.name, v.description
FROM (
              SELECT 0 AS id, 'UnspecifiedEvent' AS name, '' AS description
    UNION ALL SELECT 10, 'CommandLineParsing', ''
    UNION ALL SELECT 20, 'EnvironmentVariableReplacement', ''
    UNION ALL SELECT 31, 'CreateDatabaseLogger', ''
    UNION ALL SELECT 40, 'ValidateRayMigratorOptions', ''
    UNION ALL SELECT 50, 'CreateApplicationHost', ''
    UNION ALL SELECT 60, 'InitializeDalSpecificProperties', ''
    UNION ALL SELECT 70, 'ValidateConnectionStrings', ''
    UNION ALL SELECT 80, 'RayMigratorServiceStart', ''
    UNION ALL SELECT 100, 'TemplateExecutionRepositoryCheckCreate', ''
    UNION ALL SELECT 101, 'TemplateExecutionRepositoryUpgrade', ''
    UNION ALL SELECT 102, 'TemplateExecutionRepositoryMigratorMetaCheckInsert', ''
    UNION ALL SELECT 110, 'TemplateExecutionRepositoryMigrationRunInsert', ''
    UNION ALL SELECT 111, 'TemplateExecutionRepositoryMigrationRunUpdate', ''
    UNION ALL SELECT 112, 'TemplateExecutionRepositoryMigrationRunSelectOrphaned', ''
    UNION ALL SELECT 113, 'TemplateExecutionRepositoryMigrationRunFixOrphaned', ''
    UNION ALL SELECT 114, 'TemplateExecutionRepositoryMigrationFixOrphaned', ''
    UNION ALL SELECT 115, 'TemplateExecutionRepositoryMigrationRunHeartbeat', ''
    UNION ALL SELECT 120, 'TemplateExecutionRepositoryProductCheckInsert', ''
    UNION ALL SELECT 121, 'TemplateExecutionRepositoryEnvironmentCheckInsert', ''
    UNION ALL SELECT 122, 'TemplateExecutionRepositoryProductSelect', ''
    UNION ALL SELECT 123, 'TemplateExecutionRepositoryEnvironmentSelect', ''
    UNION ALL SELECT 130, 'TemplateExecutionRepositoryMigrationInsert', ''
    UNION ALL SELECT 131, 'TemplateExecutionRepositoryMigrationUpdate', ''
    UNION ALL SELECT 132, 'TemplateExecutionRepositoryMigrationGetInterrupted', ''
    UNION ALL SELECT 133, 'TemplateExecutionRepositoryMigrationUpdateRollback', ''
    UNION ALL SELECT 134, 'TemplateExecutionRepositoryMigrationSelect', ''
    UNION ALL SELECT 135, 'TemplateExecutionRepositoryMigrationUpdateHash', ''
    UNION ALL SELECT 136, 'TemplateExecutionRepositoryMigrationRunSelect', ''
    UNION ALL SELECT 137, 'TemplateExecutionRepositoryMigrationRecordHistorySelect', ''
    UNION ALL SELECT 1000, 'RayMigratorServiceShutdown', ''
) AS v
WHERE @v_exists = 0;

-- Outside the creation gate, so that a log table an older engine created gets the indexes at its next start.
SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_log_migration_run_id') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_log_migration_run_id ON {CFG:TableBaseName}migration_log (migration_run_id)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_log_created_at') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_log_created_at ON {CFG:TableBaseName}migration_log (created_at)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

DO RELEASE_LOCK(@v_lock_name);

SELECT CASE
    WHEN IFNULL(@v_lock_acquired, 0) = 0 THEN '-1,RayMigrator could not acquire the database logging lock within 60 seconds'
    WHEN @v_exists > 0 THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;
