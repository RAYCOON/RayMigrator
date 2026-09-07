/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_Select"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Schema name from Repository configuration (e.g., 'ray')"
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
Note5 = "Plain SELECT (no DO block): the result row is read via ExecuteScalar, no RAISE NOTICE needed"
================================================================================
*/

-- The derived table always yields exactly one row (id NULL when not found), so ExecuteScalar never sees an empty result.
SELECT CASE
    WHEN x.id IS NULL
        THEN '0,Environment [' || COALESCE(@Name, 'NULL') || '] not found'
    ELSE CAST(x.id AS VARCHAR(10)) || ',Environment [' || @Name || '] with Id [' || CAST(x.id AS VARCHAR(10)) || '] found'
END
FROM (SELECT (SELECT id FROM {CFG:SchemaName}.{CFG:TableBaseName}environment WHERE name_lower = @NameLower LIMIT 1) AS id) x;
