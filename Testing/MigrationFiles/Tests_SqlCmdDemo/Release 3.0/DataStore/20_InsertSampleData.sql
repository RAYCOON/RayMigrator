/*
[RayMigrator]
Description = "Insert sample suppliers, products, inventory, and orders"
UseTransaction = false
*/

:setvar SchemaName warehouse
:setvar DefaultCountry "Germany"

-- Suppliers
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[Supplier])
BEGIN
    SET IDENTITY_INSERT [$(SchemaName)].[Supplier] ON;

    INSERT INTO [$(SchemaName)].[Supplier] ([Id], [Name], [ContactEmail], [Phone], [Country]) VALUES
    (1, 'TechParts GmbH',      'orders@techparts.de',    '+49-30-555-0100', '$(DefaultCountry)'),
    (2, 'Global Raw Materials', 'sales@globalraw.com',    '+1-555-0200',     'United States'),
    (3, 'PackRight Ltd',        'info@packright.co.uk',   '+44-20-555-0300', 'United Kingdom'),
    (4, 'SafetyFirst AG',       'contact@safetyfirst.ch', '+41-44-555-0400', 'Switzerland');

    SET IDENTITY_INSERT [$(SchemaName)].[Supplier] OFF;
END
GO

-- Products
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[Product])
BEGIN
    SET IDENTITY_INSERT [$(SchemaName)].[Product] ON;

    INSERT INTO [$(SchemaName)].[Product] ([Id], [SKU], [Name], [ProductCategoryId], [UnitOfMeasureId], [UnitPrice], [SupplierId]) VALUES
    (1, 'ELEC-001', 'Circuit Board Type A',  1, 1, 45.50, 1),
    (2, 'ELEC-002', 'LED Display Module',    1, 1, 89.99, 1),
    (3, 'RAW-001',  'Steel Sheet 2mm',       2, 2, 12.30, 2),
    (4, 'RAW-002',  'Copper Wire 1.5mm',     2, 4,  8.75, 2),
    (5, 'PACK-001', 'Cardboard Box Large',   3, 5,  2.50, 3),
    (6, 'PACK-002', 'Bubble Wrap Roll 50m',  3, 1, 15.00, 3),
    (7, 'SAFE-001', 'Safety Goggles Pro',    5, 1, 22.00, 4),
    (8, 'SAFE-002', 'Heat Resistant Gloves', 5, 1, 35.00, 4);

    SET IDENTITY_INSERT [$(SchemaName)].[Product] OFF;
END
GO

-- Inventory
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[Inventory])
BEGIN
    SET IDENTITY_INSERT [$(SchemaName)].[Inventory] ON;

    INSERT INTO [$(SchemaName)].[Inventory] ([Id], [ProductId], [QuantityOnHand], [ReorderLevel]) VALUES
    (1, 1,  150, 25),
    (2, 2,   80, 15),
    (3, 3,  500, 100),
    (4, 4,  300, 50),
    (5, 5, 1000, 200),
    (6, 6,   60, 10),
    (7, 7,  200, 30),
    (8, 8,  120, 20);

    SET IDENTITY_INSERT [$(SchemaName)].[Inventory] OFF;
END
GO

-- Warehouse Orders
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[WarehouseOrder])
BEGIN
    SET IDENTITY_INSERT [$(SchemaName)].[WarehouseOrder] ON;

    INSERT INTO [$(SchemaName)].[WarehouseOrder] ([Id], [OrderNumber], [SupplierId], [OrderStatusId], [ExpectedDelivery], [Notes]) VALUES
    (1, 'WO-2026-0001', 1, 5, '2026-03-20', 'Regular restock order'),
    (2, 'WO-2026-0002', 2, 3, '2026-04-01', 'Quarterly raw materials order'),
    (3, 'WO-2026-0003', 3, 2, '2026-04-10', 'Packaging supplies for Q2'),
    (4, 'WO-2026-0004', 4, 1, NULL,          'Draft order - pending approval');

    SET IDENTITY_INSERT [$(SchemaName)].[WarehouseOrder] OFF;
END
GO

-- Order Lines
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[OrderLine])
BEGIN
    SET IDENTITY_INSERT [$(SchemaName)].[OrderLine] ON;

    INSERT INTO [$(SchemaName)].[OrderLine] ([Id], [WarehouseOrderId], [ProductId], [Quantity], [UnitPrice]) VALUES
    (1, 1, 1,  50, 45.50),
    (2, 1, 2,  30, 89.99),
    (3, 2, 3, 200, 12.30),
    (4, 2, 4, 100,  8.75),
    (5, 3, 5, 500,  2.50),
    (6, 3, 6,  20, 15.00),
    (7, 4, 7,  50, 22.00),
    (8, 4, 8,  30, 35.00);

    SET IDENTITY_INSERT [$(SchemaName)].[OrderLine] OFF;
END
GO

PRINT N'Sample data inserted into [$(SchemaName)].';
GO
