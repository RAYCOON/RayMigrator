/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_FixOrphaned"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Updates orphaned Migration entries (Pending/Executing status) for a given MigrationRun.
Sets MigrationStatusId based on user choice (Failed, NotMigrated or Migrated).
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no orphaned migrations found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing)
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId    = "INTEGER | REQUIRED | The MigrationRun ID whose orphaned migrations to fix"
MigrationStatusId = "INTEGER | REQUIRED | Target status: 30 (Failed), 50 (NotMigrated) or 100 (Migrated)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_None    = "0,No orphaned Migration entry found for MigrationRunId [N]"
Success_Updated = "N,Updated N orphaned Migration entry for MigrationRunId [M]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "code=0 means no orphaned entries found (not an error)"
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

UPDATE "{CFG:TableBaseName}MigrationRecord"
SET "MigrationStatusId" = @MigrationStatusId
WHERE "MigrationRunId" = @MigrationRunId
    AND "MigrationStatusId" IN (10, 20);

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '0,No orphaned Migration entry found for MigrationRunId [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || ']'
    ELSE CAST((SELECT "cnt" FROM "_rc_affected") AS TEXT) || ',Updated ' || CAST((SELECT "cnt" FROM "_rc_affected") AS TEXT) || ' orphaned Migration entry for MigrationRunId [' || IFNULL(CAST(@MigrationRunId AS TEXT), 'NULL') || ']'
END;

DROP TABLE IF EXISTS "_rc_affected";
