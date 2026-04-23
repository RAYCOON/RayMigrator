/*
[RayMigrator]
Description = "Insert data into TableA (intentional error, per-file Ignore)"
MigrationErrorAction = "Ignore"
*/

INSERT INTO tablea (nonexistent_column) VALUES ('should_fail');
