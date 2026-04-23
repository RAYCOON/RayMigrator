/*
[RayMigrator]
Description = "Rollback Insert data into TableD"
*/

DELETE FROM tabled WHERE status = 'data_d1';
