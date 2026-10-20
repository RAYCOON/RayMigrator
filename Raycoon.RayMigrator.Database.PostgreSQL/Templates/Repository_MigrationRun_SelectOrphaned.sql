/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_SelectOrphaned"
DatabaseType   = "PostgreSQL"
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
LastHeartbeatAt      = "When the run last reported that it was alive; NULL for a run that never wrote a heartbeat"
MinutesSinceHeartbeat = "Minutes since the last heartbeat, or since the start without one: the age the orphan rule compares (schema version 2)"
InitiatedBy          = "Who started the run, when the row says"
Host                 = "The machine the run executed on, when the row says"

[ModificationNotes]
Note1 = "DAL-017: Storage uses snake_case identifiers; output columns are aliased to PascalCase (AS \"...\") to preserve the cross-engine row[\"PascalCase\"] reader contract"
================================================================================
*/

SELECT
    id                     AS "MigrationRunId",
    environment_id         AS "EnvironmentId",
    started_at             AS "StartedAt",
    migration_run_mode_id  AS "MigrationRunModeId",
    EXTRACT(EPOCH FROM (NOW() - started_at)) / 60 AS "MinutesRunning",
    last_heartbeat_at      AS "LastHeartbeatAt",
    EXTRACT(EPOCH FROM (NOW() - COALESCE(last_heartbeat_at, started_at))) / 60 AS "MinutesSinceHeartbeat",
    initiated_by           AS "InitiatedBy",
    host                   AS "Host"
FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run
WHERE product_id = @ProductId
    AND environment_id = @EnvironmentId
    AND migration_run_result_id = 10
    AND finished_at IS NULL
ORDER BY started_at;
