/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableB] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Label] VARCHAR(100) NOT NULL,
    [Score] INT NULL,
    CONSTRAINT [pk_TableB] PRIMARY KEY ([Id])
)
