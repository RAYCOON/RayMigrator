/*
[RayMigrator]
Description = "Insert data into TableD (intentional error)"
*/

INSERT INTO tabled (status, nonexistent_column) VALUES ('data_d1', 'x');
