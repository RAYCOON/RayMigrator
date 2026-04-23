/*
[RayMigrator]
Description = "Broken rollback for InsertDataB"
*/

DELETE FROM nonexistent_table_xyz WHERE id = 1;
