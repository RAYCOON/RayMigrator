/*
[RayMigrator]
Description = "Create Account table for financial account management"
*/

CREATE TABLE [dbo].[Account]
(
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [AccountNumber] NVARCHAR(20) NOT NULL UNIQUE,
    [HolderName]    NVARCHAR(200) NOT NULL,
    [Balance]       DECIMAL(18,2) NOT NULL DEFAULT 0,
    [CreatedAt]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Account] PRIMARY KEY ([Id])
)
GO
