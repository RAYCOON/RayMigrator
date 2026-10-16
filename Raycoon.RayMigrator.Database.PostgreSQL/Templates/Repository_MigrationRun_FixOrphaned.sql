/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the orphaned MigrationRun to fix"
ThresholdMinutes = "INT | REQUIRED | Minimum age of the last heartbeat (or of the start without one) in minutes; 0 closes the run regardless"

[ReturnValues]
# Format: RAISE NOTICE 'code,message'
Success_Fixed     = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state or still alive"

[ModificationNotes]
Note1 = "RAISE NOTICE format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note4 = "-31 is reported with RAISE NOTICE, not RAISE EXCEPTION: the engine reads it as the result code and leaves an alive run open; nothing was changed, so there is nothing to roll back"
Note5 = "ErrorCode 1006 is TemplateResultCode.OrphanedRunClosed. The message is the same for the auto-fix and for fix: no parameter tells the template which of them closes the run"
Note6 = "timezone('UTC', ...) renders the last heartbeat as UTC wall time for the message, whatever the session time zone; it does no arithmetic with NOW() (DAL-012)"
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
        duration_in_ms = EXTRACT(EPOCH FROM (NOW() - started_at)) * 1000,
        error_code = COALESCE(error_code, '1006'),
        error_message = COALESCE(error_message, 'Closed as orphaned by the repair: the run ended without finishing; last heartbeat '
            || to_char(timezone('UTC', COALESCE(last_heartbeat_at, started_at)), 'YYYY-MM-DD HH24:MI:SS') || ' UTC')
    WHERE id = @MigrationRunId
        AND migration_run_result_id = 10
        AND finished_at IS NULL
        AND COALESCE(last_heartbeat_at, started_at) <= NOW() - make_interval(mins => @ThresholdMinutes);

    GET DIAGNOSTICS v_affected = ROW_COUNT;

    IF v_affected = 0 THEN
        -- A NOTICE, not an EXCEPTION: an alive run is an answer the engine handles (it leaves the run alone), like
        -- the -2 of Repository_MigrationRun_Insert. An EXCEPTION would surface as a template error instead of -31.
        RAISE NOTICE '-31,MigrationRun [%] not found or not in Running state or still alive',
            COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    ELSE
        RAISE NOTICE '%,MigrationRun [%] marked as Error (orphaned run cleanup)',
            @MigrationRunId, @MigrationRunId;
    END IF;
END $$;
