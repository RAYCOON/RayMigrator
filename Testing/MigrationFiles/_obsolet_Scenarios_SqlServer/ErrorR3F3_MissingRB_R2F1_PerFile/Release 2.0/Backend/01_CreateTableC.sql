/*
[RayMigrator]
Description = "Create TableC"
RequireRollbackFile = false
*/

CREATE TABLE [dbo].[TableC] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Description] VARCHAR(300) NULL,
    CONSTRAINT [PK_TableC] PRIMARY KEY ([Id])
)
