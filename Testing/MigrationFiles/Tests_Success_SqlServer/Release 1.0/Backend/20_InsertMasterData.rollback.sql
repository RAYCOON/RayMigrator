/*
[RayMigrator]
Description = "Delete masterdata entries in Table 'Sex'"
UseTransaction = false
*/

DELETE FROM [dbo].[Sex] WHERE [Id] IN (1,2);
