/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_UpdateRollback"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Updates a Migration record with rollback (FileDown*) fields and status.
Used during rollback execution to track rollback file metadata and block progress.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned)
- Return value < 0: Error
- FinishedAt and DurationInMs only set when MigrationStatusId indicates completion (IN 100, 50, 30)
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationId            = "INTEGER | REQUIRED | The Migration record ID to update"
MigrationStatusId      = "INTEGER | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileDownHash           = "TEXT | REQUIRED | SHA256 hash of the rollback file"
FileDownConfigHash     = "TEXT | OPTIONAL | SHA256 hash of TOML config in rollback file"
FileDownBlocksHash     = "TEXT | REQUIRED | SHA256 hash of SQL blocks in rollback file"
FileDownBlocksMigrated = "INTEGER | REQUIRED | Number of rollback blocks successfully executed"
FileDownBlocksTotal    = "INTEGER | REQUIRED | Total number of rollback blocks"
FileDownConfigJson     = "TEXT | OPTIONAL | JSON of parsed TOML config from rollback file"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationId),Migration with Id [N] rollback updated: Block [X/Y] - Status [S]"
Error_-1_NotFound = "-1,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "FinishedAt and DurationInMs conditionally set only for MigrationStatusId IN (100, 50, 30)"
Note4 = "DurationInMs calculated via julianday difference * 86400000"
Note5 = "Uses temp table to capture changes() since SQLite has no session variables"
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

UPDATE "{CFG:TableBaseName}MigrationRecord"
SET
    "MigrationStatusId" = @MigrationStatusId,
    "FileDownHash" = @FileDownHash,
    "FileDownConfigHash" = @FileDownConfigHash,
    "FileDownBlocksHash" = @FileDownBlocksHash,
    "FileDownBlocksMigrated" = @FileDownBlocksMigrated,
    "FileDownBlocksTotal" = @FileDownBlocksTotal,
    "FileDownConfigJson" = @FileDownConfigJson,
    "FinishedAt" = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN datetime('now')
        ELSE "FinishedAt"
    END,
    "DurationInMs" = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN CAST((julianday('now') - julianday("StartedAt")) * 86400000 AS INTEGER)
        ELSE "DurationInMs"
    END
WHERE "Id" = @MigrationId;

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
WHERE "Id" = @MigrationId AND @MigrationStatusId IN (100, 50, 30)
  AND (SELECT "cnt" FROM "_rc_affected") > 0;

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_affected") = 0
    THEN '-1,Migration with Id [' || IFNULL(CAST(@MigrationId AS TEXT), 'NULL') || '] does not exist'
    ELSE CAST(@MigrationId AS TEXT) || ',Migration with Id [' || CAST(@MigrationId AS TEXT) || '] rollback updated: Block [' || CAST(@FileDownBlocksMigrated AS TEXT) || '/' || CAST(@FileDownBlocksTotal AS TEXT) || '] - Status [' || CAST(@MigrationStatusId AS TEXT) || ']'
END;

DROP TABLE IF EXISTS "_rc_affected";
