/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_UpdateHash"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-02-03.1"

[Description]
Function = """
Updates the hash fields of an existing Migration record.
Used by the Update-Hash command to synchronize repository hashes with changed files on disk.
"""

Behaviour = """
- Return value >= 0: Success (MigrationRecordId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Only updates hash-related fields, does not change state or result
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRecordId      = "INTEGER | REQUIRED | The Migration record ID to update"
FileUpHash       = "TEXT | REQUIRED | New SHA256 hash of the entire file"
FileUpConfigHash = "TEXT | OPTIONAL | New SHA256 hash of TOML config section"
FileUpBlocksHash = "TEXT | REQUIRED | New SHA256 hash of SQL blocks"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationRecordId),Migration with Id [N] hashes updated"
Error_-1_NotFound = "-1,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Only hash fields are updated - state and result remain unchanged"
Note4 = "Uses temp table for existence check since SQLite has no session variables"
================================================================================
*/

/*
 * Transaction requirement (DAL-024):
 * This template contains multiple statements that must execute atomically.
 * The DalSqlite driver wraps execution in a transaction when UseTransaction
 * is enabled (the default), so in-framework use is safe. Manual execution
 * via the sqlite3 CLI (e.g., "sqlite3 db.sqlite < file.sql") must be
 * wrapped in "BEGIN TRANSACTION; ... COMMIT;" to guarantee atomicity.
 */

CREATE TEMP TABLE IF NOT EXISTS "_rc_exists" ("cnt" INTEGER);
DELETE FROM "_rc_exists";
INSERT INTO "_rc_exists" ("cnt")
SELECT COUNT(*) FROM "{CFG:TableBaseName}MigrationRecord" WHERE "Id" = @MigrationRecordId;

UPDATE "{CFG:TableBaseName}MigrationRecord"
SET
    "FileUpHash" = @FileUpHash,
    "FileUpConfigHash" = @FileUpConfigHash,
    "FileUpBlocksHash" = @FileUpBlocksHash
WHERE "Id" = @MigrationRecordId AND (SELECT "cnt" FROM "_rc_exists") > 0;

SELECT CASE WHEN (SELECT "cnt" FROM "_rc_exists") = 0
    THEN '-1,Migration with Id [' || IFNULL(CAST(@MigrationRecordId AS TEXT), 'NULL') || '] does not exist'
    ELSE CAST(@MigrationRecordId AS TEXT) || ',Migration with Id [' || CAST(@MigrationRecordId AS TEXT) || '] hashes updated'
END;

DROP TABLE IF EXISTS "_rc_exists";
