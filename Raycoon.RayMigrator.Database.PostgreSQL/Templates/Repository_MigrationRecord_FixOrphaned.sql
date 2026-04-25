/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_FixOrphaned"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Updates orphaned Migration entries (Pending/Executing status) for a given MigrationRun.
Sets MigrationStatusId based on user choice (Failed or Migrated).
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no orphaned migrations found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing)
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The MigrationRun ID whose orphaned migrations to fix"
MigrationStatusId = "SMALLINT | REQUIRED | Target status: 30 (Failed) or 100 (Migrated)"

[ReturnValues]
# Format: RAISE NOTICE 'code,message'
Success_None    = "0,No orphaned Migration entry found for MigrationRunId [N]"
Success_Updated = "N,Updated N orphaned Migration entry for MigrationRunId [M]"

[ModificationNotes]
Note1 = "RAISE NOTICE format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "code=0 means no orphaned entries found (not an error)"
================================================================================
*/

DO $$
DECLARE
    v_affected INT;
BEGIN
    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_record
    SET
        migration_status_id = @MigrationStatusId
    WHERE migration_run_id = @MigrationRunId
        AND migration_status_id IN (10, 20);

    GET DIAGNOSTICS v_affected = ROW_COUNT;

    IF v_affected = 0 THEN
        RAISE NOTICE '0,No orphaned Migration entry found for MigrationRunId [%]',
            COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    ELSE
        RAISE NOTICE '%,Updated % orphaned Migration entry for MigrationRunId [%]',
            v_affected, v_affected, COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    END IF;
END $$;
