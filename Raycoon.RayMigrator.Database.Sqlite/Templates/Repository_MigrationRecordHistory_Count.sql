/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecordHistory_Count"
DatabaseType   = "Sqlite"
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
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc = "TEXT | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows historized before it match"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,[N] MigrationRecordHistory rows historized before [cutoff] can be deleted"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of matching rows"
Note2 = "No commas allowed in messages"
Note3 = "The condition must stay the one of Repository_MigrationRecordHistory_Delete"
Note4 = "The cutoff text compares with the ISO-8601 text of the column (datetime('now') format, DAL-021)"
================================================================================
*/

SELECT CAST(COUNT(*) AS TEXT) || ',[' || CAST(COUNT(*) AS TEXT) || '] MigrationRecordHistory rows historized before [' || @CutoffUtc || '] can be deleted'
FROM "{CFG:TableBaseName}MigrationRecordHistory" AS x
WHERE x."HistorizedAt" < @CutoffUtc
  AND NOT EXISTS (
      SELECT 1 FROM "{CFG:TableBaseName}MigrationRun" AS r
      WHERE r."Id" = x."MigrationRunId" AND r."FinishedAt" IS NULL);
