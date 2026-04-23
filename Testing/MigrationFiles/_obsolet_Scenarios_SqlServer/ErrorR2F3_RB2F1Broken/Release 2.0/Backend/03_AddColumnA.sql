/*
[RayMigrator]
Description = "Add column to nonexistent table (intentional error)"
*/

ALTER TABLE [dbo].[NonexistentTable] ADD [ExtraInfo] VARCHAR(100) NULL
