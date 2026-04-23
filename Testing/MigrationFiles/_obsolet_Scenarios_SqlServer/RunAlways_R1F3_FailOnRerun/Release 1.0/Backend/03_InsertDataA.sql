/*
[RayMigrator]
Description = "Insert data into TableA with explicit ID (fails on rerun)"
RunAlways = true
*/

SET IDENTITY_INSERT [dbo].[TableA] ON;
INSERT INTO [dbo].[TableA] ([Id], [Name]) VALUES (9999, 'run_always_data');
SET IDENTITY_INSERT [dbo].[TableA] OFF;
