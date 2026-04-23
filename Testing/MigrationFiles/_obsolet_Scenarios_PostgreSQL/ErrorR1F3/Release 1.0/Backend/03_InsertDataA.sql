/*
[RayMigrator]
Description = "Insert data into TableA (intentional error)"
*/

INSERT INTO tablea (name, nonexistent_column) VALUES ('data_a1', 'x');
