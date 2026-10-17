/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist, and the indexes the
readers of the log filter by (rows per run, rows per time) when they are missing.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists (also when only the indexes were added)
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Inserts master data for MigrationEvent types when it creates the log tables, and keeps the rows a parallel
  first start inserted meanwhile; two first starts at once may both report 1
- Creates the indexes on MigrationLog (MigrationRunId) and (CreatedAt) when missing, also on a log
  table an older engine created
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'log_')"

[Parameters]
# No SQL parameters required for this template

[ReturnValues]
# Format: SELECT 'code,message'
Success_0_Exists  = "0,Database logging infrastructure already exists"
Success_1_Created = "1,Database logging infrastructure successfully created"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: MigrationEvent (lookup), MigrationLog (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "MigrationLog.CreatedAt defaults to datetime('now')"
Note6 = "Uses idempotent CREATE TABLE IF NOT EXISTS; the MigrationEvent catalogue is inserted only when the log tables are created in this run (gated on _rc_log_check.existed = 0), with INSERT OR IGNORE, which keeps a row that exists"
Note7 = "Uses temp table to capture pre-DDL state since SQLite has no session variables"
Note8 = "DAL-021: MigrationLog.CreatedAt carries a strict-ISO-8601 CHECK constraint"
Note9 = "The indexes stand outside the creation gate: CREATE INDEX IF NOT EXISTS runs on every start and adds them to a log table an older engine created"
Note10 = "A parallel start of another host is safe without a lock: SQLite serialises the DDL through its write lock and IF NOT EXISTS makes the second statement a no-op; the statements run one by one (the log writer uses no transaction), so two first starts can both find no log table and both insert the catalogue, and the second insert keeps the rows of the first"
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
 * Two first starts tolerate each other without a lock: the tables are
 * created IF NOT EXISTS and the catalogue is inserted OR IGNORE.
 */

CREATE TEMP TABLE IF NOT EXISTS "_rc_log_check" ("existed" INTEGER) STRICT;
DELETE FROM "_rc_log_check";
INSERT INTO "_rc_log_check" ("existed")
SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{CFG:TableBaseName}MigrationLog';

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationEvent" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationLog" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "LogLevelId"           INTEGER      NOT NULL,
    "MigrationEventId"     INTEGER          NULL,
    "RunModeId"            INTEGER          NULL,
    "ProductId"            INTEGER          NULL,
    "EnvironmentId"        INTEGER          NULL,
    "MigrationRunId"       INTEGER          NULL,
    "MigrationRecordId"          INTEGER          NULL,
    "ReleaseVersion"       TEXT             NULL,
    "TargetGroupAlias"     TEXT             NULL,
    "TargetAlias"          TEXT             NULL,
    "Filename"             TEXT             NULL,
    "FileOrderId"          INTEGER          NULL,
    "FileBlockId"          INTEGER          NULL,
    "Message"              TEXT             NULL,
    "CreatedAt"            TEXT         NOT NULL DEFAULT (datetime('now')) CHECK (datetime("CreatedAt") IS NOT NULL AND datetime("CreatedAt") = "CreatedAt")
) STRICT;

-- A parallel first start may have inserted the catalogue since the check for the log table: keep its rows.
INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationEvent" ("Id", "Name", "Description")
SELECT "column1", "column2", "column3" FROM (VALUES
    (0, 'UnspecifiedEvent', ''),
    (10, 'CommandLineParsing', ''),
    (20, 'EnvironmentVariableReplacement', ''),
    (31, 'CreateDatabaseLogger', ''),
    (40, 'ValidateRayMigratorOptions', ''),
    (50, 'CreateApplicationHost', ''),
    (60, 'InitializeDalSpecificProperties', ''),
    (70, 'ValidateConnectionStrings', ''),
    (80, 'RayMigratorServiceStart', ''),
    (100, 'TemplateExecutionRepositoryCheckCreate', ''),
    (101, 'TemplateExecutionRepositoryUpgrade', ''),
    (102, 'TemplateExecutionRepositoryMigratorMetaCheckInsert', ''),
    (110, 'TemplateExecutionRepositoryMigrationRunInsert', ''),
    (111, 'TemplateExecutionRepositoryMigrationRunUpdate', ''),
    (112, 'TemplateExecutionRepositoryMigrationRunSelectOrphaned', ''),
    (113, 'TemplateExecutionRepositoryMigrationRunFixOrphaned', ''),
    (114, 'TemplateExecutionRepositoryMigrationFixOrphaned', ''),
    (115, 'TemplateExecutionRepositoryMigrationRunHeartbeat', ''),
    (120, 'TemplateExecutionRepositoryProductCheckInsert', ''),
    (121, 'TemplateExecutionRepositoryEnvironmentCheckInsert', ''),
    (122, 'TemplateExecutionRepositoryProductSelect', ''),
    (123, 'TemplateExecutionRepositoryEnvironmentSelect', ''),
    (130, 'TemplateExecutionRepositoryMigrationInsert', ''),
    (131, 'TemplateExecutionRepositoryMigrationUpdate', ''),
    (132, 'TemplateExecutionRepositoryMigrationGetInterrupted', ''),
    (133, 'TemplateExecutionRepositoryMigrationUpdateRollback', ''),
    (134, 'TemplateExecutionRepositoryMigrationSelect', ''),
    (135, 'TemplateExecutionRepositoryMigrationUpdateHash', ''),
    (136, 'TemplateExecutionRepositoryMigrationRunSelect', ''),
    (137, 'TemplateExecutionRepositoryMigrationRecordHistorySelect', ''),
    (1000, 'RayMigratorServiceShutdown', '')
)
WHERE (SELECT "existed" FROM "_rc_log_check") = 0;

-- Outside the creation gate, so that a log table an older engine created gets the indexes at its next start.
CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationLog_MigrationRunId" ON "{CFG:TableBaseName}MigrationLog" ("MigrationRunId");
CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationLog_CreatedAt" ON "{CFG:TableBaseName}MigrationLog" ("CreatedAt");

SELECT CASE WHEN (SELECT "existed" FROM "_rc_log_check") > 0
    THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;

DROP TABLE IF EXISTS "_rc_log_check";
