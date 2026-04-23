/*
[RayMigrator]
Description = "Insert data into TableA (intentional error, per-file Ignore)"
MigrationErrorAction = "Ignore"
*/

INSERT INTO [dbo].[TableA] ([NonexistentColumn]) VALUES ('should_fail')
