/*
[RayMigrator]
Description = "Create Transaction table for recording financial transactions"
*/

CREATE TABLE [dbo].[Transaction]
(
    [Id]              INT IDENTITY(1,1) NOT NULL,
    [AccountId]       INT NOT NULL,
    [Amount]          DECIMAL(18,2) NOT NULL,
    [TransactionType] NVARCHAR(20) NOT NULL,
    [Description]     NVARCHAR(500) NULL,
    [TransactionDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Transaction] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Transaction_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
)
GO
