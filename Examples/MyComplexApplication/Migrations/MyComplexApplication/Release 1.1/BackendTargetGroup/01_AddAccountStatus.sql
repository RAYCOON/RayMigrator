/*
[RayMigrator]
Description = "Create AccountStatus lookup table and add StatusId to Account"
*/

CREATE TABLE [dbo].[AccountStatus]
(
    [Id]   INT NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_AccountStatus] PRIMARY KEY ([Id])
)
GO

INSERT INTO [dbo].[AccountStatus] ([Id], [Name])
VALUES (1, 'Active'), (2, 'Suspended'), (3, 'Closed')
GO

ALTER TABLE [dbo].[Account]
ADD [StatusId] INT NOT NULL DEFAULT 1
    CONSTRAINT [FK_Account_AccountStatus] FOREIGN KEY ([StatusId]) REFERENCES [dbo].[AccountStatus]([Id])
GO
