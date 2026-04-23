/*
[RayMigrator]
Description = "Rollback Insert data into TableB"
*/

DELETE FROM tableb WHERE value IN ('data_b1', 'data_b2');
