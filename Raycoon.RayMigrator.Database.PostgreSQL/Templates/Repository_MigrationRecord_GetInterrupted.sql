/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_GetInterrupted"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Detects interrupted migrations that can be resumed from a specific block.
Used for recovery after process crash or unexpected termination.
"""

Behaviour = """
- Return value = 0: No interrupted migration found (logged at Debug level)
- Return value > 0: Interrupted MigrationRecordId found - message contains recovery details
- Searches for migrations with: Pending or Executing status + incomplete blocks + no FinishedAt
- Returns most recent interrupted migration by FileOrderId
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
ProductId     = "INT | REQUIRED | The product ID to check for interrupted migrations"
EnvironmentId = "INT | REQUIRED | The environment ID to check (DEV, QA, PROD)"

[ReturnValues]
# Format: SELECT 'code,message' where message contains pipe-separated recovery data
Success_0_NotFound = "0,No interrupted migration found for ProductId [N] with EnvironmentId [E]"
Success_N_Found    = "N (MigrationRecordId),MigrationRecordId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "Pipe-separated message format is critical - code parses these fields"
Note3 = "Detection criteria: MigrationStatusId IN (10, 20) AND BlocksMigrated<BlocksTotal AND FinishedAt IS NULL"
================================================================================
*/

DO $$
DECLARE
    v_migration_record_id INT;
    v_migration_run_id INT;
    v_release_version VARCHAR(100);
    v_filename VARCHAR(200);
    v_blocks_migrated INT;
    v_blocks_total INT;
    v_found_environment_id INT;
    v_target_group_alias VARCHAR(100);
    v_target_alias VARCHAR(100);
BEGIN
    SELECT
        m.id,
        m.migration_run_id,
        m.release_version,
        m.filename,
        m.file_up_blocks_migrated,
        m.file_up_blocks_total,
        m.environment_id,
        m.target_group_alias,
        m.target_alias
    INTO
        v_migration_record_id,
        v_migration_run_id,
        v_release_version,
        v_filename,
        v_blocks_migrated,
        v_blocks_total,
        v_found_environment_id,
        v_target_group_alias,
        v_target_alias
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record m
    INNER JOIN {CFG:SchemaName}.{CFG:TableBaseName}migration_run mr ON m.migration_run_id = mr.id
    WHERE
        m.product_id = @ProductId
        AND m.environment_id = @EnvironmentId
        AND m.migration_status_id IN (10, 20)
        AND m.file_up_blocks_migrated < m.file_up_blocks_total
        AND m.finished_at IS NULL
    ORDER BY m.file_order_id
    LIMIT 1;

    IF v_migration_record_id IS NULL THEN
        RAISE NOTICE '0,No interrupted migration found for ProductId [%] with EnvironmentId [%]',
            CAST(@ProductId AS VARCHAR(10)), CAST(@EnvironmentId AS VARCHAR(10));
        RETURN;
    END IF;

    RAISE NOTICE '%,%|%|%|%|%|%|%|%|%',
        v_migration_record_id,
        v_migration_record_id,
        v_migration_run_id,
        COALESCE(v_release_version, ''),
        COALESCE(v_filename, ''),
        v_blocks_migrated,
        v_blocks_total,
        v_found_environment_id,
        COALESCE(v_target_group_alias, ''),
        COALESCE(v_target_alias, '');
END $$;
