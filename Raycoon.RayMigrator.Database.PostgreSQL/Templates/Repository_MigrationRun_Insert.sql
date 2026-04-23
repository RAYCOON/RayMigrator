/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Insert"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new MigrationRun record to track a migration session.
Prevents parallel migrations for the same Product/Environment/RunMode.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Checks for existing unfinished MigrationRun before inserting
- Parallel migrations for same Product/Environment/RunMode are prevented
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
ProductId          = "INT | REQUIRED | Product ID from Product table (obtained via Repository_Product_CheckInsert)"
EnvironmentId      = "INT | REQUIRED | Environment ID from Environment table (obtained via Repository_Environment_CheckInsert)"
MigrationRunModeId = "SMALLINT | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigratorMetaId     = "INT | REQUIRED | Version ID from MigratorMeta table (obtained via Repository_CheckCreate)"
MigrationRunResultId  = "SMALLINT | REQUIRED | Initial result status (should be 10=Running)"
FromReleaseVersion = "VARCHAR(100) | OPTIONAL | Starting release version for the migration range"
ToReleaseVersion   = "VARCHAR(100) | OPTIONAL | Target release version for the migration range"
MigrationRunSettingsJson = "TEXT | REQUIRED | JSON snapshot of all RayMigrator settings at migration start"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created   = "N (MigrationRunId),MigrationRun with Id [N] successfully created for ProductId [M]"
Error_-2_Parallel = "-2,MigrationRun for Product [Name] with Id [N] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=100] are not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use NOW() for StartedAt timestamp (TIMESTAMPTZ column)"
Note4 = "Parallel migration check uses FinishedAt IS NULL to detect running migrations"
Note5 = "ResultCode convention: >= 0 = Success, -1 = General template error, -2 = Migration already active (parallel run)"
================================================================================
*/

DO $$
DECLARE
    v_migration_run_id INT;
    v_product_name VARCHAR(100);
BEGIN
    -- Advisory lock per product prevents race condition between check and insert
    PERFORM pg_advisory_xact_lock(hashtext('MigrationRun_' || CAST(@ProductId AS TEXT)));

    IF EXISTS (
        SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run
        WHERE
            product_id = @ProductId AND
            environment_id = @EnvironmentId AND
            migration_run_mode_id = @MigrationRunModeId AND
            finished_at IS NULL
        LIMIT 1
    ) THEN
        SELECT name INTO v_product_name FROM {CFG:SchemaName}.{CFG:TableBaseName}product WHERE id = @ProductId;

        RAISE NOTICE '-2,MigrationRun for Product [%] with Id [%] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=%] are not allowed!',
            COALESCE(v_product_name, 'NULL'), COALESCE(CAST(@ProductId AS VARCHAR(10)), 'NULL'), COALESCE(CAST(@MigrationRunModeId AS VARCHAR(10)), 'NULL');
    ELSE
        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run
            (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id, from_release_version, to_release_version, started_at)
        VALUES
            (@MigratorMetaId, @ProductId, @EnvironmentId, @MigrationRunModeId, @MigrationRunResultId, @FromReleaseVersion, @ToReleaseVersion, NOW())
        RETURNING id INTO v_migration_run_id;

        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run_meta (migration_run_id, migration_run_settings_json)
        VALUES (v_migration_run_id, @MigrationRunSettingsJson);

        RAISE NOTICE '%,MigrationRun with Id [%] successfully created for ProductId [%]', v_migration_run_id, v_migration_run_id, COALESCE(CAST(@ProductId AS VARCHAR(10)), 'NULL');
    END IF;
END $$;
