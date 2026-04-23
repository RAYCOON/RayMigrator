/*
[RayMigrator]
Description = "This migration intentionally fails to trigger rollback chain"
*/

SELECT * FROM this_table_does_not_exist_xyz_trigger_error;
