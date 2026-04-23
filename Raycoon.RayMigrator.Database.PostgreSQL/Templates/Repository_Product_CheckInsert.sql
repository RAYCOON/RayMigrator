/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Product_CheckInsert"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

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
Note3 = "Use NOW() for CreatedAt timestamp (TIMESTAMPTZ column)"
Note4 = "Product.NameLower has UNIQUE index - duplicate names will fail at DB level"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

DO $$
DECLARE
    v_product_id INT;
    v_count INT;
BEGIN
    IF @Name IS NULL OR LENGTH(@Name) = 0 THEN
        RAISE EXCEPTION '-20,Product with empty name [%] is not allowed!', COALESCE(@Name, 'NULL');
    END IF;

    SELECT id INTO v_product_id
    FROM {CFG:SchemaName}.{CFG:TableBaseName}product
    WHERE name_lower = @NameLower;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    IF v_count = 1 THEN
        RAISE NOTICE '%,Product [%] with Id [%] found', v_product_id, @Name, v_product_id;
        RETURN;
    END IF;

    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}product (name, name_lower, created_at)
    VALUES (@Name, @NameLower, NOW())
    RETURNING id INTO v_product_id;

    RAISE NOTICE '%,Product [%] with Id [%] successfully created', v_product_id, @Name, v_product_id;
END $$;
