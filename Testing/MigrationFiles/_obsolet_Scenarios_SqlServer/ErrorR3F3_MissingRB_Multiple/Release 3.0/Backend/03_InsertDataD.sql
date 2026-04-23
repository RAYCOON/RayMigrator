/*
[RayMigrator]
Description = "Insert data into TableD (intentional error)"
*/

INSERT INTO [dbo].[TableD] ([Status], [NonexistentColumn]) VALUES ('data_d1', 'x')
