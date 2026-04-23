/*
[RayMigrator]
Description = "Insert partial data then trigger UNIQUE violation"
UseTransaction = false
*/

INSERT INTO errortest_data (name) VALUES ('R3_error_partial');

INSERT INTO errortest_data (name) VALUES ('R1_data');
