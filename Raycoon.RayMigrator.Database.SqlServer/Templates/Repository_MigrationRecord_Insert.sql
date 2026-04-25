-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Insert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Creates a new Migration record or resets an existing archived record
to track individual migration file execution.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- When @ExistingMigrationRecordId = 0: INSERT a new record (original behaviour)
- When @ExistingMigrationRecordId > 0: UPDATE the existing record, resetting all fields
- Return value >= 0: Success (MigrationRecordId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- FileUpBlocksMigrated starts at 0, incremented as blocks execute
- StartedAt timestamp recorded immediately
- UPDATE resets all FileDown* fields to NULL
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
ExistingMigrationRecordId = "INT | REQUIRED | 0 = INSERT new record, >0 = UPDATE existing record with this ID"
ProductId           = "INT | REQUIRED | Product ID from Product table"
MigrationRunId      = "INT | REQUIRED | Parent MigrationRun ID"
MigrationRunModeId  = "TINYINT | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigrationOperationId= "TINYINT | REQUIRED | Operation: 5=Rollback, 50=MigrateDown, 100=MigrateUp"
MigrationStatusId   = "TINYINT | REQUIRED | Initial status (should be 10=Pending)"
EnvironmentId       = "INT | REQUIRED | Environment ID from Environment table"
ReleaseVersion      = "NVARCHAR(100) | REQUIRED | Release version from folder path"
TargetGroupAlias    = "NVARCHAR(100) | REQUIRED | Target group alias from folder path"
TargetAlias         = "NVARCHAR(100) | REQUIRED | Target alias (specific database)"
Filename            = "NVARCHAR(200) | REQUIRED | Migration filename without path"
FileOrderId         = "INT | REQUIRED | Execution order (based on sorted path)"
FileUpHash          = "VARCHAR(100) | REQUIRED | SHA256 hash of entire file"
FileUpConfigHash    = "VARCHAR(100) | OPTIONAL | SHA256 hash of TOML config section"
FileUpBlocksHash    = "VARCHAR(100) | REQUIRED | SHA256 hash of SQL content blocks"
FileUpBlocksTotal   = "INT | REQUIRED | Total number of GO-separated blocks"
FileUpConfigJson    = "NVARCHAR(MAX) | OPTIONAL | JSON of parsed TOML configuration"
MigrateDownFileExists = "BIT | REQUIRED | 1 if .down.sql file exists, 0 otherwise"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created = "N (MigrationRecordId),Migration record with Id [N] successfully created for file [Filename]"
Success_Updated = "N (MigrationRecordId),Migration record with Id [N] successfully reset for file [Filename]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for StartedAt timestamp"
Note4 = "FileUpBlocksMigrated initialized to 0 - updated by Repository_MigrationRecord_Update"
Note5 = "MigrationStatusId values: 10=Pending, 20=Executing, 30=Failed, 50=NotMigrated, 100=Migrated"
Note6 = "MigrationOperationId values: 5=Rollback, 50=MigrateDown, 100=MigrateUp"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    DECLARE @MigrationRecordId INT;

    IF @ExistingMigrationRecordId > 0
    BEGIN
        -- Reset existing archived record
        UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
        SET
            MigrationRunId          = @MigrationRunId,
            MigrationRunModeId      = @MigrationRunModeId,
            MigrationOperationId    = @MigrationOperationId,
            MigrationStatusId       = @MigrationStatusId,
            FileOrderId             = @FileOrderId,
            FileUpHash              = @FileUpHash,
            FileUpConfigHash        = @FileUpConfigHash,
            FileUpBlocksHash        = @FileUpBlocksHash,
            FileUpBlocksMigrated    = 0,
            FileUpBlocksTotal       = @FileUpBlocksTotal,
            FileUpConfigJson        = @FileUpConfigJson,
            MigrateDownFileExists   = @MigrateDownFileExists,
            FileDownHash            = NULL,
            FileDownConfigHash      = NULL,
            FileDownBlocksHash      = NULL,
            FileDownBlocksMigrated  = NULL,
            FileDownBlocksTotal     = NULL,
            FileDownConfigJson      = NULL,
            StartedAt               = SYSUTCDATETIME(),
            FinishedAt              = NULL,
            DurationInMs            = NULL
        WHERE Id = @ExistingMigrationRecordId;

        SET @MigrationRecordId = @ExistingMigrationRecordId;

        SELECT CAST(@MigrationRecordId AS VARCHAR(10)) + ',Migration record with Id [' + CAST(@MigrationRecordId AS VARCHAR(10)) + '] successfully reset for file [' + COALESCE(@Filename, 'NULL') + ']';
    END
    ELSE
    BEGIN
        -- Insert new record
        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
        (
            ProductId,
            EnvironmentId,
            MigrationRunId,
            MigrationRunModeId,
            MigrationOperationId,
            MigrationStatusId,
            ReleaseVersion,
            TargetGroupAlias,
            TargetAlias,
            Filename,
            FileOrderId,
            FileUpHash,
            FileUpConfigHash,
            FileUpBlocksHash,
            FileUpBlocksMigrated,
            FileUpBlocksTotal,
            FileUpConfigJson,
            MigrateDownFileExists,
            StartedAt
        )
        VALUES
        (
            @ProductId,
            @EnvironmentId,
            @MigrationRunId,
            @MigrationRunModeId,
            @MigrationOperationId,
            @MigrationStatusId,
            @ReleaseVersion,
            @TargetGroupAlias,
            @TargetAlias,
            @Filename,
            @FileOrderId,
            @FileUpHash,
            @FileUpConfigHash,
            @FileUpBlocksHash,
            0,  -- FileUpBlocksMigrated starts at 0
            @FileUpBlocksTotal,
            @FileUpConfigJson,
            @MigrateDownFileExists,
            SYSUTCDATETIME()
        );

        SET @MigrationRecordId = SCOPE_IDENTITY();

        SELECT CAST(@MigrationRecordId AS VARCHAR(10)) + ',Migration record with Id [' + CAST(@MigrationRecordId AS VARCHAR(10)) + '] successfully created for file [' + COALESCE(@Filename, 'NULL') + ']';
    END

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
