/*
[RayMigrator]
Description = "Create inventory tracking table"
UseTransaction = false
*/

:setvar SchemaName warehouse

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = N'$(SchemaName)' AND t.name = N'Inventory')
BEGIN
    CREATE TABLE [$(SchemaName)].[Inventory]
    (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [ProductId]       INT               NOT NULL,
        [QuantityOnHand]  INT               NOT NULL DEFAULT 0,
        [ReorderLevel]    INT               NOT NULL DEFAULT 10,
        [LastRestocked]   DATETIME2(2)      NULL,
        [UpdatedAt]       DATETIME2(2)      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Inventory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Inventory_Product] FOREIGN KEY ([ProductId]) REFERENCES [$(SchemaName)].[Product]([Id]),
        CONSTRAINT [UQ_Inventory_Product] UNIQUE ([ProductId])
    );
END
GO

PRINT N'Inventory table created in [$(SchemaName)].';
GO
