/*
[RayMigrator]
Description = "Add Email column to Customer table"
*/

ALTER TABLE [dbo].[Customer] ADD [Email] NVARCHAR(255) NULL
GO
