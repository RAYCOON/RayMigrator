/*
[RayMigrator]
Description = "Rollback Create TableA"
*/

IF OBJECT_ID('dbo.TableA', 'U') IS NOT NULL DROP TABLE [dbo].[TableA]
