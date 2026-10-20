/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "PostgreSQL"
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
- Creates schema if not exists
- Inserts master data for MigrationEvent types
- Creates the indexes on migration_log (migration_run_id) and (created_at) when missing, also on a log
  table an older engine created; a start that finds both runs no DDL
- One transaction (the DO block), serialised with an advisory lock against a parallel start
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
Note7 = "The indexes stand outside the creation branch; each is looked up in pg_indexes first, because CREATE INDEX IF NOT EXISTS demands ownership of the table even when the index exists"
Note8 = "The advisory lock serialises a parallel start of another host: without it the second of two CREATE INDEX of the same name fails (unique violation in pg_class); the key differs from the repository schema lock, so the log never waits for a repository step"
Note9 = "The longer index name has 33 characters plus the prefix: with the longest prefix RULE_4_4 allows (16 bytes) it has 49 of the 63 bytes PostgreSQL keeps"
Note10 = "Result via RAISE NOTICE: the DAL returns the last notice, so the result notice stays the last statement of the block"
================================================================================
*/

DO $$
DECLARE
    v_existed BOOLEAN;
BEGIN
    -- A parallel start of another host on the same log tables waits here, then finds the tables and the indexes.
    PERFORM pg_advisory_xact_lock(hashtext('RayMigrator_Log_{CFG:SchemaName}_{CFG:TableBaseName}'));

    v_existed := EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = '{CFG:SchemaName}'
          AND table_name = '{CFG:TableBaseName}migration_log'
    );

    IF NOT v_existed THEN

        -- Create schema if not exists
        CREATE SCHEMA IF NOT EXISTS {CFG:SchemaName};

        CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_event (
            id                     INT          NOT NULL,
            name                   TEXT         NOT NULL,
            description            TEXT             NULL,
            CONSTRAINT pk_{CFG:TableBaseName}migration_event PRIMARY KEY (id)
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
            CONSTRAINT pk_{CFG:TableBaseName}migration_log PRIMARY KEY (id)
        );

        -- Master data: migration_event
        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_event (id, name, description)
        VALUES
            (0, 'UnspecifiedEvent', ''),
            (10, 'CommandLineParsing', ''),
            (20, 'EnvironmentVariableReplacement', ''),
            (31, 'CreateDatabaseLogger', ''),
            (40, 'ValidateRayMigratorOptions', ''),
            (50, 'CreateApplicationHost', ''),
            (60, 'InitializeDalSpecificProperties', ''),
            (70, 'ValidateConnectionStrings', ''),
            (80, 'RayMigratorServiceStart', ''),
            (100, 'TemplateExecutionRepositoryCheckCreate', ''),
            (101, 'TemplateExecutionRepositoryUpgrade', ''),
            (102, 'TemplateExecutionRepositoryMigratorMetaCheckInsert', ''),
            (110, 'TemplateExecutionRepositoryMigrationRunInsert', ''),
            (111, 'TemplateExecutionRepositoryMigrationRunUpdate', ''),
            (112, 'TemplateExecutionRepositoryMigrationRunSelectOrphaned', ''),
            (113, 'TemplateExecutionRepositoryMigrationRunFixOrphaned', ''),
            (114, 'TemplateExecutionRepositoryMigrationFixOrphaned', ''),
            (115, 'TemplateExecutionRepositoryMigrationRunHeartbeat', ''),
            (120, 'TemplateExecutionRepositoryProductCheckInsert', ''),
            (121, 'TemplateExecutionRepositoryEnvironmentCheckInsert', ''),
            (122, 'TemplateExecutionRepositoryProductSelect', ''),
            (123, 'TemplateExecutionRepositoryEnvironmentSelect', ''),
            (130, 'TemplateExecutionRepositoryMigrationInsert', ''),
            (131, 'TemplateExecutionRepositoryMigrationUpdate', ''),
            (132, 'TemplateExecutionRepositoryMigrationGetInterrupted', ''),
            (133, 'TemplateExecutionRepositoryMigrationUpdateRollback', ''),
            (134, 'TemplateExecutionRepositoryMigrationSelect', ''),
            (135, 'TemplateExecutionRepositoryMigrationUpdateHash', ''),
            (136, 'TemplateExecutionRepositoryMigrationRunSelect', ''),
            (137, 'TemplateExecutionRepositoryMigrationRecordHistorySelect', ''),
            (1000, 'RayMigratorServiceShutdown', '');

    END IF;

    -- Outside the creation branch, so that a log table an older engine created gets the indexes at its next start.
    -- Each index is looked up first: CREATE INDEX IF NOT EXISTS demands ownership of the table even when the index
    -- exists, and a start that finds both runs no DDL.
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = '{CFG:SchemaName}' AND indexname = 'ix_{CFG:TableBaseName}migration_log_migration_run_id') THEN
        CREATE INDEX ix_{CFG:TableBaseName}migration_log_migration_run_id
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_log (migration_run_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = '{CFG:SchemaName}' AND indexname = 'ix_{CFG:TableBaseName}migration_log_created_at') THEN
        CREATE INDEX ix_{CFG:TableBaseName}migration_log_created_at
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_log (created_at);
    END IF;

    -- The DAL returns the last notice: nothing may raise one after the result.
    IF v_existed THEN
        RAISE NOTICE '0,Database logging infrastructure already exists';
    ELSE
        RAISE NOTICE '1,Database logging infrastructure successfully created';
    END IF;
END $$;
