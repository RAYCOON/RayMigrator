/*
[RayMigrator]
Description = "Rollback Add column to TableA"
*/

IF COL_LENGTH('dbo.TableA', 'ExtraInfo') IS NOT NULL
    ALTER TABLE [dbo].[TableA] DROP COLUMN [ExtraInfo]
