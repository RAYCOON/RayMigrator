/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableY1] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Description] VARCHAR(MAX) NULL,
    [Weight] DECIMAL(10,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT [pk_TableY1] PRIMARY KEY ([Id])
)
