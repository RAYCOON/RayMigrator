/*
[RayMigrator]
Description = "Rollback Insert data into TableA"
*/

DELETE FROM tablea WHERE name IN ('data_a1', 'data_a2');
