/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Reports the schema version of the repository (ADR-024). Creates the schema and the
repository_schema_version table when they do not exist; creates nothing else. The engine
applies the missing Repository_Upgrade_{n} steps afterwards.
"""

Behaviour = """
- First code: highest applied schema version, 0 when the repository has none
- Second code: 1 when the migrator_meta table exists, 0 otherwise. With version 0 this
  means a repository of a RayMigrator before 0.17.0, which the engine refuses
- Idempotent; a parallel start on the same repository is serialised with an advisory lock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
# none

[ReturnValues]
# Format: RAISE NOTICE 'schemaVersion,unversionedTables,message'
Success_N        = "N (schema version),0|1,RayMigrator repository has schema version [N]"
Success_Legacy   = "0,1,RayMigrator repository tables exist without a schema version"
Success_None     = "0,0,RayMigrator repository does not exist yet"

[ModificationNotes]
Note1 = "Result format: 'schemaVersion,unversionedTables,message' via RAISE NOTICE - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Creates only the schema and the repository_schema_version table; the repository tables come from Repository_Upgrade_1"
Note4 = "DAL-017: All identifiers use unquoted snake_case"
Note5 = "Delimit the DO block with $$ and nothing else: the DAL replaces $$ with a tag unique to the call"
================================================================================
*/

DO $$
DECLARE
    v_schema_version INT;
    v_unversioned_tables INT;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtext('RayMigrator_{CFG:SchemaName}_{CFG:TableBaseName}'));

    CREATE SCHEMA IF NOT EXISTS {CFG:SchemaName};

    CREATE TABLE IF NOT EXISTS {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version (
        version                INT            NOT NULL,
        applied_at             TIMESTAMPTZ    NOT NULL,
        raymigrator_version    TEXT           NOT NULL,
        CONSTRAINT pk_repository_schema_version PRIMARY KEY (version)
    );

    SELECT COALESCE(MAX(version), 0) INTO v_schema_version
    FROM {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version;

    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = '{CFG:SchemaName}'
          AND table_name = '{CFG:TableBaseName}migrator_meta'
    ) THEN 1 ELSE 0 END INTO v_unversioned_tables;

    RAISE NOTICE '%,%,%', v_schema_version, v_unversioned_tables,
        CASE
            WHEN v_schema_version > 0 THEN 'RayMigrator repository has schema version [' || v_schema_version || ']'
            WHEN v_unversioned_tables = 1 THEN 'RayMigrator repository tables exist without a schema version'
            ELSE 'RayMigrator repository does not exist yet'
        END;
END $$;
