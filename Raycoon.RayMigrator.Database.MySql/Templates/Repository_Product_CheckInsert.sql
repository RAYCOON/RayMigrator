/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Product_CheckInsert"
DatabaseType   = "MySql"
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
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
Name      = "VARCHAR(100) | REQUIRED | The product name in original casing (e.g., 'MyApplication')"
NameLower = "VARCHAR(100) | REQUIRED | The product name in lowercase (e.g., 'myapplication') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (ProductId),Product [Name] with Id [N] found"
Success_Created  = "N (ProductId),Product [Name] with Id [N] successfully created"
Error_-20_Empty  = "-20,Product with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use CURRENT_TIMESTAMP for CreatedAt (session time_zone='+00:00' ensures UTC)"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET @v_product_id = (SELECT id FROM {CFG:TableBaseName}product WHERE name_lower = @NameLower LIMIT 1);

INSERT INTO {CFG:TableBaseName}product (name, name_lower, created_at)
SELECT @Name, @NameLower, CURRENT_TIMESTAMP
FROM DUAL
WHERE @v_product_id IS NULL AND @Name IS NOT NULL AND LENGTH(@Name) > 0;

SET @v_new_id = IF(@v_product_id IS NOT NULL, @v_product_id, LAST_INSERT_ID());

SELECT CASE
    WHEN @Name IS NULL OR LENGTH(@Name) = 0
        THEN CONCAT('-20,Product with empty name [', IFNULL(@Name, 'NULL'), '] is not allowed!')
    WHEN @v_product_id IS NOT NULL
        THEN CONCAT(CAST(@v_product_id AS CHAR), ',Product [', @Name, '] with Id [', CAST(@v_product_id AS CHAR), '] found')
    ELSE
        CONCAT(CAST(@v_new_id AS CHAR), ',Product [', @Name, '] with Id [', CAST(@v_new_id AS CHAR), '] successfully created')
END;
