/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_Select"
DatabaseType   = "Sqlite"
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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId          = "INTEGER | REQUIRED | The product ID to query migrations for"
EnvironmentId      = "INTEGER | REQUIRED | The environment ID to query migrations for"
MigrationRunModeId = "INTEGER | REQUIRED | Run mode (typically 100=Migrate)"

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
Note3 = "Used for hash validation and determining migration state"
================================================================================
*/

SELECT
    "Id"
    ,"ProductId"
    ,"MigrationRunId"
    ,"MigrationOperationId"
    ,"MigrationStatusId"
    ,"ReleaseVersion"
    ,"TargetGroupAlias"
    ,"TargetAlias"
    ,"Filename"
    ,"FileOrderId"
    ,"FileUpHash"
    ,"FileUpConfigHash"
    ,"FileUpBlocksHash"
    ,"FileUpBlocksMigrated"
    ,"FileUpBlocksTotal"
    ,"MigrateDownFileExists"
    ,"FileDownHash"
    ,"FileDownConfigHash"
    ,"FileDownBlocksHash"
    ,"FileDownBlocksMigrated"
    ,"FileDownBlocksTotal"
FROM
    "{CFG:TableBaseName}MigrationRecord"
WHERE
    "ProductId" = @ProductId AND
    "EnvironmentId" = @EnvironmentId AND
    "MigrationRunModeId" = @MigrationRunModeId;
