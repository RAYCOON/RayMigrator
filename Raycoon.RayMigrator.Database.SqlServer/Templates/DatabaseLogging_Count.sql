/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Count"
DatabaseType   = "SqlServer"
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
- The statement on the log table is compiled only when it runs (deferred name resolution), so a missing table is no error
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database, schema and prefix
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc  = "NVARCHAR | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
ExistsOnly = "INT | REQUIRED | 1 reports only whether the log table exists, without counting; 0 counts"

[ReturnValues]
# Format: SELECT 'code,code,message'
Success_N    = "N,1,[N] MigrationLog rows created before [cutoff] can be deleted"
Success_None = "0,0,MigrationLog does not exist in the repository database under the schema and prefix of the repository"
Success_Exists = "0,1,MigrationLog exists in the repository database under the schema and prefix of the repository (not counted)"

[ModificationNotes]
Note1 = "SELECT result format: 'rows,logTableExists,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "COUNT_BIG: the count may exceed the int range"
Note4 = "The condition must stay the one of DatabaseLogging_Delete"
================================================================================
*/

SET NOCOUNT ON;

-- The log is cleaned only where it shares the repository database, schema and prefix; elsewhere the table is missing here.
IF OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationLog', N'U') IS NULL
BEGIN
    SELECT '0,0,MigrationLog does not exist in the repository database under the schema and prefix of the repository';
    RETURN;
END;

IF @ExistsOnly = 1
BEGIN
    SELECT '0,1,MigrationLog exists in the repository database under the schema and prefix of the repository (not counted)';
    RETURN;
END;

DECLARE @Rows BIGINT = (
    SELECT COUNT_BIG(*)
    FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] AS x
    WHERE x.[CreatedAt] < CONVERT(DATETIME2(3), @CutoffUtc, 120)
        AND NOT EXISTS (
            SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] AS r
            WHERE r.[Id] = x.[MigrationRunId] AND r.[FinishedAt] IS NULL));

SELECT CAST(@Rows AS VARCHAR(20)) + ',1,[' + CAST(@Rows AS VARCHAR(20)) + '] MigrationLog rows created before [' + @CutoffUtc + '] can be deleted';
