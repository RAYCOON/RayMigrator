/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableF] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Tag] VARCHAR(50) NOT NULL,
    [Priority] INT NULL,
    CONSTRAINT [pk_TableF] PRIMARY KEY ([Id])
)
