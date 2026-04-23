/*
[RayMigrator]
Description = "Insert data into TableA with explicit ID (fails on rerun)"
RunAlways = true
*/

INSERT INTO tablea (id, name) VALUES (9999, 'run_always_data');
