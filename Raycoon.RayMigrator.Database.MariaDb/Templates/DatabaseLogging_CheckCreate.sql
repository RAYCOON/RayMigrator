/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.2"

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
- Inserts master data for MigrationEvent types when it creates the log tables, and keeps the rows a parallel
  first start inserted meanwhile; two first starts at once may both report 1
- Creates the indexes on migration_log (migration_run_id) and (created_at) when missing, also on a log
  table an older engine created; a start that finds both runs no DDL
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration - MUST be lowercase for MariaDB (DAL-018)"

[Parameters]
# No SQL parameters required for this template

[ReturnValues]
# Format: SELECT 'code,message'
Success_0_Exists  = "0,Database logging infrastructure already exists"
Success_1_Created = "1,Database logging infrastructure successfully created"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: migration_event (lookup), migration_log (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "migration_log.created_at defaults to CURRENT_TIMESTAMP (UTC via session time_zone)"
Note6 = "MariaDB DDL causes implicit commit - no explicit transaction wrapping"
Note7 = "Uses idempotent CREATE TABLE IF NOT EXISTS; the MigrationEvent catalogue is inserted only when the log tables are created in this run (gated on @v_exists = 0), with ON DUPLICATE KEY UPDATE, which keeps a row that exists"
Note8 = "DAL-018: All identifiers (tables, columns) use unquoted snake_case"
Note9 = "The indexes stand outside the creation gate; each is looked up in information_schema first, because CREATE INDEX IF NOT EXISTS demands the INDEX privilege even when the index exists; the statement runs through PREPARE/EXECUTE, its no-op branch is DO 0, never a SELECT, so the final SELECT stays the only result set"
Note10 = "A parallel start of another host is safe without a lock: the metadata lock of the table serialises two CREATE INDEX IF NOT EXISTS, and the second only raises a note; two first starts can both find no log table and both insert the catalogue, so the second insert keeps the rows of the first (ON DUPLICATE KEY UPDATE tolerates the duplicate key only; INSERT IGNORE would also turn data errors of the strict mode into warnings)"
Note11 = "The longer index name has 33 characters plus the prefix: with the longest prefix RULE_4_4 allows (16 bytes) it has 49 of the 64 characters MariaDB allows"
================================================================================
*/

SET @v_exists = (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log');

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_event (
    id                     INT          NOT NULL,
    name                   VARCHAR(100) NOT NULL,
    description            VARCHAR(1000)    NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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
WHERE @v_exists = 0
-- A parallel first start may have inserted the catalogue since the check for the log table: keep its rows. The
-- column is qualified with the table, because v has an id as well and a bare id is ambiguous (1052).
ON DUPLICATE KEY UPDATE {CFG:TableBaseName}migration_event.id = {CFG:TableBaseName}migration_event.id;

-- Outside the creation gate, so that a log table an older engine created gets the indexes at its next start.
SET @v_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_log_migration_run_id') = 0,
    'CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_log_migration_run_id ON {CFG:TableBaseName}migration_log (migration_run_id)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SET @v_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_log_created_at') = 0,
    'CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_log_created_at ON {CFG:TableBaseName}migration_log (created_at)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SELECT CASE WHEN @v_exists > 0
    THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;
