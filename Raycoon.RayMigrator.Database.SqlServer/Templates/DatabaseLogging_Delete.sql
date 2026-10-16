/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Delete"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Retention (ADR-027): deletes one batch of MigrationLog rows created before a cutoff, the oldest
first, and spares every row of a run of the repository that is still open (MigrationRun
FinishedAt IS NULL); a row without a run (MigrationRunId NULL) falls by its age. Runs against
the repository database under the schema and prefix of the repository, the default home of the
log, and only after DatabaseLogging_Count found the log table there. IRepositoryMaintenanceService
runs the template batch by batch until a batch deletes fewer rows than @BatchSize.
"""

Behaviour = """
- Return value N >= 0: N rows were deleted; fewer than @BatchSize means no matching row is left
- Return value < 0: Error (logged at Error level, the cleanup is aborted)
- One DELETE in autocommit (the repository DAL settings open no transaction): the database applies
  it whole or not at all, also when the call is cancelled
- Reads the clustered index in key order, stops after @BatchSize matching rows, and seeks the primary key of MigrationRun
"""

[ConfigPlaceholders]
# Values come from the Repository section, not from DatabaseLogging: the log is cleaned only where it
# shares the repository database, schema and prefix
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
CutoffUtc = "NVARCHAR | REQUIRED | The cutoff in UTC as 'yyyy-MM-dd HH:mm:ss'; rows created before it match"
BatchSize = "INT | REQUIRED | The most rows the batch deletes (at least 1)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N = "N,Deleted [N] MigrationLog rows created before [cutoff]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code is the number of deleted rows"
Note2 = "No commas allowed in messages"
Note3 = "Keep the batch one DELETE statement: a cancelled statement rolls back whole, while a second one would commit on its own"
Note4 = "The DELETE takes a key lock per row and index; the default batch size of the engine stays below the 5000 locks at which SQL Server escalates it to a table lock, which would hold back the log inserts of running hosts"
Note5 = "Joins MigrationRun of the repository: the open runs of another repository that writes into the same log table are not protected"
Note6 = "ORDER BY Id with OPTION (LOOP JOIN) keeps the batch streaming: ordered by CreatedAt the optimizer reads every matching row per batch (measured on 1000000 rows)"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Deleted INT;

-- The oldest rows first, in the order of the clustered key, which is the order in which the rows were written and so
-- of CreatedAt; a row of an open run stays whatever its age. In key order with nested loops the scan stops after the
-- rows of the batch (the last batch of a cleanup reads the younger rows to the end of the table). Ordered by
-- CreatedAt, SQL Server read every matching row for each batch (an index intersection or a scan, then a sort), and a
-- cleanup grew with the square of its rows.
WITH [batch] AS (
    SELECT TOP (@BatchSize) x.[Id]
    FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] AS x
    WHERE x.[CreatedAt] < CONVERT(DATETIME2(3), @CutoffUtc, 120)
        AND NOT EXISTS (
            SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] AS r
            WHERE r.[Id] = x.[MigrationRunId] AND r.[FinishedAt] IS NULL)
    ORDER BY x.[Id]
)
DELETE FROM [batch]
OPTION (LOOP JOIN);

SET @Deleted = @@ROWCOUNT;

SELECT CAST(@Deleted AS VARCHAR(10)) + ',Deleted [' + CAST(@Deleted AS VARCHAR(10)) + '] MigrationLog rows created before [' + @CutoffUtc + ']';
