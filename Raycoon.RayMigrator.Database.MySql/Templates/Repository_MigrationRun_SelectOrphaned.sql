/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_SelectOrphaned"
DatabaseType   = "MySql"
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
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId     = "INT | REQUIRED | Product ID from Product table"
EnvironmentId = "INT | REQUIRED | Environment ID from Environment table"

[ReturnValues]
ResultSet = "MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning"

[ModificationNotes]
Note1 = "DAL-018: Storage uses snake_case identifiers; output columns are aliased to PascalCase (AS \`...\`) to preserve the cross-engine row[\"PascalCase\"] reader contract"
================================================================================
*/

SELECT
    id                    AS `MigrationRunId`,
    environment_id        AS `EnvironmentId`,
    started_at            AS `StartedAt`,
    migration_run_mode_id AS `MigrationRunModeId`,
    TIMESTAMPDIFF(MINUTE, started_at, CURRENT_TIMESTAMP) AS `MinutesRunning`
FROM {CFG:TableBaseName}migration_run
WHERE product_id = @ProductId
    AND environment_id = @EnvironmentId
    AND migration_run_result_id = 10
    AND finished_at IS NULL
ORDER BY started_at;
