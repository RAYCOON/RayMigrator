/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Select"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Selects MigrationRun records for a specific Product, ordered by most recent first.
Used to display migration history for a product.
"""

Behaviour = """
- Returns NULL/empty result set if no MigrationRuns found
- Returns multiple rows with MigrationRun details if found
- NOTE: This is a SELECT query, NOT the standard 'code,message' format
- Results are ordered by StartedAt DESC (most recent first)
- Limit parameter controls maximum number of records returned
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId = "INT | REQUIRED | The product ID to query MigrationRuns for"
Limit     = "INT | REQUIRED | Maximum number of records to return"

[ReturnValues]
# This template returns a result set, NOT the standard 'code,message' format
# Columns returned (in order):
#   Id, ProductId, MigrationRunModeId, MigrationRunResultId, EnvironmentId,
#   FromReleaseVersion, ToReleaseVersion, StartedAt, FinishedAt, DurationInMs

[ModificationNotes]
Note1 = "This template returns a result set - NOT the standard 'code,message' format"
Note2 = "Column order is critical - code depends on positional binding"
Note3 = "DAL-018: Storage uses snake_case identifiers; output columns are aliased to PascalCase (AS \`...\`) to preserve the cross-engine row[\"PascalCase\"] reader contract"
================================================================================
*/

SELECT
    id                       AS `Id`
    ,product_id              AS `ProductId`
    ,migration_run_mode_id   AS `MigrationRunModeId`
    ,migration_run_result_id AS `MigrationRunResultId`
    ,environment_id          AS `EnvironmentId`
    ,from_release_version    AS `FromReleaseVersion`
    ,to_release_version      AS `ToReleaseVersion`
    ,started_at              AS `StartedAt`
    ,finished_at             AS `FinishedAt`
    ,duration_in_ms          AS `DurationInMs`
FROM
    {CFG:TableBaseName}migration_run
WHERE
    product_id = @ProductId
ORDER BY
    started_at DESC
LIMIT @Limit;
