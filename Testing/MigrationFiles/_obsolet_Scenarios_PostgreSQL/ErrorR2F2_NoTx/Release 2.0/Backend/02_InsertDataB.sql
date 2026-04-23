/*
[RayMigrator]
Description = "Insert data into TableB multi-block (intentional partial error)"
UseTransaction = false
*/

INSERT INTO tableb (value) VALUES ('data_b1_partial');

INSERT INTO tableb (nonexistent_column) VALUES ('this_will_fail');
