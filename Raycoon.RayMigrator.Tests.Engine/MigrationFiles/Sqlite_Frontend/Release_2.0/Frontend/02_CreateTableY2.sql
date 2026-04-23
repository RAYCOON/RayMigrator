/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE tabley2 (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 0
);
