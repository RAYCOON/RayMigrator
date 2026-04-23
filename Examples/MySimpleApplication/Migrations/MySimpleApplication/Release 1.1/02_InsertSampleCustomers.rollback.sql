/*
[RayMigrator]
Description = "Remove sample customer records"
*/

DELETE FROM [dbo].[Customer]
WHERE [Email] IN ('john.doe@example.com', 'jane.smith@example.com', 'bob.j@example.com')
GO
