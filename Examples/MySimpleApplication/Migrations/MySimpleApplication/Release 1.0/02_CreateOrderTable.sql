/*
[RayMigrator]
Description = "Create Order table with foreign key to Customer"
*/

CREATE TABLE [dbo].[Order]
(
    [Id]          INT IDENTITY(1,1) NOT NULL,
    [CustomerId]  INT NOT NULL,
    [OrderDate]   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Order] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Order_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customer]([Id])
)
GO
