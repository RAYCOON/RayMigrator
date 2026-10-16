/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Delete"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Retention (ADR-027): deletes one batch of MigrationRecordHistory rows historized before a
cutoff, the oldest first, and spares every row of a run that is still open (MigrationRun
FinishedAt IS NULL): a running or orphaned run keeps its history until it is closed.
MigrationRecord, MigrationRun and MigrationRunMeta are never touched. IRepositoryMaintenanceService
runs the template batch by batch until a batch deletes fewer rows than @BatchSize.
"""

Behaviour = """
- Return value N >= 0: N rows were deleted; fewer than @BatchSize means no matching row is left
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- One DELETE in autocommit (the repository DAL settings open no transaction): the database applies
  it whole or not at all, also when the call is cancelled
- Reads the index on historized_at of schema version 3 and the primary key of migration_run
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MySQL schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
CutoffUtc = "VARCHAR | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows historized before it match"
BatchSize = "INT | REQUIRED | The most rows the batch deletes (at least 1)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,Deleted [N] MigrationRecordHistory rows historized before [cutoff]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of deleted rows"
Note2 = "No commas allowed in messages"
Note3 = "Keep the batch one DELETE statement: a cancelled statement (KILL QUERY) rolls back whole, while a second one would commit on its own"
Note4 = "The cutoff text is read in the session time zone, which the DAL sets to UTC (DAL-014)"
Note5 = "DAL-018: All identifiers use unquoted snake_case"
================================================================================
*/

-- The oldest rows first through the index on historized_at; a row of an open run stays whatever its age. The cutoff text
-- is read in the session time zone, which the DAL sets to UTC.
DELETE FROM {CFG:TableBaseName}migration_record_history
WHERE historized_at < @CutoffUtc
  AND NOT EXISTS (
      SELECT 1 FROM {CFG:TableBaseName}migration_run r
      WHERE r.id = {CFG:TableBaseName}migration_record_history.migration_run_id AND r.finished_at IS NULL)
ORDER BY historized_at
LIMIT @BatchSize;

SET @v_deleted = ROW_COUNT();

SELECT CONCAT(CAST(@v_deleted AS CHAR), ',Deleted [', CAST(@v_deleted AS CHAR), '] MigrationRecordHistory rows historized before [', @CutoffUtc, ']');
