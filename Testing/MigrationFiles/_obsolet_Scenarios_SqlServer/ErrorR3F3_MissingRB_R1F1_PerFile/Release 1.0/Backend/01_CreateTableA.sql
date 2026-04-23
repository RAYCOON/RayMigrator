/*
[RayMigrator]
Description = "Create TableA"
RequireRollbackFile = false
*/

CREATE TABLE [dbo].[TableA] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Name] VARCHAR(100) NOT NULL,
    CONSTRAINT [PK_TableA] PRIMARY KEY ([Id])
)
