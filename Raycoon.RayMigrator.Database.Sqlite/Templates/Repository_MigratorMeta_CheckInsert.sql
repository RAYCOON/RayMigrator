/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigratorMeta_CheckInsert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Registers the RayMigrator version in MigratorMeta if it is not registered yet and returns
the id of its row, which every MigrationRun of this version references as MigratorMetaId.
Runs after Repository_CheckCreate and the Repository_Upgrade_{n} steps (ADR-024).
"""

Behaviour = """
- Return value > 0: MigratorMetaId (logged at Debug level)
- Return value < 0: Error (logged at Error level, command aborted)
- One row per RayMigratorVersion and RepositoryDatabaseType
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
RayMigratorVersion     = "TEXT | REQUIRED | The RayMigrator application version (e.g., '0.17.0')"
RepositoryDatabaseType = "TEXT | REQUIRED | The database type for the repository (e.g., 'Sqlite')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N         = "N (MigratorMetaId),RayMigrator version [X] is registered with MigratorMetaId [N]"
Error_-12_Multi   = "-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [...] RepositoryDatabaseType [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use datetime('now') for all audit timestamps (UTC, matches the CHECK constraints)"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
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

INSERT INTO "{CFG:TableBaseName}MigratorMeta" ("RayMigratorVersion", "RepositoryDatabaseType", "CreatedAt")
SELECT @RayMigratorVersion, @RepositoryDatabaseType, datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM "{CFG:TableBaseName}MigratorMeta"
    WHERE "RayMigratorVersion" = @RayMigratorVersion
      AND "RepositoryDatabaseType" = @RepositoryDatabaseType
);

SELECT CASE
    WHEN (SELECT COUNT(*) FROM "{CFG:TableBaseName}MigratorMeta"
          WHERE "RayMigratorVersion" = @RayMigratorVersion AND "RepositoryDatabaseType" = @RepositoryDatabaseType) > 1
        THEN '-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [' || IFNULL(@RayMigratorVersion, 'NULL')
             || '] RepositoryDatabaseType [' || IFNULL(@RepositoryDatabaseType, 'NULL') || '].'
    ELSE (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}MigratorMeta"
          WHERE "RayMigratorVersion" = @RayMigratorVersion AND "RepositoryDatabaseType" = @RepositoryDatabaseType LIMIT 1)
         || ',RayMigrator version [' || @RayMigratorVersion || '] is registered with MigratorMetaId ['
         || (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}MigratorMeta"
             WHERE "RayMigratorVersion" = @RayMigratorVersion AND "RepositoryDatabaseType" = @RepositoryDatabaseType LIMIT 1) || ']'
END;
