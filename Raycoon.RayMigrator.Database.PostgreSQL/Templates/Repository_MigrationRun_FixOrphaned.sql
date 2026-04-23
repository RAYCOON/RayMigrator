/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Marks a single orphaned MigrationRun as Error with FinishedAt set.
Used by the Fix command to clean up crashed migration processes.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned)
- Return value < 0: Error (record not found or not in Running state)
- Updates MigrationRunResultId to 90 (Error) and sets FinishedAt
- Calculates DurationInMs from StartedAt to current UTC time
- Only updates if MigrationRunResultId=10 (Running) and FinishedAt IS NULL
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the orphaned MigrationRun to fix"

[ReturnValues]
# Format: RAISE NOTICE 'code,message'
Success_Fixed     = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state"

[ModificationNotes]
Note1 = "RAISE NOTICE format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

DO $$
DECLARE
    v_affected INT;
BEGIN
    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_run
    SET
        migration_run_result_id = 90,
        finished_at = NOW(),
        duration_in_ms = EXTRACT(EPOCH FROM (NOW() - started_at)) * 1000
    WHERE id = @MigrationRunId
        AND migration_run_result_id = 10
        AND finished_at IS NULL;

    GET DIAGNOSTICS v_affected = ROW_COUNT;

    IF v_affected = 0 THEN
        RAISE EXCEPTION '-31,MigrationRun [%] not found or not in Running state',
            COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    ELSE
        RAISE NOTICE '%,MigrationRun [%] marked as Error (orphaned run cleanup)',
            @MigrationRunId, @MigrationRunId;
    END IF;
END $$;
