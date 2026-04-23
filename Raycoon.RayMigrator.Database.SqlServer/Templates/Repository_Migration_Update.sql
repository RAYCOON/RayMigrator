-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_Update"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Updates a Migration record with block progress or final status.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- FinishedAt and DurationInMs only set when MigrationStatusId indicates completion (IN 100, 50, 30)
- Can be called multiple times during migration to update block progress
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
MigrationId         = "INT | REQUIRED | The Migration record ID to update"
MigrationStatusId   = "TINYINT | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileUpBlocksMigrated= "INT | REQUIRED | Number of blocks successfully executed"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationId),Migration with Id [N] updated: Block [X] - Status [S]"
Error_-40_NotFound = "-40,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for FinishedAt timestamp"
Note4 = "FinishedAt only set for terminal MigrationStatusId IN (100, 50, 30) - Migrated, NotMigrated, Failed"
Note5 = "MigrationStatusId values: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
Note6 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    -- Update the Migration record
    UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    SET
        MigrationStatusId = @MigrationStatusId,
        FileUpBlocksMigrated = @FileUpBlocksMigrated,
        FinishedAt = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN SYSUTCDATETIME()
            ELSE FinishedAt
        END,
        DurationInMs = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN DATEDIFF(MILLISECOND, StartedAt, SYSUTCDATETIME())
            ELSE DurationInMs
        END
    WHERE Id = @MigrationId;

    DECLARE @v_affected INT = @@ROWCOUNT;

    IF (@v_affected = 0)
    BEGIN
        SELECT '-40,Migration with Id [' + COALESCE(CAST(@MigrationId AS VARCHAR(10)), 'NULL') + '] does not exist';
        RETURN;
    END;

    -- Historize terminal state changes (100=Migrated, 50=NotMigrated, 30=Failed)
    IF (@MigrationStatusId IN (100, 50, 30))
    BEGIN
        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory]
        (
            MigrationRecordId, ProductId, EnvironmentId, MigrationRunId, MigrationRunModeId, MigrationOperationId,
            MigrationStatusId, ReleaseVersion, TargetGroupAlias, TargetAlias,
            Filename, FileOrderId, FileUpHash, FileUpConfigHash, FileUpBlocksHash,
            FileUpBlocksMigrated, FileUpBlocksTotal, FileUpConfigJson, MigrateDownFileExists,
            FileDownHash, FileDownConfigHash, FileDownBlocksHash, FileDownBlocksMigrated,
            FileDownBlocksTotal, FileDownConfigJson, StartedAt, FinishedAt, DurationInMs,
            HistorizedAt
        )
        SELECT
            Id, ProductId, EnvironmentId, MigrationRunId, MigrationRunModeId, MigrationOperationId,
            MigrationStatusId, ReleaseVersion, TargetGroupAlias, TargetAlias,
            Filename, FileOrderId, FileUpHash, FileUpConfigHash, FileUpBlocksHash,
            FileUpBlocksMigrated, FileUpBlocksTotal, FileUpConfigJson, MigrateDownFileExists,
            FileDownHash, FileDownConfigHash, FileDownBlocksHash, FileDownBlocksMigrated,
            FileDownBlocksTotal, FileDownConfigJson, StartedAt, FinishedAt, DurationInMs,
            SYSUTCDATETIME()
        FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
        WHERE Id = @MigrationId;
    END

    SELECT CAST(@MigrationId AS VARCHAR(10)) + ',Migration with Id [' + CAST(@MigrationId AS VARCHAR(10)) + '] updated: Block [' + CAST(@FileUpBlocksMigrated AS VARCHAR(10)) + '] - Status [' + CAST(@MigrationStatusId AS VARCHAR(10)) + ']';

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
