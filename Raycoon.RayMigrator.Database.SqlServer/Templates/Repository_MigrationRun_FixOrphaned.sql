-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "SqlServer"
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
# Format: SELECT 'code,message'
Success_Fixed     = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state or still alive"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note4 = "ErrorCode 1006 is TemplateResultCode.OrphanedRunClosed. The message is the same for the auto-fix and for fix: no parameter tells the template which of them closes the run"
================================================================================
*/

SET NOCOUNT ON;

UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
SET
    [MigrationRunResultId] = 90,
    [FinishedAt] = SYSUTCDATETIME(),
    [DurationInMs] = DATEDIFF_BIG(MILLISECOND, [StartedAt], SYSUTCDATETIME()),
    [ErrorCode] = COALESCE([ErrorCode], '1006'),
    [ErrorMessage] = COALESCE([ErrorMessage], 'Closed as orphaned by the repair: the run ended without finishing; last heartbeat '
        + CONVERT(VARCHAR(19), COALESCE([LastHeartbeatAt], [StartedAt]), 120) + ' UTC')
WHERE [Id] = @MigrationRunId
    AND [MigrationRunResultId] = 10
    AND [FinishedAt] IS NULL
    AND DATEDIFF_BIG(SECOND, COALESCE([LastHeartbeatAt], [StartedAt]), SYSUTCDATETIME()) >= CAST(@ThresholdMinutes AS BIGINT) * 60;

IF @@ROWCOUNT = 0
    SELECT '-31,MigrationRun [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + '] not found or not in Running state or still alive';
ELSE
    SELECT CAST(@MigrationRunId AS VARCHAR(10)) + ',MigrationRun [' + CAST(@MigrationRunId AS VARCHAR(10)) + '] marked as Error (orphaned run cleanup)';
