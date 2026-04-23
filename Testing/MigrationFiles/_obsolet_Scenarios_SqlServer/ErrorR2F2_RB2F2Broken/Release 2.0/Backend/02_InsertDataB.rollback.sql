/*
[RayMigrator]
Description = "Broken rollback for InsertDataB"
*/

DELETE FROM [dbo].[NonexistentTable] WHERE [Id] = 1
