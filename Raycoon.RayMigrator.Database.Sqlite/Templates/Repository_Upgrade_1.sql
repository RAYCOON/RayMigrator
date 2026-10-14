/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_1"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-14.1"

[Description]
Function = """
Schema version 1 (ADR-024): creates the 11 repository tables with their indexes, foreign
keys and lookup master data, and writes row 1 of the RepositorySchemaVersion table.
Runs on a repository that has no schema version yet.
"""

Behaviour = """
- Return value 1: schema version 1 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent per statement (CREATE TABLE IF NOT EXISTS, INSERT OR IGNORE) and atomic: the step
  runs in one IMMEDIATE transaction, a parallel start waits in the busy timeout of the connection
"""

[ConfigPlaceholders]
SchemaName    = "Not used - SQLite has no schemas"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
RayMigratorVersion = "TEXT | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1  = "1,RayMigrator repository schema version [1] applied: 11 tables with master data"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "Use datetime('now') for all audit timestamps (UTC, matches the CHECK constraints)"
Note4 = "Tables created: MigratorMeta, Product, Environment, MigrationRun, MigrationRunMeta, MigrationRecord, MigrationRecordHistory, MigrationRunMode, MigrationOperation, MigrationRunResult, MigrationStatus"
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

-- The repository DAL settings run templates without a transaction, so the step opens its own: a parallel start then
-- sees either no tables or the tables with their version row, never the tables alone.
BEGIN IMMEDIATE;

-- Lookup tables (no FK dependencies)
CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationOperation" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRunResult" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRunMode" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationStatus" (
    "Id"                   INTEGER      NOT NULL,
    "Name"                 TEXT         NOT NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigratorMeta" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "RayMigratorVersion"   TEXT         NOT NULL,
    "RepositoryDatabaseType" TEXT       NOT NULL,
    "CreatedAt"            TEXT         NOT NULL CHECK (datetime("CreatedAt") IS NOT NULL AND datetime("CreatedAt") = "CreatedAt")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}Product" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "Name"                 TEXT         NOT NULL,
    "NameLower"            TEXT         NOT NULL,
    "CreatedAt"            TEXT         NOT NULL CHECK (datetime("CreatedAt") IS NOT NULL AND datetime("CreatedAt") = "CreatedAt")
) STRICT;

CREATE UNIQUE INDEX IF NOT EXISTS "uix_{CFG:TableBaseName}Product_NameLower" ON "{CFG:TableBaseName}Product" ("NameLower");

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}Environment" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "Name"                 TEXT         NOT NULL,
    "NameLower"            TEXT         NOT NULL,
    "CreatedAt"            TEXT         NOT NULL CHECK (datetime("CreatedAt") IS NOT NULL AND datetime("CreatedAt") = "CreatedAt")
) STRICT;

CREATE UNIQUE INDEX IF NOT EXISTS "uix_{CFG:TableBaseName}Environment_NameLower" ON "{CFG:TableBaseName}Environment" ("NameLower");

