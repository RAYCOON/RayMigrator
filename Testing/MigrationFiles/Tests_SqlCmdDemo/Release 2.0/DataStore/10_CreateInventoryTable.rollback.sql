/*
[RayMigrator]
Description = "Drop inventory table"
UseTransaction = false
*/

:setvar SchemaName warehouse

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Inventory')
    DROP TABLE [$(SchemaName)].[Inventory];
GO

PRINT N'Inventory table dropped from [$(SchemaName)].';
GO
