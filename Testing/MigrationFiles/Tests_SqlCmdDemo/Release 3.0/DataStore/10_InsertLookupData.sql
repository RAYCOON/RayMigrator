/*
[RayMigrator]
Description = "Insert master data into lookup tables"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Units of Measure
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[UnitOfMeasure])
BEGIN
    INSERT INTO [$(SchemaName)].[UnitOfMeasure] ([Id], [Code], [Name]) VALUES
    (1, 'EA',  'Each'),
    (2, 'KG',  'Kilogram'),
    (3, 'LTR', 'Liter'),
    (4, 'M',   'Meter'),
    (5, 'BOX', 'Box');
END
GO

-- Product Categories
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[ProductCategory])
BEGIN
    INSERT INTO [$(SchemaName)].[ProductCategory] ([Id], [Name], [Description]) VALUES
    (1, 'Electronics',      'Electronic components and devices'),
    (2, 'Raw Materials',    'Unprocessed materials for manufacturing'),
    (3, 'Packaging',        'Packaging supplies and materials'),
    (4, 'Office Supplies',  'General office consumables'),
    (5, 'Safety Equipment', 'Personal protective equipment and safety gear');
END
GO

-- Order Statuses
IF NOT EXISTS (SELECT TOP(1) 1 FROM [$(SchemaName)].[OrderStatus])
BEGIN
    INSERT INTO [$(SchemaName)].[OrderStatus] ([Id], [Name]) VALUES
    (1, 'Draft'),
    (2, 'Submitted'),
    (3, 'Confirmed'),
    (4, 'Shipped'),
    (5, 'Delivered'),
    (6, 'Cancelled');
END
GO

PRINT N'Lookup data inserted into [$(SchemaName)].';
GO
