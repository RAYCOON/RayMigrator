/*
[RayMigrator]
Description = "Create table TableA"
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableA] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Name] VARCHAR(100) NOT NULL,
    [Value] INT NULL,
    CONSTRAINT [pk_TableA] PRIMARY KEY ([Id])
)
