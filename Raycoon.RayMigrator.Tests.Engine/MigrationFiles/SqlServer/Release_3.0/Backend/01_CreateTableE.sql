/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableE] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Ref] VARCHAR(50) NOT NULL,
    [Data] VARCHAR(MAX) NULL,
    CONSTRAINT [pk_TableE] PRIMARY KEY ([Id])
)
