/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-09-09.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Inserts master data for MigrationEvent types
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

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: migration_event (lookup), migration_log (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "migration_log.created_at defaults to CURRENT_TIMESTAMP (UTC via session time_zone)"
Note6 = "MySQL DDL causes implicit commit - no explicit transaction wrapping"
Note7 = "Uses idempotent CREATE TABLE IF NOT EXISTS; the MigrationEvent catalogue is inserted only when the log tables are created in this run (gated on @v_exists = 0)"
Note8 = "DAL-018: All identifiers (tables, columns) use unquoted snake_case"
================================================================================
*/

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
    UNION ALL SELECT 110, 'TemplateExecutionRepositoryMigrationRunInsert', ''
    UNION ALL SELECT 111, 'TemplateExecutionRepositoryMigrationRunUpdate', ''
    UNION ALL SELECT 112, 'TemplateExecutionRepositoryMigrationRunSelectOrphaned', ''
    UNION ALL SELECT 113, 'TemplateExecutionRepositoryMigrationRunFixOrphaned', ''
    UNION ALL SELECT 114, 'TemplateExecutionRepositoryMigrationFixOrphaned', ''
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

SELECT CASE WHEN @v_exists > 0
    THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;
