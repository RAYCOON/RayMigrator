/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Insert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.2"

[Description]
Function = """
Creates a new MigrationRun record to track a migration session.
Checks for an open run of the product and environment and inserts the run and its MigrationRunMeta
row in one BEGIN IMMEDIATE transaction, so that a parallel start waits and then finds the open run.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- BEGIN IMMEDIATE takes the write lock of the file before the check reads MigrationRun: a parallel start
  waits for it in the busy timeout (the command timeout of the repository) and then finds the open run
- Checks for existing unfinished MigrationRun before inserting
- Parallel migrations for same Product/Environment are prevented
- Writes the MigrationRunMeta row only for the run it inserted; a refused insert writes nothing
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId          = "INTEGER | REQUIRED | Product ID from Product table"
EnvironmentId      = "INTEGER | REQUIRED | Environment ID from Environment table"
MigrationRunModeId = "INTEGER | REQUIRED | Run mode of the command (always 100=Migrate: Validate and Simulate runs never write rows, #19)"
MigratorMetaId     = "INTEGER | REQUIRED | Version ID from MigratorMeta table"
MigrationRunResultId  = "INTEGER | REQUIRED | Initial result status (should be 10=Running)"
FromReleaseVersion = "TEXT | OPTIONAL | Starting release version for the migration range"
ToReleaseVersion   = "TEXT | OPTIONAL | Target release version for the migration range"
MigrationRunSettingsJson = "TEXT | REQUIRED | JSON snapshot of all RayMigrator settings at migration start"
MigrationOperationId = "TINYINT | REQUIRED | What the run does: 50=MigrateDown, 100=MigrateUp, 110=Baseline (schema version 2)"
MigrationRunSourceId = "TINYINT | OPTIONAL | Where the run was started: 10=Cli, 20=Ci, 30=Studio; NULL when the host did not say"
InitiatedBy        = "VARCHAR(200) | OPTIONAL | Display name of the person or service account that started the run"
InitiatedBySubject = "VARCHAR(200) | OPTIONAL | Stable identifier of the initiator (e.g. the subject claim of an identity provider)"
Host               = "VARCHAR(200) | OPTIONAL | The machine the run executes on"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created    = "N (MigrationRunId),MigrationRun with Id [N] successfully created for ProductId [M]"
Error_-2_Parallel  = "-2,MigrationRun for Product [Name] with Id [N] is currently in progress..."
Error_-1_InsertFail = "-1,Failed to create MigrationRun for ProductId [N]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for StartedAt timestamp"
Note4 = "The check, the INSERT and the MigrationRunMeta row run in one BEGIN IMMEDIATE transaction: the repository DAL settings open none, and in statements that commit one by one two starts at the same moment both pass the check. Keep the COMMIT before the final SELECT; the DAL rolls the transaction back when a statement before it fails"
Note5 = "Uses temp table _rc_state to store intermediate state since SQLite has no session variables"
Note6 = "ResultCode convention: >= 0 = Success, -1 = General template error, -2 = Migration already active (parallel run)"
Note7 = "new_id takes last_insert_rowid() only when changes() > 0 in the statement right after the MigrationRun INSERT: a gated INSERT that inserts nothing leaves last_insert_rowid() at the last row inserted before on the connection (a row of _rc_state), so new_id stays NULL then"
Note8 = "The INSERT repeats the check against MigrationRun itself (NOT EXISTS), so the statement that writes the run never adds a second open run, whatever the state table says"
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
 * This template opens BEGIN IMMEDIATE ... COMMIT around the check for an
 * open run, the INSERT and the MigrationRunMeta row: a parallel start waits
 * for the write lock and then finds the open run.
 */

-- Store intermediate state in temp table
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT);
DELETE FROM "_rc_state";

-- The repository DAL settings run templates without a transaction, so the run lock opens its own: BEGIN IMMEDIATE
-- takes the write lock before the check reads MigrationRun, and a parallel start waits here until this start has
-- committed its run, which it then finds.
BEGIN IMMEDIATE;

-- Check for existing running migration
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('product_name', (SELECT "Name" FROM "{CFG:TableBaseName}Product" WHERE "Id" = @ProductId)),
    ('running', CAST((SELECT COUNT(*) FROM "{CFG:TableBaseName}MigrationRun"
        WHERE "ProductId" = @ProductId AND "EnvironmentId" = @EnvironmentId
          AND "FinishedAt" IS NULL) AS TEXT));

-- Insert only if no running migration exists
INSERT INTO "{CFG:TableBaseName}MigrationRun"
    ("MigratorMetaId", "ProductId", "EnvironmentId", "MigrationRunModeId", "MigrationRunResultId",
     "FromReleaseVersion", "ToReleaseVersion", "StartedAt",
     "MigrationOperationId", "MigrationRunSourceId", "InitiatedBy", "InitiatedBySubject", "Host", "LastHeartbeatAt")
SELECT @MigratorMetaId, @ProductId, @EnvironmentId, @MigrationRunModeId, @MigrationRunResultId,
       @FromReleaseVersion, @ToReleaseVersion, datetime('now'),
       @MigrationOperationId, @MigrationRunSourceId, @InitiatedBy, @InitiatedBySubject, @Host, datetime('now')
WHERE CAST((SELECT "val" FROM "_rc_state" WHERE "key"='running') AS INTEGER) = 0
  AND NOT EXISTS (SELECT 1 FROM "{CFG:TableBaseName}MigrationRun"
      WHERE "ProductId" = @ProductId AND "EnvironmentId" = @EnvironmentId
        AND "FinishedAt" IS NULL);

-- Capture insert state: new_id MUST be captured first in a separate INSERT, directly after
-- the MigrationRun INSERT. SQLite evaluates multi-row VALUES sequentially — row1's INSERT
-- modifies last_insert_rowid(), so row2 would see _rc_state's rowid instead of MigrationRun's.
-- changes() counts the rows the MigrationRun INSERT inserted. When a running migration blocked
-- it, last_insert_rowid() still returns the rowid of the last row inserted before on this
-- connection (a row of _rc_state), which can equal the Id of an existing run, so new_id stays
-- NULL then.
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('new_id', CASE WHEN changes() > 0 THEN CAST(last_insert_rowid() AS TEXT) END);
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('inserted', CAST((SELECT COUNT(*) FROM "{CFG:TableBaseName}MigrationRun"
        WHERE "Id" = CAST((SELECT "val" FROM "_rc_state" WHERE "key"='new_id') AS INTEGER)) AS TEXT));

-- Insert MigrationRunMeta if the MigrationRun was created
INSERT INTO "{CFG:TableBaseName}MigrationRunMeta" ("MigrationRunId", "MigrationRunSettingsJson")
SELECT CAST((SELECT "val" FROM "_rc_state" WHERE "key"='new_id') AS INTEGER), @MigrationRunSettingsJson
WHERE CAST((SELECT "val" FROM "_rc_state" WHERE "key"='inserted') AS INTEGER) > 0;

COMMIT;

-- Final result
SELECT CASE
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='running') AS INTEGER) > 0
        THEN '-2,MigrationRun for Product [' || IFNULL((SELECT "val" FROM "_rc_state" WHERE "key"='product_name'), 'NULL')
             || '] with Id [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL')
             || '] is currently in progress. Parallel migrations for the same product and environment are not allowed!'
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='inserted') AS INTEGER) = 0
        THEN '-1,Failed to create MigrationRun for ProductId [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL') || ']'
    ELSE (SELECT "val" FROM "_rc_state" WHERE "key"='new_id')
         || ',MigrationRun with Id [' || (SELECT "val" FROM "_rc_state" WHERE "key"='new_id')
         || '] successfully created for ProductId [' || IFNULL(CAST(@ProductId AS TEXT), 'NULL') || ']'
END;

DROP TABLE IF EXISTS "_rc_state";
