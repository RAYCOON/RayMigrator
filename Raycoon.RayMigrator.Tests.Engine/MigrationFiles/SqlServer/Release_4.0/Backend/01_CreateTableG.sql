/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableG] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Type] VARCHAR(50) NOT NULL,
    [Status] INT NOT NULL DEFAULT 0,
    CONSTRAINT [pk_TableG] PRIMARY KEY ([Id])
)
