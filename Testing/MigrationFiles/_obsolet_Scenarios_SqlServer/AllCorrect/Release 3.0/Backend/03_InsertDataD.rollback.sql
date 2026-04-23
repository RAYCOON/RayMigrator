/*
[RayMigrator]
Description = "Rollback Insert data into TableD"
*/

DELETE FROM [dbo].[TableD] WHERE [Status] = 'data_d1'
