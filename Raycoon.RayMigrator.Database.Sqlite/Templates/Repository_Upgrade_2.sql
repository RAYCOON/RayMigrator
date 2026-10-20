/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_2"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

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
