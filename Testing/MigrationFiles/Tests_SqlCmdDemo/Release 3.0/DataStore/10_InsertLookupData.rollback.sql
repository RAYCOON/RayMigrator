/*
[RayMigrator]
Description = "Delete master data from lookup tables"
UseTransaction = false
*/

:setvar SchemaName warehouse

DELETE FROM [$(SchemaName)].[OrderStatus];
GO

DELETE FROM [$(SchemaName)].[ProductCategory];
GO

DELETE FROM [$(SchemaName)].[UnitOfMeasure];
GO

PRINT N'Lookup data deleted from [$(SchemaName)].';
GO
