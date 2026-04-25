/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Select"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Selects all Migration records for a specific Product, Environment, and MigrationRunMode.
Used to determine which migrations have been executed and their current state.
"""

Behaviour = """
- Returns NULL/empty result set if no migrations found
- Returns multiple rows with migration details if found
- NOTE: This is a SELECT query, NOT the standard 'code,message' format
- Results are used by code to build MigrationFile objects for comparison
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId          = "INT | REQUIRED | The product ID to query migrations for"
EnvironmentId      = "INT | REQUIRED | The environment ID to query migrations for"
MigrationRunModeId = "TINYINT | REQUIRED | Run mode (typically 100=Migrate)"

[ReturnValues]
# This template returns a result set, NOT the standard 'code,message' format
# Columns returned (in order):
#   Id, ProductId, MigrationRunId, MigrationOperationId, MigrationStatusId,
#   ReleaseVersion, TargetGroupAlias, TargetAlias, Filename, FileOrderId,
#   FileUpHash, FileUpConfigHash, FileUpBlocksHash, FileUpBlocksMigrated, FileUpBlocksTotal,
#   MigrateDownFileExists, FileDownHash, FileDownConfigHash, FileDownBlocksHash,
#   FileDownBlocksMigrated, FileDownBlocksTotal

[ModificationNotes]
Note1 = "This template returns a result set - NOT the standard 'code,message' format"
Note2 = "Column order is critical - code depends on positional binding"
Note3 = "DAL-018: Storage uses snake_case identifiers; output columns are aliased to PascalCase (AS \`...\`) to preserve the cross-engine row[\"PascalCase\"] reader contract"
================================================================================
*/

SELECT
    id                        AS `Id`
    ,product_id               AS `ProductId`
    ,migration_run_id         AS `MigrationRunId`
    ,migration_operation_id   AS `MigrationOperationId`
    ,migration_status_id      AS `MigrationStatusId`
    ,release_version          AS `ReleaseVersion`
    ,target_group_alias       AS `TargetGroupAlias`
    ,target_alias             AS `TargetAlias`
    ,filename                 AS `Filename`
    ,file_order_id            AS `FileOrderId`
    ,file_up_hash             AS `FileUpHash`
    ,file_up_config_hash      AS `FileUpConfigHash`
    ,file_up_blocks_hash      AS `FileUpBlocksHash`
    ,file_up_blocks_migrated  AS `FileUpBlocksMigrated`
    ,file_up_blocks_total     AS `FileUpBlocksTotal`
    ,migrate_down_file_exists AS `MigrateDownFileExists`
    ,file_down_hash           AS `FileDownHash`
    ,file_down_config_hash    AS `FileDownConfigHash`
    ,file_down_blocks_hash    AS `FileDownBlocksHash`
    ,file_down_blocks_migrated AS `FileDownBlocksMigrated`
    ,file_down_blocks_total   AS `FileDownBlocksTotal`
FROM
    {CFG:TableBaseName}migration_record
WHERE
    product_id = @ProductId AND
    environment_id = @EnvironmentId AND
    migration_run_mode_id = @MigrationRunModeId;
