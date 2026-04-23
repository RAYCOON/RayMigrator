-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_GetInterrupted"
DatabaseType   = "SqlServer"
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
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
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
Note5 = "ORDER BY FileOrderId ensures consistent recovery order"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    DECLARE @MigrationId INT;
    DECLARE @MigrationRunId INT;
    DECLARE @ReleaseVersion NVARCHAR(100);
    DECLARE @Filename NVARCHAR(200);
    DECLARE @FileUpBlocksMigrated INT;
    DECLARE @FileUpBlocksTotal INT;
    DECLARE @FoundEnvironmentId INT;
    DECLARE @TargetGroupAlias NVARCHAR(100);
    DECLARE @TargetAlias NVARCHAR(100);

    -- Find the most recent interrupted migration:
    -- Migration with status Pending (10) or Executing (20) that has incomplete blocks
    -- Indicates a migration that was interrupted mid-execution
    SELECT TOP 1
        @MigrationId = m.Id,
        @MigrationRunId = m.MigrationRunId,
        @ReleaseVersion = m.ReleaseVersion,
        @Filename = m.Filename,
        @FileUpBlocksMigrated = m.FileUpBlocksMigrated,
        @FileUpBlocksTotal = m.FileUpBlocksTotal,
        @FoundEnvironmentId = m.EnvironmentId,
        @TargetGroupAlias = m.TargetGroupAlias,
        @TargetAlias = m.TargetAlias
    FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] m
    INNER JOIN [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] mr ON m.MigrationRunId = mr.Id
    WHERE
        m.ProductId = @ProductId
        AND m.EnvironmentId = @EnvironmentId
        AND m.MigrationStatusId IN (10, 20)
        AND m.FileUpBlocksMigrated < m.FileUpBlocksTotal
        AND m.FinishedAt IS NULL
    ORDER BY m.FileOrderId;

    IF @MigrationId IS NULL
    BEGIN
        -- No interrupted migration found
        SELECT '0,No interrupted migration found for ProductId [' + CAST(@ProductId AS VARCHAR(10)) + '] with EnvironmentId [' + CAST(@EnvironmentId AS VARCHAR(10)) + ']';
        RETURN;
    END;

    -- Return the interrupted migration details in a parseable format
    SELECT CAST(@MigrationId AS VARCHAR(10)) + ',' +
           CAST(@MigrationId AS VARCHAR(10)) + '|' +
           CAST(@MigrationRunId AS VARCHAR(10)) + '|' +
           COALESCE(@ReleaseVersion, '') + '|' +
           COALESCE(@Filename, '') + '|' +
           CAST(@FileUpBlocksMigrated AS VARCHAR(10)) + '|' +
           CAST(@FileUpBlocksTotal AS VARCHAR(10)) + '|' +
           CAST(@FoundEnvironmentId AS VARCHAR(10)) + '|' +
           COALESCE(@TargetGroupAlias, '') + '|' +
           COALESCE(@TargetAlias, '');

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
