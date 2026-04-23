/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE [dbo].[TableX2] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Title] VARCHAR(100) NOT NULL,
    [Amount] DECIMAL(10,2) NULL,
    CONSTRAINT [pk_TableX2] PRIMARY KEY ([Id])
)
