/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Delete"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Retention (ADR-027): deletes one batch of MigrationLog rows created before a cutoff, the oldest
first, and spares every row of a run of the repository that is still open (MigrationRun
FinishedAt IS NULL); a row without a run (MigrationRunId NULL) falls by its age. Runs against
the repository database under the schema and prefix of the repository, the default home of the
log, and only after DatabaseLogging_Count found the log table there. IRepositoryMaintenanceService
runs the template batch by batch until a batch deletes fewer rows than @BatchSize.
"""

Behaviour = """
- Return value N >= 0: N rows were deleted; fewer than @BatchSize means no matching row is left
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- One DELETE in autocommit (the repository DAL settings open no transaction): the database applies
  it whole or not at all, also when the call is cancelled
- Reads the index on CreatedAt of the log and the primary key of MigrationRun
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database file and prefix
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
BatchSize = "INT | REQUIRED | The most rows the batch deletes (at least 1)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,Deleted [N] MigrationLog rows created before [cutoff]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of deleted rows"
Note2 = "No commas allowed in messages"
Note3 = "The cutoff text compares with the ISO-8601 text of the column (datetime('now') format, DAL-021)"
Note4 = "Keep the batch one DELETE statement: the DAL checks the token only before a batch, Microsoft.Data.Sqlite cannot interrupt a statement"
Note5 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
Note6 = "Joins MigrationRun of the repository: the open runs of another repository that writes into the same log table are not protected"
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
 * One DELETE decides, and changes() follows it directly, so the result
 * reports this DELETE alone; a transaction around it would only hold the
 * write lock longer.
 */

-- The oldest rows first through the index on CreatedAt; a row of an open run stays whatever its age. The batch
-- selects its rowids: DELETE ... LIMIT needs a SQLite built with SQLITE_ENABLE_UPDATE_DELETE_LIMIT.
DELETE FROM "{CFG:TableBaseName}MigrationLog"
WHERE rowid IN (
    SELECT x.rowid
    FROM "{CFG:TableBaseName}MigrationLog" AS x
    WHERE x."CreatedAt" < @CutoffUtc
      AND NOT EXISTS (
          SELECT 1 FROM "{CFG:TableBaseName}MigrationRun" AS r
          WHERE r."Id" = x."MigrationRunId" AND r."FinishedAt" IS NULL)
    ORDER BY x."CreatedAt"
    LIMIT @BatchSize);

-- changes() reports the DELETE: no statement may change rows between the two.
SELECT CAST(changes() AS TEXT) || ',Deleted [' || CAST(changes() AS TEXT) || '] MigrationLog rows created before [' || @CutoffUtc || ']';
