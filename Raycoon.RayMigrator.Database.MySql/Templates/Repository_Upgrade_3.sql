/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_3"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Schema version 3 (ADR-024): adds the run result Cancelled = 70 to the migration_run_result lookup
table, the indexes the readers of the repository filter by (runs per product, environment and
start, history rows per run and per time, records per product and environment), and writes
row 3 of the repository_schema_version table.
"""

Behaviour = """
- Return value 3: schema version 3 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent per statement, because MySQL DDL causes implicit commits: each index is created through a
  prepared statement only when information_schema does not list it; serialised with GET_LOCK()
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MySQL schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.20.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_3  = "3,RayMigrator repository schema version [3] applied: migration_run_result cancelled and the reader indexes"
Error_-32  = "-32,RayMigrator could not acquire the repository schema lock within 60 seconds"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "The lookup row and the indexes are data and access paths only: rows of schema version 2 stay as they are"
Note4 = "The indexes run through PREPARE/EXECUTE because MySQL has no CREATE INDEX IF NOT EXISTS; the no-op branch is DO 0, never a SELECT, so the final SELECT stays the only result set"
Note5 = "An index is created only under the lock: a run that could not acquire it reports -32 and changes no index"
Note6 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note7 = "DAL-018: All identifiers use unquoted snake_case"
================================================================================
*/

SET @v_lock_name = CONCAT('raymigrator_schema_', DATABASE());
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

INSERT IGNORE INTO {CFG:TableBaseName}migration_run_result (id, name, description) VALUES
    (70, 'Cancelled', 'Migration(s) stopped on request before completion');

SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_run' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_run_product_environment_started_at') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_run_product_environment_started_at ON {CFG:TableBaseName}migration_run (product_id, environment_id, started_at)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_record_history' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_record_history_migration_run_id') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_record_history_migration_run_id ON {CFG:TableBaseName}migration_record_history (migration_run_id)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_record_history' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_record_history_historized_at') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_record_history_historized_at ON {CFG:TableBaseName}migration_record_history (historized_at)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

SET @v_sql = IF(
    IFNULL(@v_lock_acquired, 0) = 1 AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{CFG:TableBaseName}migration_record' AND INDEX_NAME = 'ix_{CFG:TableBaseName}migration_record_product_environment') = 0,
    'CREATE INDEX ix_{CFG:TableBaseName}migration_record_product_environment ON {CFG:TableBaseName}migration_record (product_id, environment_id)',
    'DO 0');
PREPARE v_stmt FROM @v_sql;
EXECUTE v_stmt;
DEALLOCATE PREPARE v_stmt;

-- The version row is written only under the lock: a run that could not acquire it reports -32 and leaves no claim.
INSERT IGNORE INTO {CFG:TableBaseName}repository_schema_version (version, applied_at, raymigrator_version)
SELECT 3, CURRENT_TIMESTAMP, @RayMigratorVersion FROM DUAL WHERE IFNULL(@v_lock_acquired, 0) = 1;

DO RELEASE_LOCK(@v_lock_name);

SELECT IF(IFNULL(@v_lock_acquired, 0) = 1,
    '3,RayMigrator repository schema version [3] applied: migration_run_result cancelled and the reader indexes',
    '-32,RayMigrator could not acquire the repository schema lock within 60 seconds');
