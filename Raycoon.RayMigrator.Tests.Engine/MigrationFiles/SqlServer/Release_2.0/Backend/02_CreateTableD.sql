/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableD] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Code] VARCHAR(50) NOT NULL,
    [Description] VARCHAR(MAX) NULL,
    CONSTRAINT [pk_TableD] PRIMARY KEY ([Id])
)
