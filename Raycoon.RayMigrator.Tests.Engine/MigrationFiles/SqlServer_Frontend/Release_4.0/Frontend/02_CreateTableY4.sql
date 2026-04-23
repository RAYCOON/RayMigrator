/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableY4] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Category] VARCHAR(100) NOT NULL,
    [Score] DECIMAL(10,2) NULL,
    CONSTRAINT [pk_TableY4] PRIMARY KEY ([Id])
)
