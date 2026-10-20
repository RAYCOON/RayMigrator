/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_FixOrphaned"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.2"

[Description]
Function = """
Settles the open MigrationRecord entries (Pending or Executing) of an orphaned MigrationRun, which a run that ended
abruptly left behind (a crash, a kill, a second Ctrl+C or SIGTERM). Repository_MigrationRun_FixOrphaned closes the run
before this template runs.
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no open entry found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing) of the given MigrationRun
- MigrationStatusId = 0 decides per entry from its progress; the first matching rule wins:
  a rollback was running (FileDownHash set) and FileDownBlocksMigrated >= FileDownBlocksTotal > 0: 50 (NotMigrated), the rollback had completed
  a rollback was running (FileDownHash set) otherwise: 30 (Failed), migrate-down continues a rollback with progress
  FileUpBlocksMigrated > 0 and >= FileUpBlocksTotal: 100 (Migrated), every block had committed
  FileUpBlocksMigrated > 0: 30 (Failed), the next migrate-up resumes after the committed blocks
  otherwise: 50 (NotMigrated)
- MigrationStatusId = 30, 50 or 100 sets that status on every open entry
- Every entry it updates gets FinishedAt, the time of the repair, where FinishedAt is NULL, and keeps DurationInMs:
  a file that was running has neither and gets the time of the repair without a duration; a record whose rollback
  was running keeps the end and the duration of its last terminal status
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId    = "INTEGER | REQUIRED | The MigrationRun ID whose open entries to settle"
MigrationStatusId = "INTEGER | REQUIRED | 0 = decide per entry from its progress (rule in Behaviour); 30 (Failed), 50 (NotMigrated) or 100 (Migrated) = set on every open entry"

[ReturnValues]
# Format: SELECT 'code,message'
Success_None    = "0,No orphaned Migration entry found for MigrationRunId [N]"
Success_Updated = "N,Updated N orphaned Migration entry for MigrationRunId [M]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "code=0 means no orphaned entries found (not an error)"
Note4 = "Uses temp table to capture changes() since SQLite has no session variables"
Note5 = "The block counters stay as they are; FinishedAt gets the time of the repair (datetime('now')) where it is NULL (a file that was running); DurationInMs stays as it is (NULL for a file that was running; the duration of the last terminal status for a record whose rollback was running), because the time until the repair is no execution time; no history row is written"
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

UPDATE "{CFG:TableBaseName}MigrationRecord"
SET
    "MigrationStatusId" = CASE
        WHEN @MigrationStatusId <> 0 THEN @MigrationStatusId
        WHEN "FileDownHash" IS NOT NULL THEN
            CASE
                WHEN COALESCE("FileDownBlocksTotal", 0) > 0
                     AND COALESCE("FileDownBlocksMigrated", 0) >= "FileDownBlocksTotal" THEN 50
                ELSE 30
            END
        WHEN "FileUpBlocksMigrated" > 0 AND "FileUpBlocksMigrated" >= "FileUpBlocksTotal" THEN 100
        WHEN "FileUpBlocksMigrated" > 0 THEN 30
        ELSE 50
    END,
    "FinishedAt" = COALESCE("FinishedAt", datetime('now'))
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