-- Tables with FK dependencies (created after their referenced tables)
CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRun" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "MigratorMetaId"    INTEGER      NOT NULL,
    "ProductId"            INTEGER      NOT NULL,
    "EnvironmentId"        INTEGER      NOT NULL,
    "MigrationRunModeId"   INTEGER      NOT NULL,
    "MigrationRunResultId" INTEGER      NOT NULL,
    "FromReleaseVersion"   TEXT             NULL,
    "ToReleaseVersion"     TEXT             NULL,
    "StartedAt"            TEXT         NOT NULL CHECK (datetime("StartedAt") IS NOT NULL AND datetime("StartedAt") = "StartedAt"),
    "FinishedAt"           TEXT             NULL CHECK ("FinishedAt" IS NULL OR (datetime("FinishedAt") IS NOT NULL AND datetime("FinishedAt") = "FinishedAt")),
    "DurationInMs"         INTEGER          NULL,
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRun_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRun_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRun_MigratorMeta" FOREIGN KEY ("MigratorMetaId") REFERENCES "{CFG:TableBaseName}MigratorMeta"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRun_MigrationRunResult" FOREIGN KEY ("MigrationRunResultId") REFERENCES "{CFG:TableBaseName}MigrationRunResult"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRun_MigrationRunMode" FOREIGN KEY ("MigrationRunModeId") REFERENCES "{CFG:TableBaseName}MigrationRunMode"("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRunMeta" (
    "MigrationRunId"       INTEGER      NOT NULL,
    "MigrationRunSettingsJson" TEXT         NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("MigrationRunId"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRunMeta_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRecord" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "ProductId"            INTEGER      NOT NULL,
    "EnvironmentId"        INTEGER      NOT NULL,
    "MigrationRunId"       INTEGER      NOT NULL,
    "MigrationRunModeId"   INTEGER      NOT NULL,
    "MigrationOperationId" INTEGER      NOT NULL,
    "MigrationStatusId"    INTEGER      NOT NULL,
    "ReleaseVersion"       TEXT         NOT NULL,
    "TargetGroupAlias"     TEXT         NOT NULL,
    "TargetAlias"          TEXT         NOT NULL,
    "Filename"             TEXT         NOT NULL,
    "FileOrderId"          INTEGER      NOT NULL,
    "FileUpHash"           TEXT         NOT NULL,
    "FileUpConfigHash"     TEXT             NULL,
    "FileUpBlocksHash"     TEXT         NOT NULL,
    "FileUpBlocksMigrated" INTEGER      NOT NULL,
    "FileUpBlocksTotal"    INTEGER      NOT NULL,
    "FileUpConfigJson"     TEXT             NULL,
    "MigrateDownFileExists" INTEGER     NOT NULL,
    "FileDownHash"         TEXT             NULL,
    "FileDownConfigHash"   TEXT             NULL,
    "FileDownBlocksHash"   TEXT             NULL,
    "FileDownBlocksMigrated" INTEGER        NULL,
    "FileDownBlocksTotal"  INTEGER          NULL,
    "FileDownConfigJson"   TEXT             NULL,
    "StartedAt"            TEXT             NULL CHECK ("StartedAt" IS NULL OR (datetime("StartedAt") IS NOT NULL AND datetime("StartedAt") = "StartedAt")),
    "FinishedAt"           TEXT             NULL CHECK ("FinishedAt" IS NULL OR (datetime("FinishedAt") IS NOT NULL AND datetime("FinishedAt") = "FinishedAt")),
    "DurationInMs"         INTEGER          NULL,
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_MigrationRunMode" FOREIGN KEY ("MigrationRunModeId") REFERENCES "{CFG:TableBaseName}MigrationRunMode"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_MigrationOperation" FOREIGN KEY ("MigrationOperationId") REFERENCES "{CFG:TableBaseName}MigrationOperation"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecord_MigrationStatus" FOREIGN KEY ("MigrationStatusId") REFERENCES "{CFG:TableBaseName}MigrationStatus"("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRecordHistory" (
    "Id"                   INTEGER      NOT NULL PRIMARY KEY,
    "MigrationRecordId"          INTEGER      NOT NULL,
    "ProductId"            INTEGER      NOT NULL,
    "EnvironmentId"        INTEGER      NOT NULL,
    "MigrationRunId"       INTEGER      NOT NULL,
    "MigrationRunModeId"   INTEGER      NOT NULL,
    "MigrationOperationId" INTEGER      NOT NULL,
    "MigrationStatusId"    INTEGER      NOT NULL,
    "ReleaseVersion"       TEXT         NOT NULL,
    "TargetGroupAlias"     TEXT         NOT NULL,
    "TargetAlias"          TEXT         NOT NULL,
    "Filename"             TEXT         NOT NULL,
    "FileOrderId"          INTEGER      NOT NULL,
    "FileUpHash"           TEXT         NOT NULL,
    "FileUpConfigHash"     TEXT             NULL,
    "FileUpBlocksHash"     TEXT         NOT NULL,
    "FileUpBlocksMigrated" INTEGER      NOT NULL,
    "FileUpBlocksTotal"    INTEGER      NOT NULL,
    "FileUpConfigJson"     TEXT             NULL,
    "MigrateDownFileExists" INTEGER     NOT NULL,
    "FileDownHash"         TEXT             NULL,
    "FileDownConfigHash"   TEXT             NULL,
    "FileDownBlocksHash"   TEXT             NULL,
    "FileDownBlocksMigrated" INTEGER        NULL,
    "FileDownBlocksTotal"  INTEGER          NULL,
    "FileDownConfigJson"   TEXT             NULL,
    "StartedAt"            TEXT             NULL CHECK ("StartedAt" IS NULL OR (datetime("StartedAt") IS NOT NULL AND datetime("StartedAt") = "StartedAt")),
    "FinishedAt"           TEXT             NULL CHECK ("FinishedAt" IS NULL OR (datetime("FinishedAt") IS NOT NULL AND datetime("FinishedAt") = "FinishedAt")),
    "DurationInMs"         INTEGER          NULL,
    "HistorizedAt"         TEXT             NOT NULL  DEFAULT (datetime('now')) CHECK (datetime("HistorizedAt") IS NOT NULL AND datetime("HistorizedAt") = "HistorizedAt"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecordHistory_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecordHistory_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_{CFG:TableBaseName}MigrationRecordHistory_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id")
) STRICT;

CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRecordHistory" ON "{CFG:TableBaseName}MigrationRecordHistory" ("MigrationRecordId");

-- Master data (idempotent: existing rows are kept)
INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationRunMode" ("Id", "Name", "Description") VALUES
    (10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
    (20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
    (100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.');

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationOperation" ("Id", "Name", "Description") VALUES
    (5, 'Rollback', 'Performing Rollback of current MigrationRun'),
    (50, 'MigrateDown', 'Performing Down-Migration'),
    (100, 'MigrateUp', 'Performing Up-Migration'),
    (110, 'Baseline', 'Marking migration files as migrated without executing them (baseline command)');

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationRunResult" ("Id", "Name", "Description") VALUES
    (10, 'Running', 'Migration process is currently running'),
    (50, 'PartialSuccess', 'Migration(s) finished but at least one file was skipped or left Failed'),
    (80, 'Recovered', 'Migration(s) failed and the configured error recovery rolled back cleanly'),
    (90, 'Error', 'Migration(s) stopped due to error(s)'),
    (100, 'Ok', 'Migration(s) successfully executed');

INSERT OR IGNORE INTO "{CFG:TableBaseName}MigrationStatus" ("Id", "Name", "Description") VALUES
    (10, 'Pending', 'Record created, execution pending'),
    (20, 'Executing', 'SQL blocks are being executed'),
    (30, 'Failed', 'Execution failed, DB state unclear'),
    (50, 'NotMigrated', 'Not deployed / rolled back'),
    (100, 'Migrated', 'Successfully deployed');

INSERT OR IGNORE INTO "{CFG:TableBaseName}RepositorySchemaVersion" ("Version", "AppliedAt", "RayMigratorVersion")
VALUES (1, datetime('now'), @RayMigratorVersion);

COMMIT;

SELECT '1,RayMigrator repository schema version [1] applied: 11 tables with master data';
