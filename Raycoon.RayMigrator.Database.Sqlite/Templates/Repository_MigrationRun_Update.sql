/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Update"
DatabaseType   = "Sqlite"
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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId       = "INTEGER | REQUIRED | The ID of the MigrationRun to update"
MigrationRunResultId = "INTEGER | REQUIRED | Final result: 10=Running, 90=Error, 100=Ok"

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
 * This template contains multiple statements that must execute atomically.
 * The DalSqlite driver wraps execution in a transaction when UseTransaction
 * is enabled (the default), so in-framework use is safe. Manual execution
 * via the sqlite3 CLI (e.g., "sqlite3 db.sqlite < file.sql") must be
 * wrapped in "BEGIN TRANSACTION; ... COMMIT;" to guarantee atomicity.
 */

UPDATE "{CFG:TableBaseName}MigrationRun"
SET
    "MigrationRunResultId" = @MigrationRunResultId,
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
