/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_Select"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-09-07.1"

[Description]
Function = """
Looks up a Environment by NameLower WITHOUT inserting anything.
Returns the EnvironmentId, or 0 when no Environment with that name exists.
Used by run modes and commands that must not write to the repository (e.g. Simulate),
which therefore cannot use Repository_Environment_CheckInsert (#7).
"""

Behaviour = """
- Return value > 0: EnvironmentId found (logged at Debug level)
- Return value = 0: Environment not registered yet (NOT an error - the caller treats the repository as empty)
- Return value < 0: Error (logged at Error level, command aborted)
- Read-only and idempotent: never inserts, updates or deletes
- Requires the repository tables to exist (Repository_CheckCreate); on a missing table the DB error propagates
"""

[ConfigPlaceholders]
SchemaName    = "Not used for MySQL (database = schema)"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
Name      = "TEXT | REQUIRED | The environment name in original casing (used in the result message only)"
NameLower = "TEXT | REQUIRED | The environment name in lowercase - pre-computed in C# - used for the lookup"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Found    = "N (EnvironmentId),Environment [Name] with Id [N] found"
Success_NotFound = "0,Environment [Name] not found"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Must always return exactly one row, also when the environment does not exist"
Note4 = "Must NOT insert - Repository_Environment_CheckInsert is the writing counterpart"
Note5 = "Uses a session variable like Repository_Environment_CheckInsert"
================================================================================
*/

SET @v_environment_id = (SELECT id FROM {CFG:TableBaseName}environment WHERE name_lower = @NameLower LIMIT 1);

SELECT CASE
    WHEN @v_environment_id IS NULL
        THEN CONCAT('0,Environment [', IFNULL(@Name, 'NULL'), '] not found')
    ELSE
        CONCAT(CAST(@v_environment_id AS CHAR), ',Environment [', @Name, '] with Id [', CAST(@v_environment_id AS CHAR), '] found')
END;
