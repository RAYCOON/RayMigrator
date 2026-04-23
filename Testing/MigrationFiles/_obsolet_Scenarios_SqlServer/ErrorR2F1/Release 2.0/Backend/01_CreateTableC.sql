/*
[RayMigrator]
Description = "Create TableC (intentional error)"
*/

CREATE TABLE [dbo].[TableC] (
    [Id] NONEXISTENT_TYPE NOT NULL,
    [Description] VARCHAR(300) NULL,
    CONSTRAINT [PK_TableC] PRIMARY KEY ([Id])
)
