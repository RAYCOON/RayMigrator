/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "PostgreSQL"
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
- Creates schema if not exists
- Inserts master data for MigrationEvent types
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
# NOTE: For DatabaseLogging_* templates, values come from the 'Logging' section of appsettings
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'log_') - MUST be lowercase for PostgreSQL (DAL-017)"

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
Note5 = "migration_log.created_at is TIMESTAMPTZ, defaults to NOW()"
Note6 = "DAL-017: All identifiers (tables, columns, constraints) use unquoted snake_case per PostgreSQL community convention"
================================================================================
*/

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = '{CFG:SchemaName}'
          AND table_name = '{CFG:TableBaseName}migration_log'
    ) THEN
        RAISE NOTICE '0,Database logging infrastructure already exists';
        RETURN;
    END IF;

    -- Create schema if not exists
    CREATE SCHEMA IF NOT EXISTS {CFG:SchemaName};

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_event (
        id                     INT          NOT NULL,
        name                   TEXT         NOT NULL,
        description            TEXT             NULL,
        CONSTRAINT pk_migration_event PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_log (
        id                     BIGINT       GENERATED ALWAYS AS IDENTITY NOT NULL,
        log_level_id           SMALLINT     NOT NULL,
        migration_event_id     INT              NULL,
        run_mode_id            SMALLINT         NULL,
        product_id             INT              NULL,
        environment_id         INT              NULL,
        migration_run_id       INT              NULL,
        migration_record_id           INT              NULL,
        release_version        TEXT             NULL,
        target_group_alias     TEXT             NULL,
        target_alias           TEXT             NULL,
        filename               TEXT             NULL,
        file_order_id          INT              NULL,
        file_block_id          INT              NULL,
        message                TEXT             NULL,
        created_at             TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
        CONSTRAINT pk_migration_log PRIMARY KEY (id)
    );

    -- Master data: migration_event
    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_event (id, name, description)
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

    RAISE NOTICE '1,Database logging infrastructure successfully created';
END $$;
