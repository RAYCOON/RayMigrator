/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Count"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Retention (ADR-027): counts the MigrationRecordHistory rows historized before a cutoff that do
not belong to an open run (MigrationRun FinishedAt IS NULL): the rows that
Repository_MigrationRecordHistory_Delete deletes. Used for the dry run of a cleanup; deletes nothing.
"""

Behaviour = """
- Return value N >= 0: N rows are older than the cutoff and do not belong to an open run
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- Reads only; the count may exceed the int range of the result contract and is read as a long
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
CutoffUtc = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows historized before it match"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,[N] MigrationRecordHistory rows historized before [cutoff] can be deleted"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of matching rows"
Note2 = "No commas allowed in messages"
Note3 = "The condition must stay the one of Repository_MigrationRecordHistory_Delete"
Note4 = "The cutoff is a UTC wall time: the offset +00 makes the cast read it as UTC whatever the session time zone (no AT TIME ZONE, DAL-012)"
Note5 = "DAL-017: All identifiers use unquoted snake_case"
================================================================================
*/

SELECT CAST(COUNT(*) AS TEXT) || ',[' || CAST(COUNT(*) AS TEXT) || '] MigrationRecordHistory rows historized before [' || @CutoffUtc || '] can be deleted'
FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history x
WHERE x.historized_at < CAST(@CutoffUtc || '+00' AS TIMESTAMPTZ)
  AND NOT EXISTS (
      SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_run r
      WHERE r.id = x.migration_run_id AND r.finished_at IS NULL);
