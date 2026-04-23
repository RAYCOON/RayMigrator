/*
[RayMigrator]
UseTransaction = true
Environments = ["*"]
RunAlways = false
*/

INSERT INTO [dbo].[TableA] ([Name], [Value]) VALUES ('alpha', 10)
GO
INSERT INTO [dbo].[TableA] ([Name], [Value]) VALUES ('beta', 20)
GO
INSERT INTO [dbo].[TableA] ([Name], [Value]) VALUES ('gamma', 30)
