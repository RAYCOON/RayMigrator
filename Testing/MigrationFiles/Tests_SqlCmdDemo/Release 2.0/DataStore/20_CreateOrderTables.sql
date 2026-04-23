/*
[RayMigrator]
Description = "Create order tables (WarehouseOrder, OrderLine)"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- WarehouseOrder
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'WarehouseOrder')
BEGIN
    CREATE TABLE [$(SchemaName)].[WarehouseOrder]
    (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [OrderNumber]      VARCHAR(20)       NOT NULL,
        [SupplierId]       INT               NOT NULL,
        [OrderStatusId]    INT               NOT NULL,
        [OrderDate]        DATETIME2(2)      NOT NULL DEFAULT GETUTCDATE(),
        [ExpectedDelivery] DATE              NULL,
        [Notes]            VARCHAR(1000)     NULL,
        CONSTRAINT [PK_WarehouseOrder] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WarehouseOrder_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [$(SchemaName)].[Supplier]([Id]),
        CONSTRAINT [FK_WarehouseOrder_OrderStatus] FOREIGN KEY ([OrderStatusId]) REFERENCES [$(SchemaName)].[OrderStatus]([Id]),
        CONSTRAINT [UQ_WarehouseOrder_OrderNumber] UNIQUE ([OrderNumber])
    );
END
GO

-- OrderLine
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'OrderLine')
BEGIN
    CREATE TABLE [$(SchemaName)].[OrderLine]
    (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [WarehouseOrderId] INT               NOT NULL,
        [ProductId]        INT               NOT NULL,
        [Quantity]         INT               NOT NULL,
        [UnitPrice]        DECIMAL(10,2)     NOT NULL,
        CONSTRAINT [PK_OrderLine] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderLine_WarehouseOrder] FOREIGN KEY ([WarehouseOrderId]) REFERENCES [$(SchemaName)].[WarehouseOrder]([Id]),
        CONSTRAINT [FK_OrderLine_Product] FOREIGN KEY ([ProductId]) REFERENCES [$(SchemaName)].[Product]([Id])
    );
END
GO

PRINT N'Order tables created in [$(SchemaName)].';
GO
