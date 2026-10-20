/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_CheckInsert"
DatabaseType   = "Sqlite"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.2"

[Description]
Function = """
Checks if an Environment exists by NameLower. If not, inserts a new Environment record.
Returns the existing or new EnvironmentId.
"""

Behaviour = """
- Return value >= 0: Success (EnvironmentId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Idempotent: can be called multiple times safely
- Environment NameLower has a UNIQUE index; one statement decides (INSERT ... ON CONFLICT DO NOTHING): a
  parallel first start of the same name inserts nothing and gets the Id of the row of the other start
  instead of failing on the index (SQLite error 19)
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
Name      = "TEXT | REQUIRED | The environment name in original casing (e.g., 'Docker')"
NameLower = "TEXT | REQUIRED | The environment name in lowercase (e.g., 'docker') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (EnvironmentId),Environment [Name] with Id [N] found"
Success_Created  = "N (EnvironmentId),Environment [Name] with Id [N] successfully created"
Error_-50_Empty  = "-50,Environment with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use datetime('now') for CreatedAt timestamp"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note5 = "INSERT ... ON CONFLICT (NameLower) DO NOTHING tolerates the conflict on the unique index of the name and nothing else; INSERT OR IGNORE would also skip a row that breaks a NOT NULL or CHECK constraint, and the start would get no Id instead of the error"
Note6 = "changes() in the final SELECT reports the INSERT right before it, and the Id is read from the table, whichever start inserted the row"
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
 * One statement decides: the INSERT ... ON CONFLICT ("NameLower") DO
 * NOTHING keeps the row of a parallel first start instead of failing.
 */

-- Register the name unless it is registered: a parallel first start that registered it meanwhile wins the conflict
-- on the unique index of NameLower, and this INSERT then inserts nothing instead of failing (SQLite error 19)
INSERT INTO "{CFG:TableBaseName}Environment" ("Name", "NameLower", "CreatedAt")
SELECT @Name, @NameLower, datetime('now')
WHERE @Name IS NOT NULL AND LENGTH(@Name) > 0
ON CONFLICT ("NameLower") DO NOTHING;

-- changes() reports the INSERT above: no statement may change rows between the two. The Id comes from the table,
-- whichever start inserted the row.
SELECT CASE
    WHEN @Name IS NULL OR LENGTH(@Name) = 0
        THEN '-50,Environment with empty name [' || IFNULL(@Name, 'NULL') || '] is not allowed!'
    WHEN changes() > 0
        THEN (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}Environment" WHERE "NameLower" = @NameLower)
             || ',Environment [' || @Name || '] with Id ['
             || (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}Environment" WHERE "NameLower" = @NameLower) || '] successfully created'
    ELSE (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}Environment" WHERE "NameLower" = @NameLower)
         || ',Environment [' || @Name || '] with Id ['
         || (SELECT CAST("Id" AS TEXT) FROM "{CFG:TableBaseName}Environment" WHERE "NameLower" = @NameLower) || '] found'
END;
