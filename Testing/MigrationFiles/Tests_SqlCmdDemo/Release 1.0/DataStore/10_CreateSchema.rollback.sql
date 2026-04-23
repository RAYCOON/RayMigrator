/*
[RayMigrator]
Description = "Drop warehouse schema"
UseTransaction = false
*/

:setvar SchemaName warehouse

IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'$(SchemaName)')
BEGIN
    EXEC('DROP SCHEMA [$(SchemaName)]');
END
GO

PRINT N'Schema [$(SchemaName)] dropped.';
GO
