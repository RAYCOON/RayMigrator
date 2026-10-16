/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Count"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.2"

[Description]
Function = """
Retention (ADR-027): reports whether the repository database has the log table under the schema
and prefix of the repository, and counts its rows created before a cutoff that do not belong to
an open run of the repository (MigrationRun FinishedAt IS NULL): the rows that
DatabaseLogging_Delete deletes. A log written elsewhere is missing here and left alone.
"""

Behaviour = """
- Second code 1: the log table exists; the first code N >= 0 counts its matching rows
- Second code 0: the log table does not exist under the schema and prefix of the repository (0,0)
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- Reads only; the count may exceed the int range of the result contract and is read as a long
- @ExistsOnly = 1 reports only whether the log table exists (0,1 or 0,0) and counts nothing: the cleanup
  asks it before it deletes, where a count of a large log would cost a scan; the dry run counts (0)
- PL/pgSQL plans the count only when it runs, so a missing table is no error
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database, schema and prefix
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
CutoffUtc  = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
ExistsOnly = "INTEGER | REQUIRED | 1 reports only whether the log table exists, without counting; 0 counts"

[ReturnValues]
# Format: RAISE NOTICE 'code,code,message'
Success_N    = "N,1,[N] MigrationLog rows created before [cutoff] can be deleted"
Success_None = "0,0,MigrationLog does not exist in the repository database under the schema and prefix of the repository"
Success_Exists = "0,1,MigrationLog exists in the repository database under the schema and prefix of the repository (not counted)"

[ModificationNotes]
Note1 = "Result format: 'rows,logTableExists,message' via RAISE NOTICE - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "The condition must stay the one of DatabaseLogging_Delete"
Note4 = "The cutoff is a UTC wall time: the offset +00 makes the cast read it as UTC whatever the session time zone (no AT TIME ZONE, DAL-012)"
Note5 = "DAL-017: All identifiers use unquoted snake_case"
Note6 = "Result via RAISE NOTICE: the DAL returns the last notice, so the result notice stays the last statement of the block"
================================================================================
*/

DO $$
DECLARE
    v_rows BIGINT;
BEGIN
    -- The log is cleaned only where it shares the repository database, schema and prefix; elsewhere the table is missing here.
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = '{CFG:SchemaName}'
          AND table_name = '{CFG:TableBaseName}migration_log'
    ) THEN
        RAISE NOTICE '0,0,MigrationLog does not exist in the repository database under the schema and prefix of the repository';
        RETURN;
    END IF;

    IF @ExistsOnly = 1 THEN
        RAISE NOTICE '0,1,MigrationLog exists in the repository database under the schema and prefix of the repository (not counted)';
        RETURN;
    END IF;

    SELECT COUNT(*) INTO v_rows
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_log x
    WHERE x.created_at < CAST(@CutoffUtc || '+00' AS TIMESTAMPTZ)
      AND NOT EXISTS (
          SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run r
          WHERE r.id = x.migration_run_id AND r.finished_at IS NULL);

    RAISE NOTICE '%,1,[%] MigrationLog rows created before [%] can be deleted', v_rows, v_rows, @CutoffUtc;
END $$;
