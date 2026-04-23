/*
[RayMigrator]
Description = "Drop core tables (Product, Supplier)"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Drop Product first (has FKs to Supplier, ProductCategory, UnitOfMeasure)
IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Product')
    DROP TABLE [$(SchemaName)].[Product];
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Supplier')
    DROP TABLE [$(SchemaName)].[Supplier];
GO

PRINT N'Core tables dropped from [$(SchemaName)].';
GO
