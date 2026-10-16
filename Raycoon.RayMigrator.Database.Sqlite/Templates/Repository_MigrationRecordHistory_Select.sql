/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Select"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Selects the terminal state transitions (MigrationRecordHistory rows) of all migration records of a
Product and Environment. Every row carries the MigrationRun that caused the transition, so the info run
history can attribute rolled-back records to the migrate-down or error-recovery run that rolled them back,
while the records themselves stay owned by the run that created them (#13).
"""

Behaviour = """
- Returns an empty result set if no history rows exist
- One row per terminal transition (Migrated, NotMigrated, Failed), ordered by history Id ascending
- Id is the MigrationRecordId (not the history row id), MigrationRunId is the run that caused the transition
- NOTE: This is a SELECT query, NOT the standard 'code,message' format
- Column contract is identical to Repository_MigrationRecord_Select so both are mapped by the same reader
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
ProductId          = "INT | REQUIRED | The product ID to query history rows for"
EnvironmentId      = "INT | REQUIRED | The environment ID to query history rows for"
MigrationRunModeId = "INTEGER | REQUIRED | Run mode (typically 100=Migrate)"

[ReturnValues]
# This template returns a result set, NOT the standard 'code,message' format
# Columns returned (in order):
#   Id (= MigrationRecordId), ProductId, MigrationRunId, MigrationOperationId, MigrationStatusId,
#   ReleaseVersion, TargetGroupAlias, TargetAlias, Filename, FileOrderId,
#   FileUpHash, FileUpConfigHash, FileUpBlocksHash, FileUpBlocksMigrated, FileUpBlocksTotal,
#   MigrateDownFileExists, FileDownHash, FileDownConfigHash, FileDownBlocksHash,
#   FileDownBlocksMigrated, FileDownBlocksTotal, StartedAt, FinishedAt

[ModificationNotes]
Note1 = "This template returns a result set - NOT the standard 'code,message' format"
Note2 = "Column names are the reader contract - keep them identical to Repository_MigrationRecord_Select"
Note3 = "ORDER BY the history row id: the last row of a record within a run is its final state in that run"
Note4 = "StartedAt and FinishedAt are the times of the record when the transition was historized (UTC)"
================================================================================
*/

SELECT
    "MigrationRecordId" AS "Id"
    ,"ProductId"
    ,"MigrationRunId"
    ,"MigrationOperationId"
    ,"MigrationStatusId"
    ,"ReleaseVersion"
    ,"TargetGroupAlias"
    ,"TargetAlias"
    ,"Filename"
    ,"FileOrderId"
    ,"FileUpHash"
    ,"FileUpConfigHash"
    ,"FileUpBlocksHash"
    ,"FileUpBlocksMigrated"
    ,"FileUpBlocksTotal"
    ,"MigrateDownFileExists"
    ,"FileDownHash"
    ,"FileDownConfigHash"
    ,"FileDownBlocksHash"
    ,"FileDownBlocksMigrated"
    ,"FileDownBlocksTotal"
    ,"StartedAt"
    ,"FinishedAt"
FROM
    "{CFG:TableBaseName}MigrationRecordHistory"
WHERE
    "ProductId" = @ProductId AND
    "EnvironmentId" = @EnvironmentId AND
    "MigrationRunModeId" = @MigrationRunModeId
ORDER BY
    "Id" ASC;
