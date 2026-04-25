/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Insert"
DatabaseType   = "MySql"
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
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ExistingMigrationRecordId  = "INT | REQUIRED | 0 = INSERT new record, >0 = UPDATE existing record with this ID"
ProductId            = "INT | REQUIRED | Product ID from Product table"
MigrationRunId       = "INT | REQUIRED | Parent MigrationRun ID"
MigrationRunModeId   = "TINYINT UNSIGNED | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigrationOperationId = "TINYINT UNSIGNED | REQUIRED | Operation: 5=Rollback, 50=MigrateDown, 100=MigrateUp"
MigrationStatusId    = "TINYINT UNSIGNED | REQUIRED | Initial status (should be 10=Pending)"
EnvironmentId        = "INT | REQUIRED | Environment ID from Environment table"
ReleaseVersion       = "VARCHAR(100) | REQUIRED | Release version from folder path"
TargetGroupAlias     = "VARCHAR(100) | REQUIRED | Target group alias from folder path"
TargetAlias          = "VARCHAR(100) | REQUIRED | Target alias (specific database)"
Filename             = "VARCHAR(200) | REQUIRED | Migration filename without path"
FileOrderId          = "INT | REQUIRED | Execution order (based on sorted path)"
FileUpHash           = "VARCHAR(64) | REQUIRED | SHA256 hash of entire file"
FileUpConfigHash     = "VARCHAR(64) | OPTIONAL | SHA256 hash of TOML config section"
FileUpBlocksHash     = "VARCHAR(64) | REQUIRED | SHA256 hash of SQL content blocks"
FileUpBlocksTotal    = "INT | REQUIRED | Total number of separator-delimited blocks"
FileUpConfigJson     = "TEXT | OPTIONAL | JSON of parsed TOML configuration"
MigrateDownFileExists = "TINYINT(1) | REQUIRED | 1 if .down.sql file exists, 0 otherwise"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created = "N (MigrationRecordId),Migration record with Id [N] successfully created for file [Filename]"
Success_Updated = "N (MigrationRecordId),Migration record with Id [N] successfully reset for file [Filename]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for StartedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "FileUpBlocksMigrated initialized to 0"
Note5 = "MigrationStatusId values: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
Note6 = "No IF/THEN/ELSE outside stored procedures - uses conditional WHERE clauses instead"
================================================================================
*/

SET @v_existing_id = @ExistingMigrationRecordId;

-- UPDATE path: runs only when existing record found (no-op when @v_existing_id = 0)
UPDATE {CFG:TableBaseName}migration_record
SET
    migration_run_id          = @MigrationRunId,
    migration_run_mode_id     = @MigrationRunModeId,
    migration_operation_id    = @MigrationOperationId,
    migration_status_id       = @MigrationStatusId,
    file_order_id             = @FileOrderId,
    file_up_hash              = @FileUpHash,
    file_up_config_hash       = @FileUpConfigHash,
    file_up_blocks_hash       = @FileUpBlocksHash,
    file_up_blocks_migrated   = 0,
    file_up_blocks_total      = @FileUpBlocksTotal,
    file_up_config_json       = @FileUpConfigJson,
    migrate_down_file_exists  = @MigrateDownFileExists,
    file_down_hash            = NULL,
    file_down_config_hash     = NULL,
    file_down_blocks_hash     = NULL,
    file_down_blocks_migrated = NULL,
    file_down_blocks_total    = NULL,
    file_down_config_json     = NULL,
    started_at                = CURRENT_TIMESTAMP,
    finished_at               = NULL,
    duration_in_ms            = NULL
WHERE id = @v_existing_id AND @v_existing_id > 0;

-- Track whether the UPDATE matched a row
SET @v_updated = ROW_COUNT();

-- INSERT path: runs only when no existing record (no-op when @v_updated > 0)
INSERT INTO {CFG:TableBaseName}migration_record
(
    product_id,
    environment_id,
    migration_run_id,
    migration_run_mode_id,
    migration_operation_id,
    migration_status_id,
    release_version,
    target_group_alias,
    target_alias,
    filename,
    file_order_id,
    file_up_hash,
    file_up_config_hash,
    file_up_blocks_hash,
    file_up_blocks_migrated,
    file_up_blocks_total,
    file_up_config_json,
    migrate_down_file_exists,
    started_at
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
    CURRENT_TIMESTAMP
FROM DUAL WHERE @v_updated = 0;

-- Return the MigrationRecordId
SET @v_migration_record_id = CASE WHEN @v_updated > 0 THEN @v_existing_id ELSE LAST_INSERT_ID() END;

SELECT CONCAT(
    CAST(@v_migration_record_id AS CHAR),
    ',Migration record with Id [', CAST(@v_migration_record_id AS CHAR), '] successfully ',
    CASE WHEN @v_updated > 0 THEN 'reset' ELSE 'created' END,
    ' for file [', IFNULL(@Filename, 'NULL'), ']'
);
