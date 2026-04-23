/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Product_CheckInsert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-17.1"

[Description]
Function = """
Checks if a Product exists by NameLower. If not, inserts a new Product record.
Returns the existing or new ProductId.
"""

Behaviour = """
- Return value >= 0: Success (ProductId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Idempotent: can be called multiple times safely
- Product NameLower has UNIQUE index - duplicate names will fail at DB level
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
Name      = "TEXT | REQUIRED | The product name in original casing (e.g., 'MyApplication')"
NameLower = "TEXT | REQUIRED | The product name in lowercase (e.g., 'myapplication') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (ProductId),Product [Name] with Id [N] found"
Success_Created  = "N (ProductId),Product [Name] with Id [N] successfully created"
Error_-20_Empty  = "-20,Product with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for CreatedAt timestamp"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note5 = "Uses temp table _rc_state to store intermediate state since SQLite has no session variables"
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

-- Store intermediate state in temp table
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT);
DELETE FROM "_rc_state";

-- Check existing product
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('existing_id', (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}Product" WHERE "NameLower" = @NameLower LIMIT 1));

-- Insert if not exists and name is valid
INSERT INTO "{CFG:TableBaseName}Product" ("Name", "NameLower", "CreatedAt")
SELECT @Name, @NameLower, datetime('now')
WHERE (SELECT "val" FROM "_rc_state" WHERE "key"='existing_id') IS NULL
  AND @Name IS NOT NULL AND LENGTH(@Name) > 0;

-- Capture new id (will be last_insert_rowid if inserted, or existing_id)
INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('new_id', CAST(last_insert_rowid() AS TEXT)),
    ('inserted', CAST(changes() AS TEXT));

-- Final result
SELECT CASE
    WHEN @Name IS NULL OR LENGTH(@Name) = 0
        THEN '-20,Product with empty name [' || IFNULL(@Name, 'NULL') || '] is not allowed!'
    WHEN (SELECT "val" FROM "_rc_state" WHERE "key"='existing_id') IS NOT NULL
        THEN (SELECT "val" FROM "_rc_state" WHERE "key"='existing_id')
             || ',Product [' || @Name || '] with Id ['
             || (SELECT "val" FROM "_rc_state" WHERE "key"='existing_id') || '] found'
    ELSE (SELECT "val" FROM "_rc_state" WHERE "key"='new_id')
         || ',Product [' || @Name || '] with Id ['
         || (SELECT "val" FROM "_rc_state" WHERE "key"='new_id') || '] successfully created'
END;

DROP TABLE IF EXISTS "_rc_state";
