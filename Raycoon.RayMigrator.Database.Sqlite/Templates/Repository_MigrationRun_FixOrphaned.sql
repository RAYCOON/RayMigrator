/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.2"

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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId = "INTEGER | REQUIRED | The ID of the orphaned MigrationRun to fix"
ThresholdMinutes = "INTEGER | REQUIRED | Minimum age of the last heartbeat (or of the start without one) in minutes; 0 closes the run regardless"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Fixed          = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state or still alive"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note4 = "Uses temp table to capture changes() since SQLite has no session variables"
Note5 = "ErrorCode 1006 is TemplateResultCode.OrphanedRunClosed. The message is the same for the auto-fix and for fix: no parameter tells the template which of them closes the run"
================================================================================
*/

/*
 * Transaction requirement (DAL-024):
 * The engine runs this template without a transaction of the driver (the
 * repository, the database log writer and the retention set UseTransaction
 * to false), so each statement commits on its own unless the template
 * opens a transaction itself. Statements that share an invariant must open
 * BEGIN IMMEDIATE ... COMMIT or leave the decision to one statement. The
 * sqlite3 CLI needs the same: run the file as it is, without a transaction
 * around it ("sqlite3 db.sqlite < file.sql").
 *
 * One statement writes; the statements around it only report its result.
 */

UPDATE "{CFG:TableBaseName}MigrationRun"
SET "MigrationRunResultId" = 90,
    "FinishedAt" = datetime('now'),
    "DurationInMs" = CAST((julianday('now') - julianday("StartedAt")) * 86400000 AS INTEGER),
    "ErrorCode" = COALESCE("ErrorCode", '1006'),
    "ErrorMessage" = COALESCE("ErrorMessage", 'Closed as orphaned by the repair: the run ended without finishing; last heartbeat '
        || strftime('%Y-%m-%d %H:%M:%S', COALESCE("LastHeartbeatAt", "StartedAt")) || ' UTC')
WHERE "Id" = @MigrationRunId
    AND "MigrationRunResultId" = 10
    AND "FinishedAt" IS NULL
    AND (julianday('now') - julianday(COALESCE("LastHeartbeatAt", "StartedAt"))) * 1440 >= @ThresholdMinutes;

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '-31,MigrationRun [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || '] not found or not in Running state or still alive'
    ELSE CAST(@MigrationRunId AS TEXT) || ',MigrationRun [' || CAST(@MigrationRunId AS TEXT) || '] marked as Error (orphaned run cleanup)'
END;

DROP TABLE IF EXISTS "_rc_affected";
