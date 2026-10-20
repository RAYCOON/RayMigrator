/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Delete"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
CutoffUtc = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows historized before it match"
BatchSize = "INT | REQUIRED | The most rows the batch deletes (at least 1)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,Deleted [N] MigrationRecordHistory rows historized before [cutoff]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of deleted rows"
Note2 = "No commas allowed in messages"
Note3 = "Keep the batch one statement: a cancelled statement rolls back whole; the DELETE stays in a data-modifying CTE so that the count needs no DO block"
Note4 = "The cutoff is a UTC wall time: the offset +00 makes the cast read it as UTC whatever the session time zone (no AT TIME ZONE, DAL-012)"
Note5 = "DAL-017: All identifiers use unquoted snake_case"
================================================================================
*/

-- The oldest rows first through the index on historized_at; a row of an open run stays whatever its age. One statement:
-- the batch selects the ids, the DELETE removes them, the SELECT counts what it removed.
WITH batch AS (
    SELECT x.id
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history x
    WHERE x.historized_at < CAST(@CutoffUtc || '+00' AS TIMESTAMPTZ)
      AND NOT EXISTS (
          SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run r
          WHERE r.id = x.migration_run_id AND r.finished_at IS NULL)
    ORDER BY x.historized_at
    LIMIT @BatchSize
), deleted AS (
    DELETE FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history
    WHERE id IN (SELECT id FROM batch)
    RETURNING id
)
SELECT CAST(COUNT(*) AS TEXT) || ',Deleted [' || CAST(COUNT(*) AS TEXT) || '] MigrationRecordHistory rows historized before [' || @CutoffUtc || ']'
FROM deleted;
