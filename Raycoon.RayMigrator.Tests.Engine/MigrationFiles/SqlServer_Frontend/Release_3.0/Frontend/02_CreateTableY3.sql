/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableY3] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Tag] VARCHAR(50) NOT NULL,
    [Priority] INT NULL,
    CONSTRAINT [pk_TableY3] PRIMARY KEY ([Id])
)
