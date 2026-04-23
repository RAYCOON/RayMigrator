/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableX4] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Type] VARCHAR(50) NOT NULL,
    [Status] INT NOT NULL DEFAULT 0,
    CONSTRAINT [pk_TableX4] PRIMARY KEY ([Id])
)
