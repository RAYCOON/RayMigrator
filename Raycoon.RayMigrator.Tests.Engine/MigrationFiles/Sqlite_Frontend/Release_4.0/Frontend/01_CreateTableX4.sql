/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE tablex4 (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 0
);
