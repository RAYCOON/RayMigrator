/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_UpdateRollback"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRecordId            = "INT | REQUIRED | The Migration record ID to update"
MigrationStatusId      = "SMALLINT | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
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
Note4 = "Uses DO block for existence check before update"
================================================================================
*/

DO $$
DECLARE
    v_exists INT;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record
    WHERE id = @MigrationRecordId;

    IF v_exists = 0 THEN
        RAISE EXCEPTION '-1,Migration with Id [%] does not exist', @MigrationRecordId;
    END IF;

    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_record
    SET
        migration_status_id = @MigrationStatusId,
        file_down_hash = @FileDownHash,
        file_down_config_hash = @FileDownConfigHash,
        file_down_blocks_hash = @FileDownBlocksHash,
        file_down_blocks_migrated = @FileDownBlocksMigrated,
        file_down_blocks_total = @FileDownBlocksTotal,
        file_down_config_json = @FileDownConfigJson,
        finished_at = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN NOW()
            ELSE finished_at
        END,
        duration_in_ms = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN EXTRACT(EPOCH FROM (NOW() - started_at)) * 1000
            ELSE duration_in_ms
        END
    WHERE id = @MigrationRecordId;

    -- Historize terminal state changes (100=Migrated, 50=NotMigrated, 30=Failed)
    IF @MigrationStatusId IN (100, 50, 30) THEN
        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history
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
            NOW()
        FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record
        WHERE id = @MigrationRecordId;
    END IF;
END $$;

SELECT CAST(@MigrationRecordId AS VARCHAR(10)) || ',Migration with Id [' || CAST(@MigrationRecordId AS VARCHAR(10)) || '] rollback updated: Block [' || CAST(@FileDownBlocksMigrated AS VARCHAR(10)) || '/' || CAST(@FileDownBlocksTotal AS VARCHAR(10)) || '] - Status [' || CAST(@MigrationStatusId AS VARCHAR(10)) || ']';
