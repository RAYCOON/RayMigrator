/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-09-09.1"

[Description]
Function = """
Checks for repository existence and completeness. Creates RayMigrator
infrastructure on the target database if necessary. Returns the VersionId.
"""

Behaviour = """
- Return value >= 0: Success (logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Creates all 11 repository tables with master data
- Inserts a MigratorMeta row on the first run of each RayMigrator version (the first row is the version that created the repository)
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
RayMigratorVersion     = "TEXT | REQUIRED | The RayMigrator application version (e.g., '3.0.0')"
RepositoryDatabaseType = "TEXT | REQUIRED | The database type for the repository (e.g., 'Sqlite')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N           = "N (VersionId),RayMigrator repository already exists. Using VersionId [N]."
Success_N_Created   = "N (VersionId),RayMigrator repository-tables with master data and new VersionId [N] successfully created"
Success_N_NewVer    = "N (VersionId),RayMigrator repository already exists. New VersionId [N] created."
Error_-10_Incomplete        = "-10,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of [11]."
Error_-11_PartialNoVersion  = "-11,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of the expected amount of [0]."
Error_-12_MultipleVersions  = "-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [...] RepositoryDatabaseType [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for all timestamps"
Note4 = "MigratorMeta lists the RayMigrator versions that used the repository; the first row is the version that created it and therefore identifies the schema. There is no RepositoryVersion constant and no in-place upgrade."
Note5 = "SQLite DDL is transactional - but we use IF NOT EXISTS for idempotency"
Note6 = "Master data is inserted only when the repository is created in this run (gated on _rc_state.pre_version_table = 0)"
Note7 = "Tables must be created in FK dependency order"
Note8 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note9 = "Uses temp table _rc_state to store intermediate state since SQLite has no session variables"
Note10 = "DAL-021: All TEXT datetime columns carry a strict-ISO-8601 CHECK constraint validating round-trip through datetime()"
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

-- Store pre-DDL state in temp table
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT) STRICT;
DELETE FROM "_rc_state";

INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('pre_table_count', CAST((SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN (
        '{CFG:TableBaseName}MigratorMeta',
        '{CFG:TableBaseName}Product',
        '{CFG:TableBaseName}Environment',
        '{CFG:TableBaseName}MigrationRun',
        '{CFG:TableBaseName}MigrationRunMeta',
        '{CFG:TableBaseName}MigrationRecord',
        '{CFG:TableBaseName}MigrationRecordHistory',
        '{CFG:TableBaseName}MigrationRunMode',
        '{CFG:TableBaseName}MigrationOperation',
        '{CFG:TableBaseName}MigrationRunResult',
        '{CFG:TableBaseName}MigrationStatus'
    )) AS TEXT)),
    ('pre_version_table', CAST((SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{CFG:TableBaseName}MigratorMeta') AS TEXT));

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
    CONSTRAINT "fk_MigrationRun_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_MigrationRun_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id"),
    CONSTRAINT "fk_MigrationRun_MigratorMeta" FOREIGN KEY ("MigratorMetaId") REFERENCES "{CFG:TableBaseName}MigratorMeta"("Id"),
    CONSTRAINT "fk_MigrationRun_MigrationRunResult" FOREIGN KEY ("MigrationRunResultId") REFERENCES "{CFG:TableBaseName}MigrationRunResult"("Id"),
    CONSTRAINT "fk_MigrationRun_MigrationRunMode" FOREIGN KEY ("MigrationRunModeId") REFERENCES "{CFG:TableBaseName}MigrationRunMode"("Id")
) STRICT;

CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}MigrationRunMeta" (
    "MigrationRunId"       INTEGER      NOT NULL,
    "MigrationRunSettingsJson" TEXT         NULL,
    "Description"          TEXT             NULL,
    PRIMARY KEY ("MigrationRunId"),
    CONSTRAINT "fk_MigrationRunMeta_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id")
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
    CONSTRAINT "fk_MigrationRecord_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_MigrationRecord_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id"),
    CONSTRAINT "fk_MigrationRecord_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id"),
    CONSTRAINT "fk_MigrationRecord_MigrationRunMode" FOREIGN KEY ("MigrationRunModeId") REFERENCES "{CFG:TableBaseName}MigrationRunMode"("Id"),
    CONSTRAINT "fk_MigrationRecord_MigrationOperation" FOREIGN KEY ("MigrationOperationId") REFERENCES "{CFG:TableBaseName}MigrationOperation"("Id"),
    CONSTRAINT "fk_MigrationRecord_MigrationStatus" FOREIGN KEY ("MigrationStatusId") REFERENCES "{CFG:TableBaseName}MigrationStatus"("Id")
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
    CONSTRAINT "fk_MigrationRecordHistory_MigrationRun" FOREIGN KEY ("MigrationRunId") REFERENCES "{CFG:TableBaseName}MigrationRun"("Id"),
    CONSTRAINT "fk_MigrationRecordHistory_Product" FOREIGN KEY ("ProductId") REFERENCES "{CFG:TableBaseName}Product"("Id"),
    CONSTRAINT "fk_MigrationRecordHistory_Environment" FOREIGN KEY ("EnvironmentId") REFERENCES "{CFG:TableBaseName}Environment"("Id")
) STRICT;

CREATE INDEX IF NOT EXISTS "ix_{CFG:TableBaseName}MigrationRecordHistory" ON "{CFG:TableBaseName}MigrationRecordHistory" ("MigrationRecordId");

-- Master data: written only when the repository did not exist before this run. Existing repositories are not
-- upgraded in place; the schema of a repository is the schema of the RayMigrator version that created it.
INSERT INTO "{CFG:TableBaseName}MigrationRunMode" ("Id", "Name", "Description")
SELECT "column1", "column2", "column3" FROM (VALUES
    (10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
    (20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
    (100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.')
)
WHERE (SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') = '0';

INSERT INTO "{CFG:TableBaseName}MigrationOperation" ("Id", "Name", "Description")
SELECT "column1", "column2", "column3" FROM (VALUES
    (5, 'Rollback', 'Performing Rollback of current MigrationRun'),
    (50, 'MigrateDown', 'Performing Down-Migration'),
    (100, 'MigrateUp', 'Performing Up-Migration'),
    (110, 'Baseline', 'Marking migration files as migrated without executing them (baseline command)')
)
WHERE (SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') = '0';

INSERT INTO "{CFG:TableBaseName}MigrationRunResult" ("Id", "Name", "Description")
SELECT "column1", "column2", "column3" FROM (VALUES
    (10, 'Running', 'Migration process is currently running'),
    (50, 'PartialSuccess', 'Migration(s) finished but at least one file was skipped or left Failed'),
    (80, 'Recovered', 'Migration(s) failed and the configured error recovery rolled back cleanly'),
    (90, 'Error', 'Migration(s) stopped due to error(s)'),
    (100, 'Ok', 'Migration(s) successfully executed')
)
WHERE (SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') = '0';

INSERT INTO "{CFG:TableBaseName}MigrationStatus" ("Id", "Name", "Description")
SELECT "column1", "column2", "column3" FROM (VALUES
    (10, 'Pending', 'Record created, execution pending'),
    (20, 'Executing', 'SQL blocks are being executed'),
    (30, 'Failed', 'Execution failed, DB state unclear'),
    (50, 'NotMigrated', 'Not deployed / rolled back'),
    (100, 'Migrated', 'Successfully deployed')
)
WHERE (SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') = '0';

-- Version logic: Insert version if not exists
INSERT INTO "{CFG:TableBaseName}MigratorMeta"
    ("RayMigratorVersion", "RepositoryDatabaseType", "CreatedAt")
SELECT @RayMigratorVersion, @RepositoryDatabaseType, datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM "{CFG:TableBaseName}MigratorMeta"
    WHERE "RayMigratorVersion" = @RayMigratorVersion
      AND "RepositoryDatabaseType" = @RepositoryDatabaseType
);

-- Capture version state
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('version_count', CAST((SELECT COUNT(*) FROM "{CFG:TableBaseName}MigratorMeta"
        WHERE "RayMigratorVersion" = @RayMigratorVersion
          AND "RepositoryDatabaseType" = @RepositoryDatabaseType) AS TEXT)),
    ('version_id', (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}MigratorMeta"
        WHERE "RayMigratorVersion" = @RayMigratorVersion
          AND "RepositoryDatabaseType" = @RepositoryDatabaseType
        LIMIT 1)),
    ('post_table_count', CAST((SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN (
        '{CFG:TableBaseName}MigratorMeta',
        '{CFG:TableBaseName}Product',
        '{CFG:TableBaseName}Environment',
        '{CFG:TableBaseName}MigrationRun',
        '{CFG:TableBaseName}MigrationRunMeta',
        '{CFG:TableBaseName}MigrationRecord',
        '{CFG:TableBaseName}MigrationRecordHistory',
        '{CFG:TableBaseName}MigrationRunMode',
        '{CFG:TableBaseName}MigrationOperation',
        '{CFG:TableBaseName}MigrationRunResult',
        '{CFG:TableBaseName}MigrationStatus'
    )) AS TEXT));

-- Final result
SELECT CASE
    -- Repository existed before but is incomplete/corrupt
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') AS INTEGER) > 0
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='post_table_count') AS INTEGER) != 11 THEN
        '-10,RayMigrator repository incomplete or corrupt. Repository contains ['
        || (SELECT "val" FROM "_rc_state" WHERE "key"='post_table_count')
        || '] tables instead of [11].'

    -- Repository exists and matching version found
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') AS INTEGER) > 0
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='version_count') AS INTEGER) = 1
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_table_count') AS INTEGER) = 11 THEN
        (SELECT "val" FROM "_rc_state" WHERE "key"='version_id')
        || ',RayMigrator repository already exists. Using VersionId ['
        || (SELECT "val" FROM "_rc_state" WHERE "key"='version_id') || '].'

    -- Repository exists but version was just inserted (new version)
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') AS INTEGER) > 0
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_table_count') AS INTEGER) = 11 THEN
        (SELECT "val" FROM "_rc_state" WHERE "key"='version_id')
        || ',RayMigrator repository already exists. New VersionId ['
        || (SELECT "val" FROM "_rc_state" WHERE "key"='version_id') || '] created.'

    -- Repository exists but multiple matching versions (error)
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') AS INTEGER) > 0
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='version_count') AS INTEGER) > 1 THEN
        '-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [' || IFNULL(@RayMigratorVersion, 'NULL')
        || '] RepositoryDatabaseType [' || IFNULL(@RepositoryDatabaseType, 'NULL') || '].'

    -- No version table but some tables exist (corrupt - before DDL ran)
    WHEN CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_version_table') AS INTEGER) = 0
         AND CAST((SELECT "val" FROM "_rc_state" WHERE "key"='pre_table_count') AS INTEGER) != 0 THEN
        '-11,RayMigrator repository incomplete or corrupt. Repository contains ['
        || (SELECT "val" FROM "_rc_state" WHERE "key"='pre_table_count')
        || '] tables instead of the expected amount of [0].'

    -- No repository existed - everything was just created
    ELSE
        (SELECT "val" FROM "_rc_state" WHERE "key"='version_id')
        || ',RayMigrator repository-tables with master data and new VersionId ['
        || (SELECT "val" FROM "_rc_state" WHERE "key"='version_id') || '] successfully created'
END;

DROP TABLE IF EXISTS "_rc_state";
