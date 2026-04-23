/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Update"
DatabaseType   = "MariaDb"
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
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The ID of the MigrationRun to update"
MigrationRunResultId = "TINYINT UNSIGNED | REQUIRED | Final result: 10=Running, 90=Error, 100=Ok"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRunId),MigrationRun with Id [N] successfully updated with result [M]"
Error_-30_NotFound = "-30,MigrationRun with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for FinishedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "DurationInMs calculated as TIMESTAMPDIFF(MICROSECOND, StartedAt, CURRENT_TIMESTAMP) / 1000"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_run
SET
    migration_run_result_id = @MigrationRunResultId,
    finished_at = CURRENT_TIMESTAMP,
    duration_in_ms = TIMESTAMPDIFF(MICROSECOND, started_at, CURRENT_TIMESTAMP) / 1000
WHERE id = @MigrationRunId;

SET @v_affected = ROW_COUNT();

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('-30,MigrationRun with Id [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), '] does not exist')
    ELSE CONCAT(CAST(@MigrationRunId AS CHAR), ',MigrationRun with Id [', CAST(@MigrationRunId AS CHAR), '] successfully updated with result [', CAST(@MigrationRunResultId AS CHAR), ']')
END;
