/*
[RayMigrator]
Description = "BROKEN rollback - references non-existent table to trigger error"
*/

SELECT * FROM nonexistent_xyz_table_that_does_not_exist;
