/*
[RayMigrator]
Description = "Remove sample account records"
*/

DELETE FROM [dbo].[Account]
WHERE [AccountNumber] IN ('ACC-001', 'ACC-002', 'ACC-003')
GO
