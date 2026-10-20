/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigratorMeta_CheckInsert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

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
 * The engine runs this template without a transaction of the driver (the
 * repository, the database log writer and the retention set UseTransaction
 * to false), so each statement commits on its own unless the template
 * opens a transaction itself. Statements that share an invariant must open
 * BEGIN IMMEDIATE ... COMMIT or leave the decision to one statement. The
 * sqlite3 CLI needs the same: run the file as it is, without a transaction
 * around it ("sqlite3 db.sqlite < file.sql").
 *
 * One statement decides: the INSERT ... WHERE NOT EXISTS reads
 * MigratorMeta under the write lock of its own statement, so two first
 * starts of a version insert one row.
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
