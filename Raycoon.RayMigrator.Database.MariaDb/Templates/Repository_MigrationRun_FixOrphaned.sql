/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Marks a single orphaned MigrationRun as Error with FinishedAt set and records why: the engine code 1006
(OrphanedRunClosed) and a message that names the last heartbeat.
Used by the Fix command and by the auto-fix of a later start to close a run whose process ended without finishing it.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned)
- Return value < 0: Error (record not found, not in Running state, or still alive: its last heartbeat is younger than @ThresholdMinutes)
- Updates MigrationRunResultId to 90 (Error) and sets FinishedAt
- Calculates DurationInMs from StartedAt to current UTC time
- Sets ErrorCode to 1006 (OrphanedRunClosed) and ErrorMessage to 'Closed as orphaned by the repair: the run ended without finishing; last heartbeat <yyyy-MM-dd HH:mm:ss> UTC'
  with LastHeartbeatAt or, for a run without a heartbeat, StartedAt; each keeps a value the row has already
- Only updates if MigrationRunResultId=10 (Running), FinishedAt IS NULL and the last heartbeat (LastHeartbeatAt, or StartedAt
  for a run without one) is at least @ThresholdMinutes old; 0 closes the run regardless of its heartbeat
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the orphaned MigrationRun to fix"
ThresholdMinutes = "INT | REQUIRED | Minimum age of the last heartbeat (or of the start without one) in minutes; 0 closes the run regardless"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Fixed     = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state or still alive"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note4 = "ErrorCode 1006 is TemplateResultCode.OrphanedRunClosed. The message is the same for the auto-fix and for fix: no parameter tells the template which of them closes the run"
Note5 = "DATE_FORMAT renders the last heartbeat in the session time zone, which the DAL sets to UTC (+00:00)"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_run
SET migration_run_result_id = 90,
    finished_at = CURRENT_TIMESTAMP,
    duration_in_ms = TIMESTAMPDIFF(MICROSECOND, started_at, CURRENT_TIMESTAMP) / 1000,
    error_code = COALESCE(error_code, '1006'),
    error_message = COALESCE(error_message, CONCAT('Closed as orphaned by the repair: the run ended without finishing; last heartbeat ',
        DATE_FORMAT(COALESCE(last_heartbeat_at, started_at), '%Y-%m-%d %H:%i:%s'), ' UTC'))
WHERE id = @MigrationRunId
    AND migration_run_result_id = 10
    AND finished_at IS NULL
    AND TIMESTAMPDIFF(MINUTE, COALESCE(last_heartbeat_at, started_at), CURRENT_TIMESTAMP) >= @ThresholdMinutes;

SET @v_affected = ROW_COUNT();

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('-31,MigrationRun [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), '] not found or not in Running state or still alive')
    ELSE CONCAT(CAST(@MigrationRunId AS CHAR), ',MigrationRun [', CAST(@MigrationRunId AS CHAR), '] marked as Error (orphaned run cleanup)')
END;
