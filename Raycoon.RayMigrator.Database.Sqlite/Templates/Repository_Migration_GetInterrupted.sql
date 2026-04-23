/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_GetInterrupted"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Detects interrupted migrations that can be resumed from a specific block.
Used for recovery after process crash or unexpected termination.
"""

Behaviour = """
- Return value = 0: No interrupted migration found (logged at Debug level)
- Return value > 0: Interrupted MigrationId found - message contains recovery details
- Searches for migrations with: Pending or Executing status + incomplete blocks + no FinishedAt
- Returns most recent interrupted migration by FileOrderId
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
ProductId     = "INTEGER | REQUIRED | The product ID to check for interrupted migrations"
EnvironmentId = "INTEGER | REQUIRED | The environment ID to check (DEV, QA, PROD)"

[ReturnValues]
# Format: SELECT 'code,message' where message contains pipe-separated recovery data
Success_0_NotFound = "0,No interrupted migration found for ProductId [N] with EnvironmentId [E]"
Success_N_Found    = "N (MigrationId),MigrationId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "Pipe-separated message format is critical - code parses these fields"
Note3 = "Detection criteria: StatusId IN (10, 20) AND BlocksMigrated<BlocksTotal AND FinishedAt IS NULL"
Note4 = "Uses COALESCE with subquery instead of SELECT INTO (not supported in SQLite)"
================================================================================
*/

SELECT COALESCE(
    (SELECT CAST(m."Id" AS TEXT) || ','
         || CAST(m."Id" AS TEXT) || '|'
         || CAST(m."MigrationRunId" AS TEXT) || '|'
         || IFNULL(m."ReleaseVersion", '') || '|'
         || IFNULL(m."Filename", '') || '|'
         || CAST(m."FileUpBlocksMigrated" AS TEXT) || '|'
         || CAST(m."FileUpBlocksTotal" AS TEXT) || '|'
         || CAST(m."EnvironmentId" AS TEXT) || '|'
         || IFNULL(m."TargetGroupAlias", '') || '|'
         || IFNULL(m."TargetAlias", '')
     FROM "{CFG:TableBaseName}MigrationRecord" m
     INNER JOIN "{CFG:TableBaseName}MigrationRun" mr ON m."MigrationRunId" = mr."Id"
     WHERE m."ProductId" = @ProductId
       AND m."EnvironmentId" = @EnvironmentId
       AND m."MigrationStatusId" IN (10, 20)
       AND m."FileUpBlocksMigrated" < m."FileUpBlocksTotal"
       AND m."FinishedAt" IS NULL
     ORDER BY m."FileOrderId"
     LIMIT 1),
    '0,No interrupted migration found for ProductId [' || CAST(@ProductId AS TEXT) || '] with EnvironmentId [' || CAST(@EnvironmentId AS TEXT) || ']'
);
