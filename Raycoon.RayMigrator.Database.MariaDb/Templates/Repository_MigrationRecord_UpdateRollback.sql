/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_UpdateRollback"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Updates a Migration record with rollback (FileDown*) fields and status.
Used during rollback execution to track rollback file metadata and block progress.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRecordId returned)
- Return value < 0: Error
- FinishedAt and DurationInMs only set when MigrationStatusId indicates completion (IN 100, 50, 30)
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRecordId            = "INT | REQUIRED | The Migration record ID to update"
MigrationStatusId      = "TINYINT | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileDownHash           = "VARCHAR(64) | REQUIRED | SHA256 hash of the rollback file"
FileDownConfigHash     = "VARCHAR(64) | OPTIONAL | SHA256 hash of TOML config in rollback file"
FileDownBlocksHash     = "VARCHAR(64) | REQUIRED | SHA256 hash of SQL blocks in rollback file"
FileDownBlocksMigrated = "INT | REQUIRED | Number of rollback blocks successfully executed"
FileDownBlocksTotal    = "INT | REQUIRED | Total number of rollback blocks"
FileDownConfigJson     = "TEXT | OPTIONAL | JSON of parsed TOML config from rollback file"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRecordId),Migration with Id [N] rollback updated: Block [X/Y] - Status [S]"
Error_-1_NotFound = "-1,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "FinishedAt and DurationInMs conditionally set only for MigrationStatusId IN (100, 50, 30)"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_record
SET
    migration_status_id = @MigrationStatusId,
    file_down_hash = @FileDownHash,
    file_down_config_hash = @FileDownConfigHash,
    file_down_blocks_hash = @FileDownBlocksHash,
    file_down_blocks_migrated = @FileDownBlocksMigrated,
    file_down_blocks_total = @FileDownBlocksTotal,
    file_down_config_json = @FileDownConfigJson,
    finished_at = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN CURRENT_TIMESTAMP
        ELSE finished_at
    END,
    duration_in_ms = CASE
        WHEN @MigrationStatusId IN (100, 50, 30) THEN TIMESTAMPDIFF(MICROSECOND, started_at, CURRENT_TIMESTAMP) / 1000
        ELSE duration_in_ms
    END
WHERE id = @MigrationRecordId;

SET @v_affected = ROW_COUNT();

-- Historize terminal state changes (100=Migrated, 50=NotMigrated, 30=Failed)
INSERT INTO {CFG:TableBaseName}migration_record_history
(
    migration_record_id, product_id, environment_id, migration_run_id, migration_run_mode_id, migration_operation_id,
    migration_status_id, release_version, target_group_alias, target_alias,
    filename, file_order_id, file_up_hash, file_up_config_hash, file_up_blocks_hash,
    file_up_blocks_migrated, file_up_blocks_total, file_up_config_json, migrate_down_file_exists,
    file_down_hash, file_down_config_hash, file_down_blocks_hash, file_down_blocks_migrated,
    file_down_blocks_total, file_down_config_json, started_at, finished_at, duration_in_ms,
    historized_at
)
SELECT
    id, product_id, environment_id, migration_run_id, migration_run_mode_id, migration_operation_id,
    migration_status_id, release_version, target_group_alias, target_alias,
    filename, file_order_id, file_up_hash, file_up_config_hash, file_up_blocks_hash,
    file_up_blocks_migrated, file_up_blocks_total, file_up_config_json, migrate_down_file_exists,
    file_down_hash, file_down_config_hash, file_down_blocks_hash, file_down_blocks_migrated,
    file_down_blocks_total, file_down_config_json, started_at, finished_at, duration_in_ms,
    CURRENT_TIMESTAMP
FROM {CFG:TableBaseName}migration_record
WHERE id = @MigrationRecordId AND @MigrationStatusId IN (100, 50, 30) AND @v_affected > 0;

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('-1,Migration with Id [', IFNULL(CAST(@MigrationRecordId AS CHAR), 'NULL'), '] does not exist')
    ELSE CONCAT(CAST(@MigrationRecordId AS CHAR), ',Migration with Id [', CAST(@MigrationRecordId AS CHAR), '] rollback updated: Block [', CAST(@FileDownBlocksMigrated AS CHAR), '/', CAST(@FileDownBlocksTotal AS CHAR), '] - Status [', CAST(@MigrationStatusId AS CHAR), ']')
END;
