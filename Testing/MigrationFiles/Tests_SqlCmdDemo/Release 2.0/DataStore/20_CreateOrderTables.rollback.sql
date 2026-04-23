/*
[RayMigrator]
Description = "Drop order tables (OrderLine, WarehouseOrder)"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Drop OrderLine first (has FK to WarehouseOrder)
IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'OrderLine')
    DROP TABLE [$(SchemaName)].[OrderLine];
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'WarehouseOrder')
    DROP TABLE [$(SchemaName)].[WarehouseOrder];
GO

PRINT N'Order tables dropped from [$(SchemaName)].';
GO
