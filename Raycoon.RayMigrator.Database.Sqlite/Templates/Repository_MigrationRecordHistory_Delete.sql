/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Delete"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Retention (ADR-027): deletes one batch of MigrationRecordHistory rows historized before a
cutoff, the oldest first, and spares every row of a run that is still open (MigrationRun
FinishedAt IS NULL): a running or orphaned run keeps its history until it is closed.
MigrationRecord, MigrationRun and MigrationRunMeta are never touched. IRepositoryMaintenanceService
runs the template batch by batch until a batch deletes fewer rows than @BatchSize.
"""

Behaviour = """
- Return value N >= 0: N rows were deleted; fewer than @BatchSize means no matching row is left
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- One DELETE in autocommit (the repository DAL settings open no transaction): the database applies
  it whole or not at all, also when the call is cancelled
- Reads the index on HistorizedAt of schema version 3 and the primary key of MigrationRun
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows historized before it match"
BatchSize = "INT | REQUIRED | The most rows the batch deletes (at least 1)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,Deleted [N] MigrationRecordHistory rows historized before [cutoff]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of deleted rows"
Note2 = "No commas allowed in messages"
Note3 = "The cutoff text compares with the ISO-8601 text of the column (datetime('now') format, DAL-021)"
Note4 = "Keep the batch one DELETE statement: the DAL checks the token only before a batch, Microsoft.Data.Sqlite cannot interrupt a statement"
Note5 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
================================================================================
*/

/*
 * Transaction requirement (DAL-024):
 * This template contains multiple statements that must execute atomically.
 * The DalSqlite driver wraps execution in a transaction when UseTransaction
 * is enabled (the default), so in-framework use is safe. Manual execution
 * via the sqlite3 CLI (e.g., "sqlite3 db.sqlite < file.sql") must be
 * wrapped in "BEGIN TRANSACTION; ... COMMIT;" to guarantee atomicity.
 *
 * The retention runs this template without a transaction of the driver (the
 * repository settings turn UseTransaction off): the DELETE commits on its
 * own, and changes() follows it directly, so the result reports this DELETE
 * alone. A wrapping transaction would only hold the write lock longer.
 */

-- The oldest rows first through the index on HistorizedAt; a row of an open run stays whatever its age. The batch
-- selects its rowids: DELETE ... LIMIT needs a SQLite built with SQLITE_ENABLE_UPDATE_DELETE_LIMIT.
DELETE FROM "{CFG:TableBaseName}MigrationRecordHistory"
WHERE rowid IN (
    SELECT x.rowid
    FROM "{CFG:TableBaseName}MigrationRecordHistory" AS x
    WHERE x."HistorizedAt" < @CutoffUtc
      AND NOT EXISTS (
          SELECT 1 FROM "{CFG:TableBaseName}MigrationRun" AS r
          WHERE r."Id" = x."MigrationRunId" AND r."FinishedAt" IS NULL)
    ORDER BY x."HistorizedAt"
    LIMIT @BatchSize);

-- changes() reports the DELETE: no statement may change rows between the two.
SELECT CAST(changes() AS TEXT) || ',Deleted [' || CAST(changes() AS TEXT) || '] MigrationRecordHistory rows historized before [' || @CutoffUtc || ']';
