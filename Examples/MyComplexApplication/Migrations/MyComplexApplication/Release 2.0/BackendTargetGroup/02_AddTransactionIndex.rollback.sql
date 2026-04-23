/*
[RayMigrator]
Description = "Drop composite index on Transaction"
*/

DROP INDEX [IX_Transaction_AccountId_Date] ON [dbo].[Transaction]
GO
