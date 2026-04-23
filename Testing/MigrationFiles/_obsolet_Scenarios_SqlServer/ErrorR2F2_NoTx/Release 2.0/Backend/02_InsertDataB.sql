/*
[RayMigrator]
Description = "Insert data into TableB multi-block (intentional partial error)"
UseTransaction = false
*/

INSERT INTO [dbo].[TableB] ([Value]) VALUES ('data_b1_partial')
go
INSERT INTO [dbo].[TableB] ([NonexistentColumn]) VALUES ('this_will_fail')
