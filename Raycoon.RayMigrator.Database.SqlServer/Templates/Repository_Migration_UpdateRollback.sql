-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_UpdateRollback"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Updates a Migration record with rollback (FileDown*) fields and status.
Used during rollback execution to track rollback file metadata and block progress.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Updates FileDown* fields with rollback file hashes and block progress
- FinishedAt and DurationInMs only set when MigrationStatusId indicates completion (IN 100, 50, 30)
- Can be called multiple times during rollback to update block progress
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
MigrationId            = "INT | REQUIRED | The Migration record ID to update"
MigrationStatusId      = "TINYINT | REQUIRED | Status: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
FileDownHash           = "VARCHAR(100) | REQUIRED | SHA256 hash of the rollback file"
FileDownConfigHash     = "VARCHAR(100) | OPTIONAL | SHA256 hash of TOML config in rollback file"
FileDownBlocksHash     = "VARCHAR(100) | REQUIRED | SHA256 hash of SQL blocks in rollback file"
FileDownBlocksMigrated = "INT | REQUIRED | Number of rollback blocks successfully executed"
FileDownBlocksTotal    = "INT | REQUIRED | Total number of rollback blocks"
FileDownConfigJson     = "NVARCHAR(MAX) | OPTIONAL | JSON of parsed TOML config from rollback file"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationId),Migration with Id [N] rollback updated: Block [X/Y] - State [S] - Result [R]"
Error_-1_NotFound = "-1,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for FinishedAt timestamp"
Note4 = "FinishedAt only set for terminal MigrationStatusId IN (100, 50, 30) - Migrated, NotMigrated, Failed"
Note5 = "MigrationStatusId values: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    -- Verify the Migration exists
    IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] WHERE Id = @MigrationId)
    BEGIN
        SELECT '-1,Migration with Id [' + COALESCE(CAST(@MigrationId AS VARCHAR(10)), 'NULL') + '] does not exist';
        RETURN;
    END;

    -- Update the Migration record with rollback fields
    UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    SET
        MigrationStatusId = @MigrationStatusId,
        FileDownHash = @FileDownHash,
        FileDownConfigHash = @FileDownConfigHash,
        FileDownBlocksHash = @FileDownBlocksHash,
        FileDownBlocksMigrated = @FileDownBlocksMigrated,
        FileDownBlocksTotal = @FileDownBlocksTotal,
        FileDownConfigJson = @FileDownConfigJson,
        FinishedAt = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN SYSUTCDATETIME()
            ELSE FinishedAt
        END,
        DurationInMs = CASE
            WHEN @MigrationStatusId IN (100, 50, 30) THEN DATEDIFF(MILLISECOND, StartedAt, SYSUTCDATETIME())
            ELSE DurationInMs
        END
    WHERE Id = @MigrationId;

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

    SELECT CAST(@MigrationId AS VARCHAR(10)) + ',Migration with Id [' + CAST(@MigrationId AS VARCHAR(10)) + '] rollback updated: Block [' + CAST(@FileDownBlocksMigrated AS VARCHAR(10)) + '/' + CAST(@FileDownBlocksTotal AS VARCHAR(10)) + '] - Status [' + CAST(@MigrationStatusId AS VARCHAR(10)) + ']';

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
