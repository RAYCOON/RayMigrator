-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Insert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new MigrationRun record to track a migration session.
Prevents parallel migrations for the same Product/Environment/RunMode.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Checks for existing unfinished MigrationRun before inserting
- Parallel migrations for same Product/Environment/RunMode are prevented
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
ProductId          = "INT | REQUIRED | Product ID from Product table (obtained via Repository_Product_CheckInsert)"
EnvironmentId      = "INT | REQUIRED | Environment ID from Environment table (obtained via Repository_Environment_CheckInsert)"
MigrationRunModeId = "TINYINT | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigratorMetaId     = "INT | REQUIRED | Version ID from MigratorMeta table (obtained via Repository_CheckCreate)"
MigrationRunResultId  = "TINYINT | REQUIRED | Initial result status (should be 10=Running)"
FromReleaseVersion = "NVARCHAR(100) | OPTIONAL | Starting release version for the migration range"
ToReleaseVersion   = "NVARCHAR(100) | OPTIONAL | Target release version for the migration range"
MigrationRunSettingsJson = "NVARCHAR(MAX) | REQUIRED | JSON snapshot of all RayMigrator settings at migration start"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created   = "N (MigrationRunId),MigrationRun with Id [N] successfully created for ProductId [M]"
Error_-2_Parallel = "-2,MigrationRun for Product [Name] with Id [N] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=100] are not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for StartedAt timestamp"
Note4 = "Parallel migration check uses FinishedAt IS NULL to detect running migrations"
Note5 = "MigrationRunResultId values: 10=Running, 90=Error, 100=Ok"
Note6 = "ResultCode convention: >= 0 = Success, -1 = General template error, -2 = Migration already active (parallel run)"
================================================================================
*/

-- Mandatory RepositoryVersion: DO NOT change manually, otherwise repository-inconsistencies may occur that results in migration errors !!!

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    BEGIN TRANSACTION;

        IF EXISTS
            (
                SELECT TOP (1) 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] WITH (UPDLOCK, HOLDLOCK)
                WHERE
                    [ProductId] = @ProductId AND
                    [EnvironmentId] = @EnvironmentId AND
                    [MigrationRunModeId] = @MigrationRunModeId AND
                    [FinishedAt] IS NULL
            )
            BEGIN
                DECLARE @ProductName NVARCHAR(100);
                SELECT @ProductName = [Name] FROM [{CFG:SchemaName}].[{CFG:TableBaseName}Product] WHERE [Id] = @ProductId;

                ROLLBACK TRANSACTION;
                SELECT '-2,MigrationRun for Product [' + COALESCE(@ProductName, 'NULL') + '] with Id [' + COALESCE(CAST(@ProductId AS VARCHAR(10)), 'NULL') + '] is currently in progress. Parallel migrations for the same product with MigrationRunModeId [Migrate=' + COALESCE(CAST(@MigrationRunModeId AS VARCHAR(10)), 'NULL') + '] are not allowed!';
                RETURN;
            END;

        DECLARE @MigrationRunId INT;

        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] (MigratorMetaId, ProductId, EnvironmentId, MigrationRunModeId, MigrationRunResultId, FromReleaseVersion, ToReleaseVersion, StartedAt)
        VALUES (@MigratorMetaId, @ProductId, @EnvironmentId, @MigrationRunModeId, @MigrationRunResultId, @FromReleaseVersion, @ToReleaseVersion, SYSUTCDATETIME());

        SET @MigrationRunId = SCOPE_IDENTITY();

        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMeta] (MigrationRunId, MigrationRunSettingsJson)
        VALUES (@MigrationRunId, @MigrationRunSettingsJson);

    COMMIT TRANSACTION;

    SELECT CAST(@MigrationRunId AS VARCHAR(10)) + ',MigrationRun with Id [' + CAST(@MigrationRunId AS VARCHAR(10)) + '] successfully created for ProductId [' + COALESCE(CAST(@ProductId AS VARCHAR(10)), 'NULL') + ']';

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
