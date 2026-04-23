/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableX1] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Label] VARCHAR(100) NOT NULL,
    [Active] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [pk_TableX1] PRIMARY KEY ([Id])
)
