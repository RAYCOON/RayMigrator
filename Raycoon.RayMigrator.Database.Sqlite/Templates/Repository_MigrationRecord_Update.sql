/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Update"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Updates a Migration record with block progress or final status.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRecordId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- FinishedAt and DurationInMs only set when MigrationStatusId indicates completion (IN 100, 50, 30)
- Can be called multiple times during migration to update block progress
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRecordId          = "INTEGER | REQUIRED | The Migration record ID to update"
MigrationStatusId    = "INTEGER | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileUpBlocksMigrated = "INTEGER | REQUIRED | Number of blocks successfully executed"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRecordId),Migration with Id [N] updated: Block [X] - Status [S]"
Error_-40_NotFound = "-40,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for FinishedAt timestamp"
Note4 = "FinishedAt only set for terminal MigrationStatusId IN (100, 50, 30) - Migrated, NotMigrated, Failed"
Note5 = "DurationInMs calculated via julianday difference * 86400000"
Note6 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note7 = "Uses temp table to capture changes() since SQLite has no session variables"
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
 * On the atomic path the engine runs this template inside the transaction
 * of the file on the shared connection, so it must not open a transaction
 * of its own. On the repository connection the UPDATE and the history
 * INSERT commit one by one.
 */

UPDATE "{CFG:TableBaseName}MigrationRecord"
SET
    "MigrationStatusId" = @MigrationStatusId,
    "FileUpBlocksMigrated" = @FileUpBlocksMigrated,
    "FinishedAt" = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN datetime('now')
        ELSE "FinishedAt"
    END,
    "DurationInMs" = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN CAST((julianday('now') - julianday("StartedAt")) * 86400000 AS INTEGER)
        ELSE "DurationInMs"
    END
WHERE "Id" = @MigrationRecordId;

-- DROP+CREATE instead of CREATE IF NOT EXISTS+DELETE: DELETE is DML and resets changes() to 0.
DROP TABLE IF EXISTS "_rc_affected";
CREATE TEMP TABLE "_rc_affected" ("cnt" INTEGER);
INSERT INTO "_rc_affected" ("cnt") VALUES (changes());

-- Historize terminal state changes (100=Migrated, 50=NotMigrated, 30=Failed)
INSERT INTO "{CFG:TableBaseName}MigrationRecordHistory"
(
    "MigrationRecordId", "ProductId", "EnvironmentId", "MigrationRunId", "MigrationRunModeId", "MigrationOperationId",
    "MigrationStatusId", "ReleaseVersion", "TargetGroupAlias", "TargetAlias",
    "Filename", "FileOrderId", "FileUpHash", "FileUpConfigHash", "FileUpBlocksHash",
    "FileUpBlocksMigrated", "FileUpBlocksTotal", "FileUpConfigJson", "MigrateDownFileExists",
    "FileDownHash", "FileDownConfigHash", "FileDownBlocksHash", "FileDownBlocksMigrated",
    "FileDownBlocksTotal", "FileDownConfigJson", "StartedAt", "FinishedAt", "DurationInMs",
    "HistorizedAt"
)
SELECT
    "Id", "ProductId", "EnvironmentId", "MigrationRunId", "MigrationRunModeId", "MigrationOperationId",
    "MigrationStatusId", "ReleaseVersion", "TargetGroupAlias", "TargetAlias",
    "Filename", "FileOrderId", "FileUpHash", "FileUpConfigHash", "FileUpBlocksHash",
    "FileUpBlocksMigrated", "FileUpBlocksTotal", "FileUpConfigJson", "MigrateDownFileExists",
    "FileDownHash", "FileDownConfigHash", "FileDownBlocksHash", "FileDownBlocksMigrated",
    "FileDownBlocksTotal", "FileDownConfigJson", "StartedAt", "FinishedAt", "DurationInMs",
    datetime('now')
FROM "{CFG:TableBaseName}MigrationRecord"
WHERE "Id" = @MigrationRecordId AND @MigrationStatusId IN (100, 50, 30)
  AND (SELECT "cnt" FROM "_rc_affected") > 0;

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '-40,Migration with Id [' || IFNULL(CAST(@MigrationRecordId AS TEXT), 'NULL') || '] does not exist'
    ELSE CAST(@MigrationRecordId AS TEXT) || ',Migration with Id [' || CAST(@MigrationRecordId AS TEXT) || '] updated: Block [' || CAST(@FileUpBlocksMigrated AS TEXT) || '] - Status [' || CAST(@MigrationStatusId AS TEXT) || ']'
END;

DROP TABLE IF EXISTS "_rc_affected";
