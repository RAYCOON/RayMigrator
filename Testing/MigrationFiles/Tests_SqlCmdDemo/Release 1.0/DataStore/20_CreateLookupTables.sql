/*
[RayMigrator]
Description = "Create lookup tables (UnitOfMeasure, ProductCategory, OrderStatus)"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Unit of Measure
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'UnitOfMeasure')
BEGIN
    CREATE TABLE [$(SchemaName)].[UnitOfMeasure]
    (
        [Id]   INT          NOT NULL,
        [Code] VARCHAR(10)  NOT NULL,
        [Name] VARCHAR(50)  NOT NULL,
        CONSTRAINT [PK_UnitOfMeasure] PRIMARY KEY ([Id])
    );
END
GO

-- Product Category
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'ProductCategory')
BEGIN
    CREATE TABLE [$(SchemaName)].[ProductCategory]
    (
        [Id]          INT           NOT NULL,
        [Name]        VARCHAR(100)  NOT NULL,
        [Description] VARCHAR(500)  NULL,
        CONSTRAINT [PK_ProductCategory] PRIMARY KEY ([Id])
    );
END
GO

-- Order Status
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'OrderStatus')
BEGIN
    CREATE TABLE [$(SchemaName)].[OrderStatus]
    (
        [Id]   INT         NOT NULL,
        [Name] VARCHAR(50) NOT NULL,
        CONSTRAINT [PK_OrderStatus] PRIMARY KEY ([Id])
    );
END
GO

PRINT N'Lookup tables created in [$(SchemaName)].';
GO
