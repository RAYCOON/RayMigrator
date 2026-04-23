/*
[RayMigrator]
Description = "Rollback Insert data into TableA"
*/

DELETE FROM [dbo].[TableA] WHERE [Name] IN ('data_a1', 'data_a2')
