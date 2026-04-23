/*
[RayMigrator]
Description = "Create table tablea"
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE tablea (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    value INTEGER
);
