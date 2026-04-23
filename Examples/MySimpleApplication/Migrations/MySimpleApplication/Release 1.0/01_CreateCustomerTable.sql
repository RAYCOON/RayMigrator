/*
[RayMigrator]
Description = "Create Customer table for storing customer information"
*/

CREATE TABLE [dbo].[Customer]
(
    [Id]        INT IDENTITY(1,1) NOT NULL,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName]  NVARCHAR(100) NOT NULL,
    [Phone]     NVARCHAR(20) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Customer] PRIMARY KEY ([Id])
)
GO
