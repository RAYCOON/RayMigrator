/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_3"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Schema version 3 (ADR-024): adds the run result Cancelled = 70 to the MigrationRunResult lookup
table, the indexes the readers of the repository filter by (runs per product, environment and
start, history rows per run and per time, records per product and environment), and writes
row 3 of the RepositorySchemaVersion table.
"""

Behaviour = """
- Return value 3: schema version 3 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: every row and index is created only when missing; the step runs in one IMMEDIATE
  transaction, so a failed step leaves nothing behind and a parallel start waits in the busy timeout
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
RayMigratorVersion = "TEXT | REQUIRED | The RayMigrator version applying the step (e.g., '0.20.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_3  = "3,RayMigrator repository schema version [3] applied: MigrationRunResult Cancelled and the reader indexes"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "The lookup row and the indexes are data and access paths only: rows of schema version 2 stay as they are"
Note4 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note5 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
================================================================================
*/

/*
 * Transaction requirement (DAL-024):
 * The engine runs this template without a transaction of the driver (the
 * repository, the database log writer and the retention set UseTransaction
 * to false), so each statement commits on its own unless the template
 * opens a transaction itself. Statements that share an invariant must open
 * BEGIN IMMEDIATE ... COMMIT or leave the decision to one statement. The
 * sqlite3 CLI needs the same: run the file as it is, without a transaction
 * around it ("sqlite3 db.sqlite < file.sql").
 *
 * This step opens BEGIN IMMEDIATE ... COMMIT itself, so it applies as a
 * whole or not at all and a parallel start waits for it in the busy
 * timeout; the DAL rolls it back when a statement before the COMMIT fails.
 */

-- The repository DAL settings run templates without a transaction, so the step opens its own.
BEGIN IMMEDIATE;

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationRunResult" ("Id", "Name", "Description") VALUES
    (70, 'Cancelled', 'Migration(s) stopped on request before completion');

CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt" ON "{CFG:TableBaseName}MigrationRun" ("ProductId", "EnvironmentId", "StartedAt");
CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRecordHistory_MigrationRunId" ON "{CFG:TableBaseName}MigrationRecordHistory" ("MigrationRunId");
CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRecordHistory_HistorizedAt" ON "{CFG:TableBaseName}MigrationRecordHistory" ("HistorizedAt");
CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRecord_Product_Environment" ON "{CFG:TableBaseName}MigrationRecord" ("ProductId", "EnvironmentId");

INSERT OR IGNORE INTO "{CFG:TableBaseName}RepositorySchemaVersion" ("Version", "AppliedAt", "RayMigratorVersion")
VALUES (3, datetime('now'), @RayMigratorVersion);

COMMIT;

SELECT '3,RayMigrator repository schema version [3] applied: MigrationRunResult Cancelled and the reader indexes';
