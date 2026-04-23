/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Drop"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-01-29.1"

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
# Replaced when loading the template (compile-time)
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
Note3 = "Tables to drop (in order due to FK constraints): MigrationRecordHistory, MigrationRecord, MigrationRunMeta, MigrationRun, Product, Environment, MigratorMeta, MigrationStatus, MigrationRunResult, MigrationRunMode, MigrationOperation"
Note4 = "Also drops MigrationLog and MigrationEvent from logging infrastructure"
Note5 = "Consider implementing confirmation parameter before enabling"
================================================================================
*/

/*
-- COMMENTED OUT: Uncomment and modify for actual drop implementation
-- WARNING: This will permanently delete all migration tracking data!

USE [Backend_1];
GO

DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationEvent];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMeta];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}Product];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}Environment];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMode];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationOperation];
DROP TABLE IF EXISTS [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationStatus];

SELECT '0,RayMigrator repository tables successfully dropped';
*/

SELECT '0,RayMigrator tables were NOT dropped since it is currently not implemented';
