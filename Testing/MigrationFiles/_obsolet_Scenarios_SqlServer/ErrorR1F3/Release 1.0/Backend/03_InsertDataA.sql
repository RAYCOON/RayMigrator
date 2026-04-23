/*
[RayMigrator]
Description = "Insert data into TableA (intentional error)"
*/

INSERT INTO [dbo].[TableA] ([Name], [NonexistentColumn]) VALUES ('data_a1', 'x')
