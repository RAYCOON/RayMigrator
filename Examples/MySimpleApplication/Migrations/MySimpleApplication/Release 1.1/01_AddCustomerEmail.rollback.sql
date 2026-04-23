/*
[RayMigrator]
Description = "Remove Email column from Customer table"
*/

ALTER TABLE [dbo].[Customer] DROP COLUMN [Email]
GO
