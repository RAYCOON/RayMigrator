-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_FixOrphaned"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Marks a single orphaned MigrationRun as Error with FinishedAt set.
Used by the Fix command to clean up crashed migration processes.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRunId returned)
- Return value < 0: Error (record not found or not in Running state)
- Updates MigrationRunResultId to 90 (Error) and sets FinishedAt
- Calculates DurationInMs from StartedAt to current UTC time
- Only updates if MigrationRunResultId=10 (Running) and FinishedAt IS NULL
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the orphaned MigrationRun to fix"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Fixed     = "N (MigrationRunId),MigrationRun [N] marked as Error (orphaned run cleanup)"
Error_-31_NotInRunning = "-31,MigrationRun [N] not found or not in Running state"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET NOCOUNT ON;

UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
SET
    [MigrationRunResultId] = 90,
    [FinishedAt] = SYSUTCDATETIME(),
    [DurationInMs] = DATEDIFF(MILLISECOND, [StartedAt], SYSUTCDATETIME())
WHERE [Id] = @MigrationRunId
    AND [MigrationRunResultId] = 10
    AND [FinishedAt] IS NULL;

IF @@ROWCOUNT = 0
    SELECT '-31,MigrationRun [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + '] not found or not in Running state';
ELSE
    SELECT CAST(@MigrationRunId AS VARCHAR(10)) + ',MigrationRun [' + CAST(@MigrationRunId AS VARCHAR(10)) + '] marked as Error (orphaned run cleanup)';
