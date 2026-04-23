/*
[RayMigrator]
Description = "Rollback Create TableD"
*/

IF OBJECT_ID('dbo.TableD', 'U') IS NOT NULL DROP TABLE [dbo].[TableD]
