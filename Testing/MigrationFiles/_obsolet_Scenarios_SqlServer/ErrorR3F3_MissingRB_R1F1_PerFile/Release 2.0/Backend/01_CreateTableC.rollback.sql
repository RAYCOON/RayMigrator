/*
[RayMigrator]
Description = "Rollback Create TableC"
*/

IF OBJECT_ID('dbo.TableC', 'U') IS NOT NULL DROP TABLE [dbo].[TableC]
