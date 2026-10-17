/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Update"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId       = "INTEGER | REQUIRED | The ID of the MigrationRun to update"
MigrationRunResultId = "INTEGER | REQUIRED | Final result: 10=Running, 90=Error, 100=Ok"
ErrorCode         = "VARCHAR(100) | OPTIONAL | Engine error code of the failure that decided the result; NULL for Ok"
ErrorMessage      = "TEXT | OPTIONAL | Message of that failure, cut to 4000 characters by the engine; NULL for Ok"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated    = "N (MigrationRunId),MigrationRun with Id [N] successfully updated with result [M]"
Error_-30_NotFound = "-30,MigrationRun with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for FinishedAt timestamp"
Note4 = "DurationInMs calculated via julianday difference * 86400000"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note6 = "Uses temp table to capture changes() since SQLite has no session variables"
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
SET
    "MigrationRunResultId" = @MigrationRunResultId,
    "ErrorCode" = @ErrorCode,
    "ErrorMessage" = @ErrorMessage,
    "FinishedAt" = datetime('now'),
    "DurationInMs" = CAST((julianday('now') - julianday("StartedAt")) * 86400000 AS INTEGER)
WHERE "Id" = @MigrationRunId;

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
-- DROP TABLE and CREATE TABLE are DDL and do NOT affect changes().
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '-30,MigrationRun with Id [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || '] does not exist'
    ELSE CAST(@MigrationRunId AS TEXT) || ',MigrationRun with Id [' || CAST(@MigrationRunId AS TEXT) || '] successfully updated with result [' || CAST(@MigrationRunResultId AS TEXT) || ']'
END;

DROP TABLE IF EXISTS "_rc_affected";
