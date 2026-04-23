/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Insert"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new MigrationRun record to track a migration session.
Uses GET_LOCK() for cross-process advisory locking to prevent parallel migrations.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Acquires advisory lock via GET_LOCK() with timeout=0 (immediate fail if locked)
- Lock name encodes ProductId + Environment (different environments can run in parallel)
- Checks for existing unfinished MigrationRun after acquiring the lock
- RELEASE_LOCK() is called before returning the result
- Parallel migrations for same Product/Environment/RunMode are prevented
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId          = "INT | REQUIRED | Product ID from Product table"
EnvironmentId      = "INT | REQUIRED | Environment ID from Environment table"
MigrationRunModeId = "TINYINT UNSIGNED | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigratorMetaId     = "INT | REQUIRED | Version ID from MigratorMeta table"
MigrationRunResultId  = "TINYINT UNSIGNED | REQUIRED | Initial result status (should be 10=Running)"
FromReleaseVersion = "VARCHAR(100) | OPTIONAL | Starting release version for the migration range"
ToReleaseVersion   = "VARCHAR(100) | OPTIONAL | Target release version for the migration range"
MigrationRunSettingsJson = "TEXT | REQUIRED | JSON snapshot of all RayMigrator settings at migration start"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created    = "N (MigrationRunId),MigrationRun with Id [N] successfully created for ProductId [M]"
Error_-2_LockFail  = "-2,Could not acquire migration lock for ProductId [N] environment [E]. Another migration may be in progress."
Error_-2_Parallel  = "-2,MigrationRun for Product [Name] with Id [N] is currently in progress..."
Error_-1_InsertFail = "-1,Failed to create MigrationRun for ProductId [N]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for StartedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "GET_LOCK(@name, 0) acquires advisory lock with immediate timeout"
Note5 = "Lock name pattern: RayMigrator_Run_{ProductId}_{Environment}"
Note6 = "RELEASE_LOCK() is called before returning the result to the caller"
Note7 = "ResultCode convention: >= 0 = Success, -1 = General template error, -2 = Migration already active (parallel run / lock conflict)"
================================================================================
*/

SET @v_lock_name = CONCAT('RayMigrator_Run_', CAST(@ProductId AS CHAR), '_', CAST(@EnvironmentId AS CHAR));
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 0);

SET @v_product_name = (SELECT name FROM {CFG:TableBaseName}product WHERE id = @ProductId);

SET @v_running = CASE WHEN @v_lock_acquired = 1 THEN (
    SELECT COUNT(*) FROM {CFG:TableBaseName}migration_run
    WHERE product_id = @ProductId AND environment_id = @EnvironmentId
      AND migration_run_mode_id = @MigrationRunModeId AND finished_at IS NULL
) ELSE 0 END;

INSERT INTO {CFG:TableBaseName}migration_run
    (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id,
     from_release_version, to_release_version, started_at)
SELECT @MigratorMetaId, @ProductId, @EnvironmentId, @MigrationRunModeId, @MigrationRunResultId,
       @FromReleaseVersion, @ToReleaseVersion, CURRENT_TIMESTAMP
FROM DUAL
WHERE @v_lock_acquired = 1 AND @v_running = 0;

SET @v_inserted = ROW_COUNT();

INSERT INTO {CFG:TableBaseName}migration_run_meta (migration_run_id, migration_run_settings_json)
SELECT LAST_INSERT_ID(), @MigrationRunSettingsJson
FROM DUAL
WHERE @v_lock_acquired = 1 AND @v_running = 0 AND @v_inserted > 0;

SET @v_result = CASE
    WHEN @v_lock_acquired != 1
        THEN CONCAT('-2,Could not acquire migration lock for ProductId [',
             IFNULL(CAST(@ProductId AS CHAR), 'NULL'), '] environmentId [',
             IFNULL(CAST(@EnvironmentId AS CHAR), 'NULL'),
             ']. Another migration may be in progress.')
    WHEN @v_running > 0
        THEN CONCAT('-2,MigrationRun for Product [', IFNULL(@v_product_name, 'NULL'),
             '] with Id [', IFNULL(CAST(@ProductId AS CHAR), 'NULL'),
             '] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=',
             IFNULL(CAST(@MigrationRunModeId AS CHAR), 'NULL'), '] are not allowed!')
    WHEN @v_inserted = 0
        THEN CONCAT('-1,Failed to create MigrationRun for ProductId [',
             IFNULL(CAST(@ProductId AS CHAR), 'NULL'), ']')
    ELSE CONCAT(CAST(LAST_INSERT_ID() AS CHAR), ',MigrationRun with Id [',
         CAST(LAST_INSERT_ID() AS CHAR), '] successfully created for ProductId [',
         IFNULL(CAST(@ProductId AS CHAR), 'NULL'), ']')
END;

DO RELEASE_LOCK(@v_lock_name);

SELECT @v_result;
