/*
[RayMigrator]
Description = "Insert data into TableB (intentional error)"
*/

INSERT INTO [dbo].[TableB] ([Value], [NonexistentColumn]) VALUES ('data_b1', 'x')
