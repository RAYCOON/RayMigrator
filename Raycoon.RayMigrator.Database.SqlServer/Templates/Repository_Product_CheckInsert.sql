-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Product_CheckInsert"
DatabaseType   = "SqlServer"
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
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
Name      = "NVARCHAR(100) | REQUIRED | The product name in original casing (e.g., 'MyApplication')"
NameLower = "NVARCHAR(100) | REQUIRED | The product name in lowercase (e.g., 'myapplication') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (ProductId),Product [Name] with Id [N] found"
Success_Created  = "N (ProductId),Product [Name] with Id [N] successfully created"
Error_-20_Empty  = "-20,Product with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for CreatedAt timestamp"
Note4 = "Product.NameLower has UNIQUE index - duplicate names will fail at DB level"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

-- Mandatory RepositoryVersion: DO NOT change manually, otherwise repository-inconsistencies may occur that results in migration errors !!!

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    if (@Name IS NULL OR LEN(@Name) = 0)
        begin
            SELECT '-20,Product with empty name [' + COALESCE(@Name, 'NULL') + '] is not allowed!'
            return;
        end;

    declare
        @ProductId int,
        @numberOfRows int;

    select @ProductId = [Id] from [{CFG:SchemaName}].[{CFG:TableBaseName}Product] where [NameLower] = @NameLower;
    SET @numberOfRows = @@rowcount;

    if (@numberOfRows = 1)
        begin
            SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] found';
            return;
        end;

    if (@numberOfRows = 0)
        begin
            INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}Product] (Name, NameLower, CreatedAt)
            VALUES (@Name, @NameLower, SYSUTCDATETIME());

            SET @ProductId = SCOPE_IDENTITY();
            SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] successfully created';
            return;
        end;

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
