/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableY2] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Code] VARCHAR(50) NOT NULL,
    [Enabled] BIT NOT NULL DEFAULT 0,
    CONSTRAINT [pk_TableY2] PRIMARY KEY ([Id])
)
