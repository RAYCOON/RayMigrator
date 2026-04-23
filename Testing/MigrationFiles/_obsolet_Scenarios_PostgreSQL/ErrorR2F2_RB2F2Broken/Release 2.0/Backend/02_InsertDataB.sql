/*
[RayMigrator]
Description = "Insert data into TableB (intentional error)"
*/

INSERT INTO tableb (value, nonexistent_column) VALUES ('data_b1', 'x');
