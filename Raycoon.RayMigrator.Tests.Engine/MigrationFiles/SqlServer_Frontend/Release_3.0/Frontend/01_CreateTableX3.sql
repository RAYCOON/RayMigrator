/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableX3] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Ref] VARCHAR(50) NOT NULL,
    [Data] VARCHAR(MAX) NULL,
    CONSTRAINT [pk_TableX3] PRIMARY KEY ([Id])
)
