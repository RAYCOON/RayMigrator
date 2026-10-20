-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_SelectOrphaned"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Selects orphaned MigrationRun entries (Running state with no FinishedAt).
Used by the Fix command to identify crashed migration processes.
"""

Behaviour = """
- Returns a result set of orphaned MigrationRun records
- Filters by ProductId and Environment
- Only returns entries with MigrationRunResultId=10 (Running) and FinishedAt IS NULL
- Calculates MinutesRunning from StartedAt to current UTC time
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
ProductId     = "INT | REQUIRED | Product ID from Product table"
EnvironmentId = "INT | REQUIRED | Environment ID from Environment table"

[ReturnValues]
ResultSet = "MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning"
================================================================================
*/

SELECT
    [Id] AS MigrationRunId,
    [EnvironmentId],
    [StartedAt],
    [MigrationRunModeId],
    DATEDIFF_BIG(SECOND, [StartedAt], SYSUTCDATETIME()) / 60 AS MinutesRunning,
    [LastHeartbeatAt],
    DATEDIFF_BIG(SECOND, COALESCE([LastHeartbeatAt], [StartedAt]), SYSUTCDATETIME()) / 60 AS MinutesSinceHeartbeat,
    [InitiatedBy],
    [Host]
FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
WHERE [ProductId] = @ProductId
    AND [EnvironmentId] = @EnvironmentId
    AND [MigrationRunResultId] = 10
    AND [FinishedAt] IS NULL
ORDER BY [StartedAt];
