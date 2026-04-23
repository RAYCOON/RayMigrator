/*
[RayMigrator]
Description = "Create core tables (Supplier, Product with FKs)"
UseTransaction = false
*/

:setvar SchemaName warehouse

-- Supplier
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Supplier')
BEGIN
    CREATE TABLE [$(SchemaName)].[Supplier]
    (
        [Id]           INT IDENTITY(1,1) NOT NULL,
        [Name]         VARCHAR(200)      NOT NULL,
        [ContactEmail] VARCHAR(200)      NULL,
        [Phone]        VARCHAR(50)       NULL,
        [Country]      VARCHAR(100)      NULL,
        [CreatedAt]    DATETIME2(2)      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Supplier] PRIMARY KEY ([Id])
    );
END
GO

-- Product
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Product')
BEGIN
    CREATE TABLE [$(SchemaName)].[Product]
    (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [SKU]               VARCHAR(50)       NOT NULL,
        [Name]              VARCHAR(200)      NOT NULL,
        [ProductCategoryId] INT               NOT NULL,
        [UnitOfMeasureId]   INT               NOT NULL,
        [UnitPrice]         DECIMAL(10,2)     NOT NULL,
        [SupplierId]        INT               NOT NULL,
        [CreatedAt]         DATETIME2(2)      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Product] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Product_ProductCategory] FOREIGN KEY ([ProductCategoryId]) REFERENCES [$(SchemaName)].[ProductCategory]([Id]),
        CONSTRAINT [FK_Product_UnitOfMeasure] FOREIGN KEY ([UnitOfMeasureId]) REFERENCES [$(SchemaName)].[UnitOfMeasure]([Id]),
        CONSTRAINT [FK_Product_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [$(SchemaName)].[Supplier]([Id]),
        CONSTRAINT [UQ_Product_SKU] UNIQUE ([SKU])
    );
END
GO

PRINT N'Core tables created in [$(SchemaName)].';
GO
