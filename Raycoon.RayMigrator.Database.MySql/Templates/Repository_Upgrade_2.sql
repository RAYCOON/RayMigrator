/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_2"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Schema version 2 (ADR-024): adds the run details to migration_run (operation, source, initiator,
host, error code and message, last heartbeat) and the migration_runSource lookup table
(Cli = 10, Ci = 20, Studio = 30), and writes row 2 of the repository_schema_version table.
"""

Behaviour = """
- Return value 2: schema version 2 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent per statement, because MySQL DDL causes implicit commits: the columns are added through a
  prepared statement only when information_schema does not list them; serialised with GET_LOCK()
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MySQL schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_2  = "2,RayMigrator repository schema version [2] applied: migration_run run details and migration_run_source"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "New migration_run columns are nullable: rows of schema version 1 keep NULL"
Note4 = "The ALTER runs through PREPARE/EXECUTE because MySQL has no portable ADD COLUMN IF NOT EXISTS; the no-op branch is DO 0, never a SELECT, so the final SELECT stays the only result set"
Note5 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note6 = "DAL-018: All identifiers use unquoted snake_case"
================================================================================
*/

SET @v_lock_name = CONCAT('raymigrator_schema_', DATABASE());
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

CREATE TABLE IF NOT EXISTS {CFG:TableBaseName}migration_run_source (
    id                     TINYINT UNSIGNED NOT NULL,
    name                   VARCHAR(100)     NOT NULL,
    description            VARCHAR(1000)        NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT IGNORE INTO {CFG:TableBaseName}migration_run_source (id, name, description) VALUES
    (10, 'Cli', 'Started by the RayMigrator command line on a workstation or server'),
    (20, 'Ci', 'Started by the RayMigrator command line in a CI/CD pipeline'),
    (30, 'Studio', 'Started by RayMigrator Studio');

-- The columns and their foreign keys in one ALTER, which InnoDB applies atomically; only when the first column is missing.
SET @v_sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_run' AND COLUMN_NAME = 'migration_operation_id') = 0,
    'ALTER TABLE {CFG:TableBaseName}migration_run
        ADD COLUMN migration_operation_id  TINYINT UNSIGNED NULL,
        ADD COLUMN migration_run_source_id TINYINT UNSIGNED NULL,
        ADD COLUMN initiated_by            VARCHAR(200)     NULL,
        ADD COLUMN initiated_by_subject    VARCHAR(200)     NULL,
        ADD COLUMN host                    VARCHAR(200)     NULL,
        ADD COLUMN error_code              VARCHAR(100)     NULL,
        ADD COLUMN error_message           TEXT             NULL,
        ADD COLUMN last_heartbeat_at       TIMESTAMP        NULL,
        ADD CONSTRAINT fk_migration_run_migration_operation FOREIGN KEY (migration_operation_id) REFERENCES {CFG:TableBaseName}migration_operation(id),
        ADD CONSTRAINT fk_migration_run_migration_run_source FOREIGN KEY (migration_run_source_id) REFERENCES {CFG:TableBaseName}migration_run_source(id)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

INSERT IGNORE INTO {CFG:TableBaseName}repository_schema_version (version, applied_at, raymigrator_version)
VALUES (2, CURRENT_TIMESTAMP, @RayMigratorVersion);

DO RELEASE_LOCK(@v_lock_name);

SELECT '2,RayMigrator repository schema version [2] applied: migration_run run details and migration_run_source';
