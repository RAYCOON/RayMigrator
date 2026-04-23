/*
[RayMigrator]
Environments = ["*"]
RunAlways = false
*/

CREATE TABLE tablex1 (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    label TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);
