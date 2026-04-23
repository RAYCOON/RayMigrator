/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Inserts master data for MigrationEvent types
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'Log_')"

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
Note6 = "Uses idempotent CREATE TABLE IF NOT EXISTS and INSERT OR IGNORE"
Note7 = "Uses temp table to capture pre-DDL state since SQLite has no session variables"
Note8 = "DAL-021: MigrationLog.CreatedAt carries a strict-ISO-8601 CHECK constraint"
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
    "MigrationId"          INTEGER          NULL,
    "ReleaseVersion"       TEXT             NULL,
    "TargetGroupAlias"     TEXT             NULL,
    "TargetAlias"          TEXT             NULL,
    "Filename"             TEXT             NULL,
    "FileOrderId"          INTEGER          NULL,
    "FileBlockId"          INTEGER          NULL,
    "Message"              TEXT             NULL,
    "CreatedAt"            TEXT         NOT NULL DEFAULT (datetime('now')) CHECK (datetime("CreatedAt") IS NOT NULL AND datetime("CreatedAt") = "CreatedAt")
) STRICT;

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationEvent" ("Id", "Name", "Description")
VALUES
    (0, 'UnspecifiedEvent', ''),
    (10, 'CommandLineParsing', ''),
    (20, 'EnvironmentVariableReplacement', ''),
    (31, 'CreateDatabaseLogger', ''),
    (32, 'CreateCompositeLogger', ''),
    (40, 'ValidateRayMigratorOptions', ''),
    (50, 'CreateApplicationHost', ''),
    (60, 'InitializeDalSpecificProperties', ''),
    (70, 'ValidateConnectionStrings', ''),
    (80, 'RayMigratorServiceStart', ''),
    (100, 'CreateAndStartRayMigratorService', ''),
    (1000, 'RayMigratorServiceShutdown', '');

SELECT CASE WHEN (SELECT "existed" FROM "_rc_log_check") > 0
    THEN '0,Database logging infrastructure already exists'
    ELSE '1,Database logging infrastructure successfully created'
END;

DROP TABLE IF EXISTS "_rc_log_check";
