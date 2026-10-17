/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Heartbeat"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Writes one heartbeat for a running MigrationRun: sets LastHeartbeatAt to now while the row
is still open (FinishedAt IS NULL). The engine calls it every Repository.HeartbeatIntervalInSeconds
from its own connection; the heartbeat age decides whether another process may treat the run
as orphaned (schema version 2).
"""

Behaviour = """
- Return value 1: the row was open and updated
- Return value 0: the run has already finished (not an error)
- Return value < 0: Error (logged at Warning level by the heartbeat loop, the run continues)
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the running MigrationRun"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1 = "1,Heartbeat of MigrationRun [N] written"
Success_0 = "0,MigrationRun [N] is not open"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use datetime('now') for the heartbeat (UTC, matches the CHECK constraint)"
Note4 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
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
SET "LastHeartbeatAt" = datetime('now')
WHERE "Id" = @MigrationRunId
    AND "FinishedAt" IS NULL;

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '0,MigrationRun [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || '] is not open'
    ELSE '1,Heartbeat of MigrationRun [' || CAST(@MigrationRunId AS TEXT) || '] written'
END;

DROP TABLE IF EXISTS "_rc_affected";
