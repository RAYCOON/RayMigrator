/*
[RayMigrator]
Description = "Create Product table for the product catalog"
*/

CREATE TABLE [dbo].[Product]
(
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [Name]          NVARCHAR(200) NOT NULL,
    [Price]         DECIMAL(18,2) NOT NULL,
    [StockQuantity] INT NOT NULL DEFAULT 0,
    [CreatedAt]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Product] PRIMARY KEY ([Id])
)
GO
