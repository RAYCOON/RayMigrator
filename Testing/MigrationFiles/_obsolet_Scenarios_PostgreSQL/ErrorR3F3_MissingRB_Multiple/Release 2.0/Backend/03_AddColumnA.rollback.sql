/*
[RayMigrator]
Description = "Rollback Add column to TableA"
*/

ALTER TABLE tablea DROP COLUMN IF EXISTS extra_info;
