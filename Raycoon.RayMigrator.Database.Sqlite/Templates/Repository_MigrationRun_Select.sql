/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Select"
DatabaseType   = "Sqlite"
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
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId = "INTEGER | REQUIRED | The product ID to query MigrationRuns for"
Limit     = "INTEGER | REQUIRED | Maximum number of records to return"

[ReturnValues]
# This template returns a result set, NOT the standard 'code,message' format
# Columns returned (in order):
#   Id, ProductId, MigrationRunModeId, MigrationRunResultId, EnvironmentId,
#   FromReleaseVersion, ToReleaseVersion, StartedAt, FinishedAt, DurationInMs

[ModificationNotes]
Note1 = "This template returns a result set - NOT the standard 'code,message' format"
Note2 = "Column order is critical - code depends on positional binding"
Note3 = "Use LIMIT @Limit to limit results (SQLite syntax)"
================================================================================
*/

SELECT
    "Id"
    ,"ProductId"
    ,"MigrationRunModeId"
    ,"MigrationRunResultId"
    ,"EnvironmentId"
    ,"FromReleaseVersion"
    ,"ToReleaseVersion"
    ,"StartedAt"
    ,"FinishedAt"
    ,"DurationInMs"
FROM
    "{CFG:TableBaseName}MigrationRun"
WHERE
    "ProductId" = @ProductId
ORDER BY
    "StartedAt" DESC
LIMIT @Limit;
