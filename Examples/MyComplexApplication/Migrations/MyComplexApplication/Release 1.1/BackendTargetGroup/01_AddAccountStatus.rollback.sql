/*
[RayMigrator]
Description = "Remove StatusId from Account and drop AccountStatus table"
*/

ALTER TABLE [dbo].[Account] DROP CONSTRAINT [FK_Account_AccountStatus]
GO

ALTER TABLE [dbo].[Account] DROP COLUMN [StatusId]
GO

DROP TABLE [dbo].[AccountStatus]
GO
