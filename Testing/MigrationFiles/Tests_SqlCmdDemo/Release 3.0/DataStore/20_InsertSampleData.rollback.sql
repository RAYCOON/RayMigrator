/*
[RayMigrator]
Description = "Delete sample data in FK-safe order"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Delete in FK-safe order: children first, then parents
DELETE FROM [$(SchemaName)].[OrderLine];
GO

DELETE FROM [$(SchemaName)].[WarehouseOrder];
GO

DELETE FROM [$(SchemaName)].[Inventory];
GO

DELETE FROM [$(SchemaName)].[Product];
GO

DELETE FROM [$(SchemaName)].[Supplier];
GO

PRINT N'Sample data deleted from [$(SchemaName)].';
GO
