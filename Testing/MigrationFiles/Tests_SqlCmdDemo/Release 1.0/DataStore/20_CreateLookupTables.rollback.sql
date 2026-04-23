/*
[RayMigrator]
Description = "Drop lookup tables (OrderStatus, ProductCategory, UnitOfMeasure)"
UseTransaction = false
*/

:setvar SchemaName warehouse

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'OrderStatus')
    DROP TABLE [$(SchemaName)].[OrderStatus];
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'ProductCategory')
    DROP TABLE [$(SchemaName)].[ProductCategory];
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'UnitOfMeasure')
    DROP TABLE [$(SchemaName)].[UnitOfMeasure];
GO

PRINT N'Lookup tables dropped from [$(SchemaName)].';
GO
