/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Count"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Retention (ADR-027): reports whether the repository database has the log table under the schema
and prefix of the repository, and counts its rows created before a cutoff that do not belong to
an open run of the repository (MigrationRun FinishedAt IS NULL): the rows that
DatabaseLogging_Delete deletes. A log written elsewhere is missing here and left alone.
"""

Behaviour = """
- Second code 1: the log table exists; the first code N >= 0 counts its matching rows
- Second code 0: the log table does not exist under the schema and prefix of the repository (0,0)
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- Reads only; the count may exceed the int range of the result contract and is read as a long
- @ExistsOnly = 1 reports only whether the log table exists (0,1 or 0,0) and counts nothing: the cleanup
  asks it before it deletes, where a count of a large log would cost a scan; the dry run counts (0)
- SQLite resolves the tables of a statement when it prepares it, so the count of a missing table
  would fail before any condition is read: a missing log table is created empty for the count inside
  a transaction that is rolled back afterwards, which leaves nothing behind; an existing one is kept
  as it is (IF NOT EXISTS) and only read
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database file and prefix
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc  = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
ExistsOnly = "INTEGER | REQUIRED | 1 reports only whether the log table exists, without counting; 0 counts"

[ReturnValues]
# Format: SELECT 'code,code,message'
Success_N    = "N,1,[N] MigrationLog rows created before [cutoff] can be deleted"
Success_None = "0,0,MigrationLog does not exist in the repository database under the prefix of the repository"
Success_Exists = "0,1,MigrationLog exists in the repository database under the prefix of the repository (not counted)"

[ModificationNotes]
Note1 = "SELECT result format: 'rows,logTableExists,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "The condition must stay the one of DatabaseLogging_Delete"
Note4 = "The empty table of a missing log has only the columns the count reads; the ROLLBACK removes it, the DAL rolls back an open transaction of a failed script"
Note5 = "Uses temp table to capture whether the log table existed before the empty table was created"
Note6 = "The SELECT comes before the ROLLBACK and the cleanup: the DAL returns the scalar of the last result set with rows"
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
 * This template opens a transaction of its own and rolls it back (BEGIN
 * ... ROLLBACK), which also removes the empty log table it may create for
 * the count; SQLite does not nest transactions.
 */

CREATE TEMP TABLE IF NOT EXISTS "_rc_log_count" ("existed" INTEGER) STRICT;
DELETE FROM "_rc_log_count";
INSERT INTO "_rc_log_count" ("existed")
SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{CFG:TableBaseName}MigrationLog';

-- The log is cleaned only where it shares the repository database file and prefix; elsewhere the table is missing here.
-- The count needs a table to prepare against: a missing one is created empty and rolled back below.
BEGIN;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationLog" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "MigrationRunId"       INTEGER          NULL,
    "CreatedAt"            TEXT         NOT NULL
) STRICT;

-- With @ExistsOnly = 1 the constant condition is false before any row is read, so nothing is counted
SELECT CASE
    WHEN (SELECT "existed" FROM "_rc_log_count") = 0
        THEN '0,0,MigrationLog does not exist in the repository database under the prefix of the repository'
    WHEN @ExistsOnly = 1
        THEN '0,1,MigrationLog exists in the repository database under the prefix of the repository (not counted)'
    ELSE CAST(COUNT(*) AS TEXT) || ',1,[' || CAST(COUNT(*) AS TEXT) || '] MigrationLog rows created before [' || @CutoffUtc || '] can be deleted'
END
FROM "{CFG:TableBaseName}MigrationLog" AS x
WHERE @ExistsOnly = 0
  AND x."CreatedAt" < @CutoffUtc
  AND NOT EXISTS (
      SELECT 1 FROM "{CFG:TableBaseName}MigrationRun" AS r
      WHERE r."Id" = x."MigrationRunId" AND r."FinishedAt" IS NULL);

ROLLBACK;

DROP TABLE IF EXISTS "_rc_log_count";
