/*
[RayMigrator]
Description = "Rollback Insert data into TableC"
*/

DELETE FROM tablec WHERE description = 'data_c1';
