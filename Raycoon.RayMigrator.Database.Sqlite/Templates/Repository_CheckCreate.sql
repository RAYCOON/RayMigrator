/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Reports the schema version of the repository (ADR-024). Creates the RepositorySchemaVersion
table when it does not exist; creates nothing else. The engine applies the missing
Repository_Upgrade_{n} steps afterwards.
"""

Behaviour = """
- First code: highest applied schema version, 0 when the repository has none
- Second code: 1 when the MigratorMeta table exists, 0 otherwise. With version 0 this
  means a repository of a RayMigrator before 0.17.0, which the engine refuses
- Idempotent (CREATE TABLE IF NOT EXISTS); a parallel start waits in the busy timeout
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# none

[ReturnValues]
# Format: SELECT 'schemaVersion,unversionedTables,message'
Success_N        = "N (schema version),0|1,RayMigrator repository has schema version [N]"
Success_Legacy   = "0,1,RayMigrator repository tables exist without a schema version"
Success_None     = "0,0,RayMigrator repository does not exist yet"

[ModificationNotes]
Note1 = "SELECT result format: 'schemaVersion,unversionedTables,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Creates only the RepositorySchemaVersion table; the repository tables come from Repository_Upgrade_1"
Note4 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
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

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}RepositorySchemaVersion" (
    "Version"              INTEGER      NOT NULL PRIMARY KEY,
    "AppliedAt"            TEXT         NOT NULL CHECK (datetime("AppliedAt") IS NOT NULL AND datetime("AppliedAt") = "AppliedAt"),
    "RayMigratorVersion"   TEXT         NOT NULL
) STRICT;

SELECT CAST(IFNULL((SELECT MAX("Version") FROM "{CFG:TableBaseName}RepositorySchemaVersion"), 0) AS TEXT)
    || ',' || CAST((SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{CFG:TableBaseName}MigratorMeta') AS TEXT)
    || ',' || CASE
        WHEN IFNULL((SELECT MAX("Version") FROM "{CFG:TableBaseName}RepositorySchemaVersion"), 0) > 0
            THEN 'RayMigrator repository has schema version [' || CAST((SELECT MAX("Version") FROM "{CFG:TableBaseName}RepositorySchemaVersion") AS TEXT) || ']'
        WHEN (SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{CFG:TableBaseName}MigratorMeta') = 1
            THEN 'RayMigrator repository tables exist without a schema version'
        ELSE 'RayMigrator repository does not exist yet'
    END;
