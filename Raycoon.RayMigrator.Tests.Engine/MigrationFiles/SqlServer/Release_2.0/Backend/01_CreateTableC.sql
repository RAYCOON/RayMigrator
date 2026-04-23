/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableC] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Title] VARCHAR(100) NOT NULL,
    [Amount] DECIMAL(10,2) NULL,
    CONSTRAINT [pk_TableC] PRIMARY KEY ([Id])
)
