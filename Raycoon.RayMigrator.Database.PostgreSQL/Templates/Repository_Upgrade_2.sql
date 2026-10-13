/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_2"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Schema version 2 (ADR-024): adds the run details to migration_run (operation, source, initiator,
host, error code and message, last heartbeat) and the migration_runSource lookup table
(Cli = 10, Ci = 20, Studio = 30), and writes row 2 of the repository_schema_version table.
"""

Behaviour = """
- Return value 2: schema version 2 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: every object is created only when missing; one transaction (the DO block), serialised with an advisory lock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
RayMigratorVersion = "TEXT | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_2  = "2,RayMigrator repository schema version [2] applied: migration_run run details and migration_run_source"

[ModificationNotes]
Note1 = "Result format: 'code,message' via RAISE NOTICE - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "New migration_run columns are nullable: rows of schema version 1 keep NULL"
Note4 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note5 = "DAL-017: All identifiers use unquoted snake_case"
================================================================================
*/

DO $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtext('RayMigrator_{CFG:SchemaName}_{CFG:TableBaseName}'));

    IF NOT EXISTS (SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version WHERE version = 2) THEN

        CREATE TABLE IF NOT EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_run_source (
            id                     SMALLINT       NOT NULL,
            name                   TEXT           NOT NULL,
            description            TEXT               NULL,
            CONSTRAINT pk_migration_run_source PRIMARY KEY (id)
        );

        COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_source IS 'Cli = 10, Ci = 20, Studio = 30';

        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run_source (id, name, description)
        VALUES
            (10, 'Cli', 'Started by the RayMigrator command line on a workstation or server'),
            (20, 'Ci', 'Started by the RayMigrator command line in a CI/CD pipeline'),
            (30, 'Studio', 'Started by RayMigrator Studio')
        ON CONFLICT (id) DO NOTHING;

        ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run
            ADD COLUMN IF NOT EXISTS migration_operation_id   SMALLINT        NULL,
            ADD COLUMN IF NOT EXISTS migration_run_source_id  SMALLINT        NULL,
            ADD COLUMN IF NOT EXISTS initiated_by             TEXT            NULL,
            ADD COLUMN IF NOT EXISTS initiated_by_subject     TEXT            NULL,
            ADD COLUMN IF NOT EXISTS host                     TEXT            NULL,
            ADD COLUMN IF NOT EXISTS error_code               TEXT            NULL,
            ADD COLUMN IF NOT EXISTS error_message            TEXT            NULL,
            ADD COLUMN IF NOT EXISTS last_heartbeat_at        TIMESTAMPTZ     NULL;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_migration_run_migration_operation' AND connamespace = '{CFG:SchemaName}'::regnamespace) THEN
            ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_migration_operation FOREIGN KEY (migration_operation_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_operation(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_migration_run_migration_run_source' AND connamespace = '{CFG:SchemaName}'::regnamespace) THEN
            ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_migration_run_source FOREIGN KEY (migration_run_source_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run_source(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
        END IF;

        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_run_migration_operation_id
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (migration_operation_id);
        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_run_migration_run_source_id
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (migration_run_source_id);

        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version (version, applied_at, raymigrator_version)
        VALUES (2, NOW(), @RayMigratorVersion)
        ON CONFLICT (version) DO NOTHING;

    END IF;

    RAISE NOTICE '2,RayMigrator repository schema version [2] applied: migration_run run details and migration_run_source';
END $$;
