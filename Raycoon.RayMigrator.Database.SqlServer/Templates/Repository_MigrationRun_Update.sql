-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Update"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-01-29.1"

[Description]
Function = """
Updates an existing MigrationRun with the final result status and completion time.
Calculates duration in milliseconds from StartedAt to FinishedAt.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Sets FinishedAt timestamp and calculates DurationInMs
- Called at the end of a migration run regardless of success/failure
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
MigrationRunId    = "INT | REQUIRED | The ID of the MigrationRun to update"
MigrationRunResultId = "TINYINT | REQUIRED | Final result: 10=Running, 90=Error, 100=Ok"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRunId),MigrationRun with Id [N] successfully updated with result [M]"
Error_-30_NotFound = "-30,MigrationRun with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for FinishedAt timestamp"
Note4 = "DurationInMs calculated as DATEDIFF(MILLISECOND, StartedAt, FinishedAt)"
Note5 = "MigrationRunResultId values: 10=Running, 90=Error, 100=Ok"
Note6 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    -- Verify the MigrationRun exists
    IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] WITH(UPDLOCK, HOLDLOCK) WHERE Id = @MigrationRunId)
    BEGIN
        SELECT '-30,MigrationRun with Id [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + '] does not exist';
        RETURN;
    END;

    -- Update the MigrationRun with completion data
    UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
    SET
        MigrationRunResultId = @MigrationRunResultId,
        FinishedAt = SYSUTCDATETIME(),
        DurationInMs = DATEDIFF(MILLISECOND, StartedAt, SYSUTCDATETIME())
    WHERE Id = @MigrationRunId;

    SELECT CAST(@MigrationRunId AS VARCHAR(10)) + ',MigrationRun with Id [' + CAST(@MigrationRunId AS VARCHAR(10)) + '] successfully updated with result [' + CAST(@MigrationRunResultId AS VARCHAR(10)) + ']';

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
