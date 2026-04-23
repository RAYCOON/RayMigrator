/*
[RayMigrator]
Description = "Rollback Insert data into TableA with explicit ID"
*/

DELETE FROM [dbo].[TableA] WHERE [Id] = 9999
