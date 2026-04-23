/*
[RayMigrator]
Description = "Add column to nonexistent table (intentional error)"
*/

ALTER TABLE nonexistent_table_xyz ADD COLUMN extra_info VARCHAR(100) NULL;
