-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Environment_CheckInsert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-16.1"

[Description]
Function = """
Checks if an Environment exists by NameLower. If not, inserts a new Environment record.
Returns the existing or new EnvironmentId.
"""

Behaviour = """
- Return value >= 0: Success (EnvironmentId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Idempotent: can be called multiple times safely
- Environment NameLower has UNIQUE index - duplicate names will fail at DB level
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
Name      = "NVARCHAR(100) | REQUIRED | The environment name in original casing (e.g., 'Docker')"
NameLower = "NVARCHAR(100) | REQUIRED | The environment name in lowercase (e.g., 'docker') - pre-computed in C#"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Existing = "N (EnvironmentId),Environment [Name] with Id [N] found"
Success_Created  = "N (EnvironmentId),Environment [Name] with Id [N] successfully created"
Error_-50_Empty  = "-50,Environment with empty name [NULL] is not allowed!"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for CreatedAt timestamp"
Note4 = "Environment.NameLower has UNIQUE index - duplicate names will fail at DB level"
Note5 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

-- Mandatory RepositoryVersion: DO NOT change manually, otherwise repository-inconsistencies may occur that results in migration errors !!!

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    if (@Name IS NULL OR LEN(@Name) = 0)
        begin
            SELECT '-50,Environment with empty name [' + COALESCE(@Name, 'NULL') + '] is not allowed!'
            return;
        end;

    declare
        @EnvironmentId int,
        @numberOfRows int;

    select @EnvironmentId = [Id] from [{CFG:SchemaName}].[{CFG:TableBaseName}Environment] where [NameLower] = @NameLower;
    SET @numberOfRows = @@rowcount;

    if (@numberOfRows = 1)
        begin
            SELECT CAST(@EnvironmentId AS varchar(10)) + ',Environment [' + @Name + '] with Id [' + CAST(@EnvironmentId AS varchar(10)) + '] found';
            return;
        end;

    if (@numberOfRows = 0)
        begin
            INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}Environment] (Name, NameLower, CreatedAt)
            VALUES (@Name, @NameLower, SYSUTCDATETIME());

            SET @EnvironmentId = SCOPE_IDENTITY();
            SELECT CAST(@EnvironmentId AS varchar(10)) + ',Environment [' + @Name + '] with Id [' + CAST(@EnvironmentId AS varchar(10)) + '] successfully created';
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
