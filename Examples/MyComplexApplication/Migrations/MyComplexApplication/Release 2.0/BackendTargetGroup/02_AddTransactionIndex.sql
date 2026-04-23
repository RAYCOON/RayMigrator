/*
[RayMigrator]
Description = "Add composite index on Transaction for reporting performance"
*/

CREATE NONCLUSTERED INDEX [IX_Transaction_AccountId_Date]
ON [dbo].[Transaction] ([AccountId], [TransactionDate])
INCLUDE ([Amount], [TransactionType])
GO
