/*
[RayMigrator]
Description = "Rollback Insert data into TableB"
*/

DELETE FROM [dbo].[TableB] WHERE [Value] IN ('data_b1', 'data_b2')
