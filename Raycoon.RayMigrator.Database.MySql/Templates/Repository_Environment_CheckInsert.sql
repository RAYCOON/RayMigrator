/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_CheckInsert"
DatabaseType   = "MySql"
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
- Environment NameLower has a UNIQUE index; a parallel first start of the same name keeps the row of the
  other start (ON DUPLICATE KEY UPDATE) instead of failing on the index (1062): both starts get its Id
  and may both report that they created it
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

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
Note3 = "Use CURRENT_TIMESTAMP for CreatedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note5 = "ON DUPLICATE KEY UPDATE tolerates the duplicate key of a parallel first start and no other error, where INSERT IGNORE would turn errors of the strict mode into warnings; the updated column is qualified with its table (1052). The Id is read from the table: LAST_INSERT_ID() and ROW_COUNT() cannot tell the inserted row from the kept one"
================================================================================
*/

SET @v_environment_id = (SELECT id FROM {CFG:TableBaseName}environment WHERE name_lower = @NameLower LIMIT 1);

-- A parallel first start may have inserted the name since the check: the duplicate key then keeps its row instead of
-- failing (1062). ON DUPLICATE KEY UPDATE tolerates the duplicate key and no other error, where INSERT IGNORE would turn
-- errors of the strict mode into warnings; the column is qualified with its table, so that it stays unambiguous (1052).
INSERT INTO {CFG:TableBaseName}environment (name, name_lower, created_at)
SELECT @Name, @NameLower, CURRENT_TIMESTAMP
FROM DUAL
WHERE @v_environment_id IS NULL AND @Name IS NOT NULL AND LENGTH(@Name) > 0
ON DUPLICATE KEY UPDATE {CFG:TableBaseName}environment.name_lower = {CFG:TableBaseName}environment.name_lower;

-- The Id comes from the table, whichever start inserted the row: LAST_INSERT_ID() and ROW_COUNT() cannot tell the
-- inserted row from the kept one
SET @v_new_id = IF(@v_environment_id IS NOT NULL, @v_environment_id,
    (SELECT id FROM {CFG:TableBaseName}environment WHERE name_lower = @NameLower LIMIT 1));

SELECT CASE
    WHEN @Name IS NULL OR LENGTH(@Name) = 0
        THEN CONCAT('-50,Environment with empty name [', IFNULL(@Name, 'NULL'), '] is not allowed!')
    WHEN @v_environment_id IS NOT NULL
        THEN CONCAT(CAST(@v_environment_id AS CHAR), ',Environment [', @Name, '] with Id [', CAST(@v_environment_id AS CHAR), '] found')
    ELSE
        CONCAT(CAST(@v_new_id AS CHAR), ',Environment [', @Name, '] with Id [', CAST(@v_new_id AS CHAR), '] successfully created')
END;
