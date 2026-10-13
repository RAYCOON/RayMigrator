/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigratorMeta_CheckInsert"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Registers the RayMigrator version in migrator_meta if it is not registered yet and returns
the id of its row, which every migration_run of this version references as migrator_meta_id.
Runs after Repository_CheckCreate and the Repository_Upgrade_{n} steps (ADR-024).
"""

Behaviour = """
- Return value > 0: MigratorMetaId (logged at Debug level)
- Return value < 0: Error (logged at Error level, command aborted)
- One row per RayMigratorVersion and RepositoryDatabaseType; a parallel first run of the
  same version is serialised with an advisory lock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
RayMigratorVersion     = "TEXT | REQUIRED | The RayMigrator application version (e.g., '0.17.0')"
RepositoryDatabaseType = "TEXT | REQUIRED | The database type for the repository (e.g., 'PostgreSQL')"

[ReturnValues]
# Format: RAISE NOTICE 'code,message'
Success_N         = "N (MigratorMetaId),RayMigrator version [X] is registered with MigratorMetaId [N]"
Success_N_New     = "N (MigratorMetaId),RayMigrator version [X] registered with new MigratorMetaId [N]"
Error_-12_Multi   = "-12,Multiple [migrator_meta]-entries found for RayMigratorVersion [...] RepositoryDatabaseType [...]."

[ModificationNotes]
Note1 = "Result format: 'code,message' via RAISE NOTICE - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use NOW() for all audit timestamps (writes to TIMESTAMPTZ columns)"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

DO $$
DECLARE
    v_version_id INT;
    v_number_of_rows INT;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtext('MigratorMeta_' || @RayMigratorVersion));

    SELECT COUNT(*) INTO v_number_of_rows
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
    WHERE raymigrator_version = @RayMigratorVersion
      AND repository_database_type = @RepositoryDatabaseType;

    IF v_number_of_rows > 1 THEN
        RAISE EXCEPTION '-12,Multiple [migrator_meta]-entries found for RayMigratorVersion [%] RepositoryDatabaseType [%].',
            COALESCE(@RayMigratorVersion, 'NULL'), COALESCE(@RepositoryDatabaseType, 'NULL');
    END IF;

    IF v_number_of_rows = 1 THEN
        SELECT id INTO v_version_id
        FROM {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
        WHERE raymigrator_version = @RayMigratorVersion
          AND repository_database_type = @RepositoryDatabaseType;

        RAISE NOTICE '%,RayMigrator version [%] is registered with MigratorMetaId [%]', v_version_id, @RayMigratorVersion, v_version_id;
    ELSE
        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
            (raymigrator_version, repository_database_type, created_at)
        VALUES
            (@RayMigratorVersion, @RepositoryDatabaseType, NOW())
        RETURNING id INTO v_version_id;

        RAISE NOTICE '%,RayMigrator version [%] registered with new MigratorMetaId [%]', v_version_id, @RayMigratorVersion, v_version_id;
    END IF;
END $$;
