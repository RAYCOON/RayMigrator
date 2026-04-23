/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_GetInterrupted"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Detects interrupted migrations that can be resumed from a specific block.
Used for recovery after process crash or unexpected termination.
"""

Behaviour = """
- Return value = 0: No interrupted migration found (logged at Debug level)
- Return value > 0: Interrupted MigrationId found - message contains recovery details
- Searches for migrations with: Pending (10) or Executing (20) status + incomplete blocks + no FinishedAt
- Returns most recent interrupted migration by FileOrderId
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId     = "INT | REQUIRED | The product ID to check for interrupted migrations"
EnvironmentId = "INT | REQUIRED | The environment ID to check (DEV, QA, PROD)"

[ReturnValues]
# Format: SELECT 'code,message' where message contains pipe-separated recovery data
Success_0_NotFound = "0,No interrupted migration found for ProductId [N] with EnvironmentId [E]"
Success_N_Found    = "N (MigrationId),MigrationId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "Pipe-separated message format is critical - code parses these fields"
Note3 = "Detection criteria: MigrationStatusId IN (10, 20) AND BlocksMigrated<BlocksTotal AND FinishedAt IS NULL"
Note4 = "MigrationStatusId 10 = Pending; 20 = Executing"
================================================================================
*/

SET @v_migration_id = NULL;

SELECT
    m.id, m.migration_run_id, m.release_version, m.filename,
    m.file_up_blocks_migrated, m.file_up_blocks_total, m.environment_id, m.target_group_alias, m.target_alias
INTO
    @v_migration_id, @v_migration_run_id, @v_release_version, @v_filename,
    @v_blocks_migrated, @v_blocks_total, @v_found_environment_id, @v_target_group_alias, @v_target_alias
FROM {CFG:TableBaseName}migration_record m
INNER JOIN {CFG:TableBaseName}migration_run mr ON m.migration_run_id = mr.id
WHERE
    m.product_id = @ProductId
    AND m.environment_id = @EnvironmentId
    AND m.migration_status_id IN (10, 20)
    AND m.file_up_blocks_migrated < m.file_up_blocks_total
    AND m.finished_at IS NULL
ORDER BY m.file_order_id
LIMIT 1;

SELECT CASE WHEN @v_migration_id IS NULL
    THEN CONCAT('0,No interrupted migration found for ProductId [', CAST(@ProductId AS CHAR), '] with EnvironmentId [', CAST(@EnvironmentId AS CHAR), ']')
    ELSE CONCAT(
        CAST(@v_migration_id AS CHAR), ',',
        CAST(@v_migration_id AS CHAR), '|',
        CAST(@v_migration_run_id AS CHAR), '|',
        IFNULL(@v_release_version, ''), '|',
        IFNULL(@v_filename, ''), '|',
        CAST(@v_blocks_migrated AS CHAR), '|',
        CAST(@v_blocks_total AS CHAR), '|',
        CAST(@v_found_environment_id AS CHAR), '|',
        IFNULL(@v_target_group_alias, ''), '|',
        IFNULL(@v_target_alias, ''))
END;
