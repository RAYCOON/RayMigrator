/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE tableg (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 0
);
