/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Reports the schema version of the repository (ADR-024). Creates the repository_schema_version
table when it does not exist; creates nothing else. The engine applies the missing
Repository_Upgrade_{n} steps afterwards.
"""

Behaviour = """
- First code: highest applied schema version, 0 when the repository has none
- Second code: 1 when the migrator_meta table exists, 0 otherwise. With version 0 this
  means a repository of a RayMigrator before 0.17.0, which the engine refuses
- Idempotent (CREATE TABLE IF NOT EXISTS); takes the schema lock of the steps (GET_LOCK), so a
  parallel start waits for a step another process is applying
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MariaDB schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MariaDB (DAL-018)"

[Parameters]
# none

[ReturnValues]
# Format: SELECT 'schemaVersion,unversionedTables,message'
Success_N        = "N (schema version),0|1,RayMigrator repository has schema version [N]"
Success_Legacy   = "0,1,RayMigrator repository tables exist without a schema version"
Success_None     = "0,0,RayMigrator repository does not exist yet"
Error_-32_Lock   = "-32,RayMigrator could not acquire the repository schema lock within 60 seconds"

[ModificationNotes]
Note1 = "SELECT result format: 'schemaVersion,unversionedTables,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Creates only the repository_schema_version table; the repository tables come from Repository_Upgrade_1"
Note4 = "MariaDB DDL causes an implicit commit; the statement is idempotent instead of transactional"
Note5 = "DAL-018: All identifiers use unquoted snake_case"
Note6 = "A pre-0.17.0 repository is one with rows in migrator_meta and no version row; an empty migrator_meta left by a step 1 that failed midway is completed by rerunning the step (the row count is read through PREPARE because the table may not exist)"
Note7 = "Without GET_LOCK() within 60 seconds the result is -32"
================================================================================
*/

-- The schema lock of the Repository_Upgrade_{n} steps: a check that runs while another process applies a step waits for
-- the step to finish, so it never sees the tables without the version row.
SET @v_lock_name = CONCAT('raymigrator_schema_', DATABASE());
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}repository_schema_version (
    version                INT          NOT NULL,
    applied_at             TIMESTAMP    NOT NULL,
    raymigrator_version    VARCHAR(100) NOT NULL,
    PRIMARY KEY (version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET @v_schema_version = (SELECT IFNULL(MAX(version), 0) FROM {CFG:TableBaseName}repository_schema_version);

-- A repository of a RayMigrator before 0.17.0 has rows in migrator_meta and no version row. A step 1 that lost its
-- connection midway (DDL commits implicitly here) may have created migrator_meta, but empty: that repository is
-- completed by rerunning the step, not refused. The row count is read through PREPARE because the table may not exist.
SET @v_meta_table_exists = (
    SELECT COUNT(*) FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migrator_meta'
);
SET @v_sql = IF(@v_meta_table_exists = 1,
    'SELECT COUNT(*) INTO @v_meta_rows FROM {CFG:TableBaseName}migrator_meta',
    'SET @v_meta_rows = 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;
SET @v_unversioned_tables = IF(@v_schema_version = 0 AND @v_meta_rows > 0, 1, 0);

DO RELEASE_LOCK(@v_lock_name);

SELECT IF(IFNULL(@v_lock_acquired, 0) = 1,
    CONCAT(CAST(@v_schema_version AS CHAR), ',', CAST(@v_unversioned_tables AS CHAR), ',',
        CASE
            WHEN @v_schema_version > 0 THEN CONCAT('RayMigrator repository has schema version [', CAST(@v_schema_version AS CHAR), ']')
            WHEN @v_unversioned_tables = 1 THEN 'RayMigrator repository tables exist without a schema version'
            ELSE 'RayMigrator repository does not exist yet'
        END),
    '-32,RayMigrator could not acquire the repository schema lock within 60 seconds');
