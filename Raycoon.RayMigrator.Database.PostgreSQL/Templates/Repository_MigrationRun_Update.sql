/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Update"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-02-03.1"

[Description]
Function = """
Updates an existing MigrationRun with the final result status and completion time.
Calculates duration in milliseconds from StartedAt to FinishedAt.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Sets FinishedAt timestamp and calculates DurationInMs
- Called at the end of a migration run regardless of success/failure
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The ID of the MigrationRun to update"
MigrationRunResultId = "SMALLINT | REQUIRED | Final result: 10=Running, 90=Error, 100=Ok"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRunId),MigrationRun with Id [N] successfully updated with result [M]"
Error_-30_NotFound = "-30,MigrationRun with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use NOW() for FinishedAt timestamp (TIMESTAMPTZ column)"
Note4 = "DurationInMs calculated as EXTRACT(EPOCH FROM (FinishedAt - StartedAt)) * 1000"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

DO $$
DECLARE
    v_exists INT;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run
    WHERE id = @MigrationRunId;

    IF v_exists = 0 THEN
        RAISE EXCEPTION '-30,MigrationRun with Id [%] does not exist', COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    END IF;

    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_run
    SET
        migration_run_result_id = @MigrationRunResultId,
        finished_at = NOW(),
        duration_in_ms = EXTRACT(EPOCH FROM (NOW() - started_at)) * 1000
    WHERE id = @MigrationRunId;

    RAISE NOTICE '%,MigrationRun with Id [%] successfully updated with result [%]',
        @MigrationRunId, @MigrationRunId, @MigrationRunResultId;
END $$;
