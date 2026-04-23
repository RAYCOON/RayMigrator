/*
[RayMigrator]
Description = "Drop initial model"
UseTransaction = false
*/

ALTER TABLE [dbo].[Person] DROP CONSTRAINT [Login_Person]
go

ALTER TABLE [dbo].[Person] DROP CONSTRAINT [Sex_Person]
go

DROP TABLE [dbo].[Person]
go

DROP TABLE [dbo].[Login]
go

DROP TABLE [dbo].[Sex]
go
