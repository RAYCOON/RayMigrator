/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableH] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Category] VARCHAR(100) NOT NULL,
    [Weight] DECIMAL(10,2) NULL,
    CONSTRAINT [pk_TableH] PRIMARY KEY ([Id])
)
