/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Drop"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-02-03.1"

[Description]
Function = """
Drops all RayMigrator repository tables from the database.
WARNING: This is a destructive operation - all migration history will be lost!
"""

Behaviour = """
- Return value = 0: Success (tables dropped or operation skipped)
- Return value < 0: Error
- CURRENTLY NOT IMPLEMENTED: Returns success without dropping tables
- Requires manual implementation before use in production
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# No parameters required for this template

[ReturnValues]
# Format: SELECT 'code,message'
Success_0_NotImpl = "0,RayMigrator tables were NOT dropped since it is currently not implemented"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "PLACEHOLDER: Drop logic is commented out for safety"
Note3 = "Tables to drop (in order due to FK constraints): migration_record_history, migration_record, migration_run_meta, migration_run, product, environment, migrator_meta, migration_status, migration_run_result, migration_run_mode, migration_operation"
Note4 = "DAL-017: All identifiers (tables, columns, constraints) use unquoted snake_case per PostgreSQL community convention"
================================================================================
*/

/*
-- COMMENTED OUT: Uncomment and modify for actual drop implementation
-- WARNING: This will permanently delete all migration tracking data!

DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_log;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_event;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_record;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_run_meta;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_run;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}product;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}environment;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_operation;
DROP TABLE IF EXISTS {CFG:SchemaName}.{CFG:TableBaseName}migration_status;

SELECT '0,RayMigrator repository tables successfully dropped';
*/

SELECT '0,RayMigrator tables were NOT dropped since it is currently not implemented';
