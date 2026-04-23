/*
[RayMigrator]
Description = "Rollback Create TableB"
*/

IF OBJECT_ID('dbo.TableB', 'U') IS NOT NULL DROP TABLE [dbo].[TableB]
