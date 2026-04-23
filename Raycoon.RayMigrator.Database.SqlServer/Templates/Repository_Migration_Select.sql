/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_Select"
DatabaseType   = "SqlServer"
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
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
ProductId          = "INT | REQUIRED | The product ID to query migrations for"
EnvironmentId      = "INT | REQUIRED | The environment ID to query migrations for"
MigrationRunModeId = "TINYINT | REQUIRED | Run mode (typically 100=Migrate for actual migrations)"

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
Note3 = "Commented columns (MigrationRunModeId, Environment, ConfigJson, timestamps) are available but not currently used"
Note4 = "Used for hash validation and determining migration state"
================================================================================
*/

-- Mandatory RepositoryVersion: DO NOT change manually, otherwise repository-inconsistencies may occur that results in migration errors !!!

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    SELECT 
        [Id]
        ,[ProductId]
        ,[MigrationRunId]
--         ,[MigrationRunModeId]
        ,[MigrationOperationId]
        ,[MigrationStatusId]
--         ,[Environment]
        ,[ReleaseVersion]
        ,[TargetGroupAlias]
        ,[TargetAlias]
        ,[Filename]
        ,[FileOrderId]
        ,[FileUpHash]
        ,[FileUpConfigHash]
        ,[FileUpBlocksHash]
        ,[FileUpBlocksMigrated]
        ,[FileUpBlocksTotal]
--         ,[FileUpConfigJson]
        ,[MigrateDownFileExists]
        ,[FileDownHash]
        ,[FileDownConfigHash]
        ,[FileDownBlocksHash]
        ,[FileDownBlocksMigrated]
        ,[FileDownBlocksTotal]
--         ,[FileDownConfigJson]
--         ,[StartedAt]
--         ,[FinishedAt]
--         ,[DurationInMs]
    FROM
        [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    WHERE
        [ProductId] = @ProductId AND
        [EnvironmentId] = @EnvironmentId AND
        [MigrationRunModeId] = @MigrationRunModeId;
        
END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        ;THROW;

END CATCH;
