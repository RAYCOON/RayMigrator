/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Insert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new MigrationRun record to track a migration session.
Uses inline EXISTS check to prevent parallel migrations (SQLite has file-level locking).
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- SQLite uses file-level locking instead of advisory locks (GET_LOCK)
- Checks for existing unfinished MigrationRun before inserting
- Parallel migrations for same Product/Environment/RunMode are prevented
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId          = "INTEGER | REQUIRED | Product ID from Product table"
EnvironmentId      = "INTEGER | REQUIRED | Environment ID from Environment table"
MigrationRunModeId = "INTEGER | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigratorMetaId     = "INTEGER | REQUIRED | Version ID from MigratorMeta table"
MigrationRunResultId  = "INTEGER | REQUIRED | Initial result status (should be 10=Running)"
FromReleaseVersion = "TEXT | OPTIONAL | Starting release version for the migration range"
ToReleaseVersion   = "TEXT | OPTIONAL | Target release version for the migration range"
MigrationRunSettingsJson = "TEXT | REQUIRED | JSON snapshot of all RayMigrator settings at migration start"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created    = "N (MigrationRunId),MigrationRun with Id [N] successfully created for ProductId [M]"
Error_-2_Parallel  = "-2,MigrationRun for Product [Name] with Id [N] is currently in progress..."
Error_-1_InsertFail = "-1,Failed to create MigrationRun for ProductId [N]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for StartedAt timestamp"
Note4 = "SQLite uses file-level locking - no GET_LOCK/RELEASE_LOCK needed"
Note5 = "Uses temp table _rc_state to store intermediate state since SQLite has no session variables"
Note6 = "ResultCode convention: >= 0 = Success, -1 = General template error, -2 = Migration already active (parallel run)"
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

-- Store intermediate state in temp table
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT);
DELETE FROM "_rc_state";

-- Check for existing running migration
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('product_name', (SELECT "Name" FROM "{CFG:TableBaseName}Product" WHERE "Id" = @ProductId)),
    ('running', CAST((SELECT COUNT(*) FROM "{CFG:TableBaseName}MigrationRun"
        WHERE "ProductId" = @ProductId AND "EnvironmentId" = @EnvironmentId
          AND "MigrationRunModeId" = @MigrationRunModeId AND "FinishedAt" IS NULL) AS TEXT));

-- Insert only if no running migration exists
INSERT INTO "{CFG:TableBaseName}MigrationRun"
    ("MigratorMetaId", "ProductId", "EnvironmentId", "MigrationRunModeId", "MigrationRunResultId",
     "FromReleaseVersion", "ToReleaseVersion", "StartedAt")
SELECT @MigratorMetaId, @ProductId, @EnvironmentId, @MigrationRunModeId, @MigrationRunResultId,
       @FromReleaseVersion, @ToReleaseVersion, datetime('now')
WHERE CAST((SELECT "val" FROM "_rc_state" WHERE "key"='running') AS INTEGER) = 0;

-- Capture insert state: new_id MUST be captured first in a separate INSERT.
-- SQLite evaluates multi-row VALUES sequentially — row1's INSERT modifies
-- last_insert_rowid(), so row2 would see _rc_state's rowid instead of MigrationRun's.
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('new_id', CAST(last_insert_rowid() AS TEXT));
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('inserted', CAST((SELECT COUNT(*) FROM "{CFG:TableBaseName}MigrationRun"
        WHERE "Id" = CAST((SELECT "val" FROM "_rc_state" WHERE "key"='new_id') AS INTEGER)) AS TEXT));

-- Insert MigrationRunMeta if the MigrationRun was created
INSERT INTO "{CFG:TableBaseName}MigrationRunMeta" ("MigrationRunId", "MigrationRunSettingsJson")
SELECT CAST((SELECT "val" FROM "_rc_state" WHERE "key"='new_id') AS INTEGER), @MigrationRunSettingsJson
WHERE CAST((SELECT "val" FROM "_rc_state" WHERE "key"='inserted') AS INTEGER) > 0;

-- Final result
SELECT CASE
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='running') AS INTEGER) > 0
        THEN '-2,MigrationRun for Product [' || IFNULL((SELECT "val" FROM "_rc_state" WHERE "key"='product_name'), 'NULL')
             || '] with Id [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL')
             || '] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate='
             || IFNULL(CAST(@MigrationRunModeId AS TEXT), 'NULL') || '] are not allowed!'
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='inserted') AS INTEGER) = 0
        THEN '-1,Failed to create MigrationRun for ProductId [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL') || ']'
    ELSE (SELECT "val" FROM "_rc_state" WHERE "key"='new_id')
         || ',MigrationRun with Id [' || (SELECT "val" FROM "_rc_state" WHERE "key"='new_id')
         || '] successfully created for ProductId [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL') || ']'
END;

DROP TABLE IF EXISTS "_rc_state";
