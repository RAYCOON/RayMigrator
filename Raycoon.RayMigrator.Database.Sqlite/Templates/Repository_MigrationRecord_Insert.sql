/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Insert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new Migration record or resets an existing archived record
to track individual migration file execution.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- When @ExistingMigrationRecordId = 0: INSERT a new record (original behaviour)
- When @ExistingMigrationRecordId > 0: UPDATE the existing record, resetting all fields
- Return value >= 0: Success (MigrationRecordId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- FileUpBlocksMigrated starts at 0, incremented as blocks execute
- StartedAt timestamp recorded immediately
- UPDATE resets all FileDown* fields to NULL
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ExistingMigrationRecordId  = "INTEGER | REQUIRED | 0 = INSERT new record, >0 = UPDATE existing record with this ID"
ProductId            = "INTEGER | REQUIRED | Product ID from Product table"
MigrationRunId       = "INTEGER | REQUIRED | Parent MigrationRun ID"
MigrationRunModeId   = "INTEGER | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigrationOperationId = "INTEGER | REQUIRED | Operation: 5=Rollback, 50=MigrateDown, 100=MigrateUp"
MigrationStatusId    = "INTEGER | REQUIRED | Initial status (should be 10=Pending)"
EnvironmentId        = "INTEGER | REQUIRED | Environment ID from Environment table"
ReleaseVersion       = "TEXT | REQUIRED | Release version from folder path"
TargetGroupAlias     = "TEXT | REQUIRED | Target group alias from folder path"
TargetAlias          = "TEXT | REQUIRED | Target alias (specific database)"
Filename             = "TEXT | REQUIRED | Migration filename without path"
FileOrderId          = "INTEGER | REQUIRED | Execution order (based on sorted path)"
FileUpHash           = "TEXT | REQUIRED | SHA256 hash of entire file"
FileUpConfigHash     = "TEXT | OPTIONAL | SHA256 hash of TOML config section"
FileUpBlocksHash     = "TEXT | REQUIRED | SHA256 hash of SQL content blocks"
FileUpBlocksTotal    = "INTEGER | REQUIRED | Total number of separator-delimited blocks"
FileUpConfigJson     = "TEXT | OPTIONAL | JSON of parsed TOML configuration"
MigrateDownFileExists = "INTEGER | REQUIRED | 1 if .down.sql file exists, 0 otherwise"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created = "N (MigrationRecordId),Migration record with Id [N] successfully created for file [Filename]"
Success_Updated = "N (MigrationRecordId),Migration record with Id [N] successfully reset for file [Filename]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for StartedAt timestamp"
Note4 = "FileUpBlocksMigrated initialized to 0"
Note5 = "Uses last_insert_rowid() to retrieve the new MigrationRecordId"
Note6 = "SQLite has no IF/ELSE - UPDATE runs on matching WHERE, INSERT uses WHERE NOT to skip"
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
    "MigrationRunId"          = @MigrationRunId,
    "MigrationRunModeId"      = @MigrationRunModeId,
    "MigrationOperationId"    = @MigrationOperationId,
    "MigrationStatusId"       = @MigrationStatusId,
    "FileOrderId"             = @FileOrderId,
    "FileUpHash"              = @FileUpHash,
    "FileUpConfigHash"        = @FileUpConfigHash,
    "FileUpBlocksHash"        = @FileUpBlocksHash,
    "FileUpBlocksMigrated"    = 0,
    "FileUpBlocksTotal"       = @FileUpBlocksTotal,
    "FileUpConfigJson"        = @FileUpConfigJson,
    "MigrateDownFileExists"   = @MigrateDownFileExists,
    "FileDownHash"            = NULL,
    "FileDownConfigHash"      = NULL,
    "FileDownBlocksHash"      = NULL,
    "FileDownBlocksMigrated"  = NULL,
    "FileDownBlocksTotal"     = NULL,
    "FileDownConfigJson"      = NULL,
    "StartedAt"               = datetime('now'),
    "FinishedAt"              = NULL,
    "DurationInMs"            = NULL
WHERE "Id" = @ExistingMigrationRecordId AND @ExistingMigrationRecordId > 0;

INSERT INTO "{CFG:TableBaseName}MigrationRecord"
(
    "ProductId",
    "EnvironmentId",
    "MigrationRunId",
    "MigrationRunModeId",
    "MigrationOperationId",
    "MigrationStatusId",
    "ReleaseVersion",
    "TargetGroupAlias",
    "TargetAlias",
    "Filename",
    "FileOrderId",
    "FileUpHash",
    "FileUpConfigHash",
    "FileUpBlocksHash",
    "FileUpBlocksMigrated",
    "FileUpBlocksTotal",
    "FileUpConfigJson",
    "MigrateDownFileExists",
    "StartedAt"
)
SELECT
    @ProductId,
    @EnvironmentId,
    @MigrationRunId,
    @MigrationRunModeId,
    @MigrationOperationId,
    @MigrationStatusId,
    @ReleaseVersion,
    @TargetGroupAlias,
    @TargetAlias,
    @Filename,
    @FileOrderId,
    @FileUpHash,
    @FileUpConfigHash,
    @FileUpBlocksHash,
    0,
    @FileUpBlocksTotal,
    @FileUpConfigJson,
    @MigrateDownFileExists,
    datetime('now')
WHERE @ExistingMigrationRecordId <= 0 OR @ExistingMigrationRecordId IS NULL;

SELECT CASE
    WHEN @ExistingMigrationRecordId > 0 THEN
        CAST(@ExistingMigrationRecordId AS TEXT) || ',Migration record with Id [' || CAST(@ExistingMigrationRecordId AS TEXT) || '] successfully reset for file [' || IFNULL(@Filename, 'NULL') || ']'
    ELSE
        CAST(last_insert_rowid() AS TEXT) || ',Migration record with Id [' || CAST(last_insert_rowid() AS TEXT) || '] successfully created for file [' || IFNULL(@Filename, 'NULL') || ']'
END;
