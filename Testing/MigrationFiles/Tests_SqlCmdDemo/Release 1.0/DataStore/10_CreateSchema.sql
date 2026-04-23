/*
[RayMigrator]
Description = "Create warehouse schema"
UseTransaction = false
*/

:setvar SchemaName warehouse

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'$(SchemaName)')
BEGIN
    EXEC('CREATE SCHEMA [$(SchemaName)]');
END
GO

PRINT N'Schema [$(SchemaName)] ensured.';
GO
