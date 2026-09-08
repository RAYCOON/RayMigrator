/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Select"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-09-08.1"

[Description]
Function = """
Selects the terminal state transitions (MigrationRecordHistory rows) of all migration records of a
Product and Environment. Every row carries the MigrationRun that caused the transition, so the info run
history can attribute rolled-back records to the migrate-down or error-recovery run that rolled them back,
while the records themselves stay owned by the run that created them (#13).
"""

Behaviour = """
- Returns an empty result set if no history rows exist
- One row per terminal transition (Migrated, NotMigrated, Failed), ordered by history Id ascending
- Id is the MigrationRecordId (not the history row id), MigrationRunId is the run that caused the transition
- NOTE: This is a SELECT query, NOT the standard 'code,message' format
- Column contract is identical to Repository_MigrationRecord_Select so both are mapped by the same reader
"""

[ConfigPlaceholders]
SchemaName    = "Not used (MariaDB uses the database itself)"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
ProductId          = "INT | REQUIRED | The product ID to query history rows for"
EnvironmentId      = "INT | REQUIRED | The environment ID to query history rows for"
MigrationRunModeId = "TINYINT | REQUIRED | Run mode (typically 100=Migrate)"

[ReturnValues]
# This template returns a result set, NOT the standard 'code,message' format
# Columns returned (in order):
#   Id (= MigrationRecordId), ProductId, MigrationRunId, MigrationOperationId, MigrationStatusId,
#   ReleaseVersion, TargetGroupAlias, TargetAlias, Filename, FileOrderId,
#   FileUpHash, FileUpConfigHash, FileUpBlocksHash, FileUpBlocksMigrated, FileUpBlocksTotal,
#   MigrateDownFileExists, FileDownHash, FileDownConfigHash, FileDownBlocksHash,
#   FileDownBlocksMigrated, FileDownBlocksTotal

[ModificationNotes]
Note1 = "This template returns a result set - NOT the standard 'code,message' format"
Note2 = "Column names are the reader contract - keep them identical to Repository_MigrationRecord_Select"
Note3 = "ORDER BY the history row id: the last row of a record within a run is its final state in that run"
================================================================================
*/

SELECT
    migration_record_id       AS `Id`
    ,product_id                AS `ProductId`
    ,migration_run_id          AS `MigrationRunId`
    ,migration_operation_id    AS `MigrationOperationId`
    ,migration_status_id       AS `MigrationStatusId`
    ,release_version           AS `ReleaseVersion`
    ,target_group_alias        AS `TargetGroupAlias`
    ,target_alias              AS `TargetAlias`
    ,filename                  AS `Filename`
    ,file_order_id             AS `FileOrderId`
    ,file_up_hash              AS `FileUpHash`
    ,file_up_config_hash       AS `FileUpConfigHash`
    ,file_up_blocks_hash       AS `FileUpBlocksHash`
    ,file_up_blocks_migrated   AS `FileUpBlocksMigrated`
    ,file_up_blocks_total      AS `FileUpBlocksTotal`
    ,migrate_down_file_exists  AS `MigrateDownFileExists`
    ,file_down_hash            AS `FileDownHash`
    ,file_down_config_hash     AS `FileDownConfigHash`
    ,file_down_blocks_hash     AS `FileDownBlocksHash`
    ,file_down_blocks_migrated AS `FileDownBlocksMigrated`
    ,file_down_blocks_total    AS `FileDownBlocksTotal`
FROM
    {CFG:TableBaseName}migration_record_history
WHERE
    product_id = @ProductId AND
    environment_id = @EnvironmentId AND
    migration_run_mode_id = @MigrationRunModeId
ORDER BY
    id ASC;
