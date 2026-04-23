/*
[RayMigrator]
Description = "Rollback Insert data into TableC"
*/

DELETE FROM [dbo].[TableC] WHERE [Description] = 'data_c1'
