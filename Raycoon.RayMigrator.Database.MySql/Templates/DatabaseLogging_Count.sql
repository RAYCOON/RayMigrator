/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Count"
DatabaseType   = "MySql"
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
- The count runs through PREPARE because the table may not exist: a statement on a missing table fails when it runs
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database and prefix
SchemaName    = "Not used - MySQL schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
CutoffUtc  = "VARCHAR | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
ExistsOnly = "INT | REQUIRED | 1 reports only whether the log table exists, without counting; 0 counts"

[ReturnValues]
# Format: SELECT 'code,code,message'
Success_N    = "N,1,[N] MigrationLog rows created before [cutoff] can be deleted"
Success_None = "0,0,MigrationLog does not exist in the repository database under the prefix of the repository"
Success_Exists = "0,1,MigrationLog exists in the repository database under the prefix of the repository (not counted)"

[ModificationNotes]
Note1 = "SELECT result format: 'rows,logTableExists,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "The condition must stay the one of DatabaseLogging_Delete"
Note4 = "The prepared statement reads the cutoff from @v_cutoff: a parameter inside the statement text would be substituted into a string literal"
Note5 = "DAL-018: All identifiers use unquoted snake_case"
================================================================================
*/

-- The log is cleaned only where it shares the repository database and prefix; elsewhere the table is missing here.
SET @v_log_exists = (
    SELECT COUNT(*) FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_log'
);
SET @v_cutoff = @CutoffUtc;
SET @v_rows = 0;

SET @v_sql = IF(@v_log_exists = 1 AND @ExistsOnly = 0,
    'SELECT COUNT(*) INTO @v_rows FROM {CFG:TableBaseName}migration_log x WHERE x.created_at < @v_cutoff AND NOT EXISTS (SELECT 1 FROM {CFG:TableBaseName}migration_run r WHERE r.id = x.migration_run_id AND r.finished_at IS NULL)',
    'SET @v_rows = 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SELECT IF(@v_log_exists = 1,
    IF(@ExistsOnly = 1,
        '0,1,MigrationLog exists in the repository database under the prefix of the repository (not counted)',
        CONCAT(CAST(@v_rows AS CHAR), ',1,[', CAST(@v_rows AS CHAR), '] MigrationLog rows created before [', @v_cutoff, '] can be deleted')),
    '0,0,MigrationLog does not exist in the repository database under the prefix of the repository');
