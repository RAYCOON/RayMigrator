/*
[RayMigrator]
Description = "Create TableA (intentional error)"
*/

CREATE TABLE [dbo].[TableA] (
    [Id] NONEXISTENT_TYPE NOT NULL,
    [Name] VARCHAR(100) NOT NULL,
    CONSTRAINT [PK_TableA] PRIMARY KEY ([Id])
)
