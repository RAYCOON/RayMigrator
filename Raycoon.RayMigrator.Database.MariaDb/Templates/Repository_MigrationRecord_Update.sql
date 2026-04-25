/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Update"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

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
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRecordId          = "INT | REQUIRED | The Migration record ID to update"
MigrationStatusId    = "TINYINT UNSIGNED | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileUpBlocksMigrated = "INT | REQUIRED | Number of blocks successfully executed"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRecordId),Migration with Id [N] updated: Block [X] - Status [S]"
Error_-40_NotFound = "-40,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for FinishedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "FinishedAt only set for terminal MigrationStatusId IN (100, 50, 30) - Migrated, NotMigrated, Failed"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_record
SET
    migration_status_id = @MigrationStatusId,
    file_up_blocks_migrated = @FileUpBlocksMigrated,
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
    THEN CONCAT('-40,Migration with Id [', IFNULL(CAST(@MigrationRecordId AS CHAR), 'NULL'), '] does not exist')
    ELSE CONCAT(CAST(@MigrationRecordId AS CHAR), ',Migration with Id [', CAST(@MigrationRecordId AS CHAR), '] updated: Block [', CAST(@FileUpBlocksMigrated AS CHAR), '] - Status [', CAST(@MigrationStatusId AS CHAR), ']')
END;
