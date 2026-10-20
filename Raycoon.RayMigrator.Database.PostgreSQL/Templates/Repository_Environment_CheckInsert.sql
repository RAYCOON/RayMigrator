/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_CheckInsert"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Checks if an Environment exists by NameLower. If not, inserts a new Environment record.
Returns the existing or new EnvironmentId.
"""

Behaviour = """
- Return value >= 0: Success (EnvironmentId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Idempotent: can be called multiple times safely
- Environment NameLower has a UNIQUE index; a parallel first start of the same name inserts nothing
  (ON CONFLICT DO NOTHING) and gets the Id of the row of the other start instead of failing on the
  index (23505)
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
Name      = "VARCHAR(100) | REQUIRED | The environment name in original casing (e.g., 'Docker')"
NameLower = "VARCHAR(100) | REQUIRED | The environment name in lowercase (e.g., 'docker') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (EnvironmentId),Environment [Name] with Id [N] found"
Success_Created  = "N (EnvironmentId),Environment [Name] with Id [N] successfully created"
Error_-50_Empty  = "-50,Environment with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use NOW() for CreatedAt timestamp (TIMESTAMPTZ column)"
Note4 = "Environment.NameLower has a UNIQUE index: the INSERT tolerates the conflict on it (ON CONFLICT (name_lower) DO NOTHING), which waits for the transaction of a parallel first start of the same name; the Id then comes from a SELECT"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

DO $$
DECLARE
    v_environment_id INT;
    v_count INT;
BEGIN
    IF @Name IS NULL OR LENGTH(@Name) = 0 THEN
        RAISE EXCEPTION '-50,Environment with empty name [%] is not allowed!', COALESCE(@Name, 'NULL');
    END IF;

    SELECT id INTO v_environment_id
    FROM {CFG:SchemaName}.{CFG:TableBaseName}environment
    WHERE name_lower = @NameLower;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    IF v_count = 1 THEN
        RAISE NOTICE '%,Environment [%] with Id [%] found', v_environment_id, @Name, v_environment_id;
        RETURN;
    END IF;

    -- A parallel first start may have inserted the name since the SELECT: ON CONFLICT waits for its transaction and then
    -- inserts nothing instead of failing on the unique index (23505), and the Id comes from its row
    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}environment (name, name_lower, created_at)
    VALUES (@Name, @NameLower, NOW())
    ON CONFLICT (name_lower) DO NOTHING
    RETURNING id INTO v_environment_id;

    IF v_environment_id IS NULL THEN
        SELECT id INTO v_environment_id
        FROM {CFG:SchemaName}.{CFG:TableBaseName}environment
        WHERE name_lower = @NameLower;

        RAISE NOTICE '%,Environment [%] with Id [%] found', v_environment_id, @Name, v_environment_id;
        RETURN;
    END IF;

    RAISE NOTICE '%,Environment [%] with Id [%] successfully created', v_environment_id, @Name, v_environment_id;
END $$;
