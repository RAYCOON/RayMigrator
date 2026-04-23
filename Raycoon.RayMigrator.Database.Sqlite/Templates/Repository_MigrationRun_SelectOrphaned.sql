/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_SelectOrphaned"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId     = "INTEGER | REQUIRED | Product ID from Product table"
EnvironmentId = "INTEGER | REQUIRED | Environment ID from Environment table"

[ReturnValues]
ResultSet = "MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning"
================================================================================
*/

SELECT
    "Id" AS MigrationRunId,
    "EnvironmentId",
    "StartedAt",
    "MigrationRunModeId",
    CAST((julianday('now') - julianday("StartedAt")) * 1440 AS INTEGER) AS MinutesRunning
FROM "{CFG:TableBaseName}MigrationRun"
WHERE "ProductId" = @ProductId
    AND "EnvironmentId" = @EnvironmentId
    AND "MigrationRunResultId" = 10
    AND "FinishedAt" IS NULL
ORDER BY "StartedAt";
