/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

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
Note7 = "Uses idempotent CREATE TABLE IF NOT EXISTS and INSERT IGNORE"
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
    migration_id           INT              NULL,
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

INSERT IGNORE INTO {CFG:TableBaseName}migration_event (id, name, description)
VALUES
    (0, 'UnspecifiedEvent', ''),
    (10, 'CommandLineParsing', ''),
    (20, 'EnvironmentVariableReplacement', ''),
    (31, 'CreateDatabaseLogger', ''),
    (32, 'CreateCompositeLogger', ''),
    (40, 'ValidateRayMigratorOptions', ''),
    (50, 'CreateApplicationHost', ''),
    (60, 'InitializeDalSpecificProperties', ''),
    (70, 'ValidateConnectionStrings', ''),
    (80, 'RayMigratorServiceStart', ''),
    (100, 'CreateAndStartRayMigratorService', ''),
    (1000, 'RayMigratorServiceShutdown', '');

SELECT CASE WHEN @v_exists > 0
    THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;
