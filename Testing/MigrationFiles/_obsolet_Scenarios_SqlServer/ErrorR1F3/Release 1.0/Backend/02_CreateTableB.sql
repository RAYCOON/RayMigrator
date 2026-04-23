/*
[RayMigrator]
Description = "Create TableB"
*/

CREATE TABLE [dbo].[TableB] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Value] VARCHAR(200) NULL,
    CONSTRAINT [PK_TableB] PRIMARY KEY ([Id])
)
