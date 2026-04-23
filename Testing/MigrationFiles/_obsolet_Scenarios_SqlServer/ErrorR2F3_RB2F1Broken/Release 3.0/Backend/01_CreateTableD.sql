/*
[RayMigrator]
Description = "Create TableD"
*/

CREATE TABLE [dbo].[TableD] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Status] VARCHAR(50) NOT NULL,
    CONSTRAINT [PK_TableD] PRIMARY KEY ([Id])
)
