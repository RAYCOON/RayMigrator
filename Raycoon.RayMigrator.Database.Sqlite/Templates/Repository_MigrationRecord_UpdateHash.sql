/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_UpdateHash"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

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
 * The engine runs this template without a transaction of the driver (the
 * repository, the database log writer and the retention set UseTransaction
 * to false), so each statement commits on its own unless the template
 * opens a transaction itself. Statements that share an invariant must open
 * BEGIN IMMEDIATE ... COMMIT or leave the decision to one statement. The
 * sqlite3 CLI needs the same: run the file as it is, without a transaction
 * around it ("sqlite3 db.sqlite < file.sql").
 *
 * One statement writes; the statements around it only report its result.
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
