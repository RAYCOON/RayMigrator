/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "Sqlite"
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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId = "INTEGER | REQUIRED | The ID of the orphaned MigrationRun to fix"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Fixed          = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note4 = "Uses temp table to capture changes() since SQLite has no session variables"
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
SET "MigrationRunResultId" = 90,
    "FinishedAt" = datetime('now'),
    "DurationInMs" = CAST((julianday('now') - julianday("StartedAt")) * 86400000 AS INTEGER)
WHERE "Id" = @MigrationRunId
    AND "MigrationRunResultId" = 10
    AND "FinishedAt" IS NULL;

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '-31,MigrationRun [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || '] not found or not in Running state'
    ELSE CAST(@MigrationRunId AS TEXT) || ',MigrationRun [' || CAST(@MigrationRunId AS TEXT) || '] marked as Error (orphaned run cleanup)'
END;

DROP TABLE IF EXISTS "_rc_affected";
