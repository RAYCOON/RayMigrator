/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_2"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Schema version 2 (ADR-024): adds the run details to MigrationRun (operation, source, initiator,
host, error code and message, last heartbeat) and the MigrationRunSource lookup table
(Cli = 10, Ci = 20, Studio = 30), and writes row 2 of the RepositorySchemaVersion table.
"""

Behaviour = """
- Return value 2: schema version 2 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Atomic: the step runs in one IMMEDIATE transaction, so a failed step leaves nothing behind and the
  engine runs it again on the next command; a parallel start waits in the busy timeout
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
RayMigratorVersion = "TEXT | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_2  = "2,RayMigrator repository schema version [2] applied: MigrationRun run details and MigrationRunSource"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "New MigrationRun columns are nullable: rows of schema version 1 keep NULL"
Note4 = "ALTER TABLE ADD COLUMN has no IF NOT EXISTS in SQLite; the transaction makes the step all-or-nothing instead, and a parallel start that lost the race fails on the first ALTER TABLE: TemplateExecutor re-reads the version and accepts the step as applied"
Note5 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note6 = "The last statement must be the SELECT: the DAL returns the first column of the last result set"
================================================================================
*/

/*
 * Transaction requirement (DAL-024):
 * This template contains multiple statements that must execute atomically.
 * The DalSqlite driver wraps execution in a transaction when UseTransaction
 * is enabled (the default), so in-framework use is safe. Manual execution
 * via the sqlite3 CLI (e.g., "sqlite3 db.sqlite < file.sql") must be
 * wrapped in "BEGIN TRANSACTION; ... COMMIT;" to guarantee atomicity.
 */

-- The repository DAL settings run templates without a transaction, so the step opens its own.
BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRunSource" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationRunSource" ("Id", "Name", "Description") VALUES
    (10, 'Cli', 'Started by the RayMigrator command line on a workstation or server'),
    (20, 'Ci', 'Started by the RayMigrator command line in a CI/CD pipeline'),
    (30, 'Studio', 'Started by RayMigrator Studio');

ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "MigrationOperationId" INTEGER NULL REFERENCES "{CFG:TableBaseName}MigrationOperation"("Id");
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "MigrationRunSourceId" INTEGER NULL REFERENCES "{CFG:TableBaseName}MigrationRunSource"("Id");
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "InitiatedBy" TEXT NULL;
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "InitiatedBySubject" TEXT NULL;
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "Host" TEXT NULL;
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "ErrorCode" TEXT NULL;
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "ErrorMessage" TEXT NULL;
ALTER TABLE "{CFG:TableBaseName}MigrationRun" ADD COLUMN "LastHeartbeatAt" TEXT NULL CHECK ("LastHeartbeatAt" IS NULL OR (datetime("LastHeartbeatAt") IS NOT NULL AND datetime("LastHeartbeatAt") = "LastHeartbeatAt"));

INSERT OR IGNORE INTO "{CFG:TableBaseName}RepositorySchemaVersion" ("Version", "AppliedAt", "RayMigratorVersion")
VALUES (2, datetime('now'), @RayMigratorVersion);

COMMIT;

SELECT '2,RayMigrator repository schema version [2] applied: MigrationRun run details and MigrationRunSource';
