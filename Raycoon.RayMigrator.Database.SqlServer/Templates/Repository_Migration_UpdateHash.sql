-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_UpdateHash"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-02-03.1"

[Description]
Function = """
Updates the hash fields (FileUpHash, FileUpConfigHash, FileUpBlocksHash) of an existing
Migration record. Used by the Update-Hash command to synchronize repository hashes
with changed migration files on disk.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Only updates hash-related fields, does not change state or result
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
MigrationId      = "INT | REQUIRED | The Migration record ID to update"
FileUpHash       = "VARCHAR(100) | REQUIRED | New SHA256 hash of the entire file"
FileUpConfigHash = "VARCHAR(100) | OPTIONAL | New SHA256 hash of TOML config section"
FileUpBlocksHash = "VARCHAR(100) | REQUIRED | New SHA256 hash of SQL blocks"

[ReturnValues]
# Format: SELECT 'code,message'
Success_Updated   = "N (MigrationId),Migration with Id [N] hashes updated"
Error_-1_NotFound = "-1,Migration with Id [N] does not exist"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Only hash fields are updated - state and result remain unchanged"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

    -- Verify the Migration exists
    IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] WHERE Id = @MigrationId)
    BEGIN
        SELECT '-1,Migration with Id [' + COALESCE(CAST(@MigrationId AS VARCHAR(10)), 'NULL') + '] does not exist';
        RETURN;
    END;

    -- Update the hash fields
    UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    SET
        FileUpHash = @FileUpHash,
        FileUpConfigHash = @FileUpConfigHash,
        FileUpBlocksHash = @FileUpBlocksHash
    WHERE Id = @MigrationId;

    SELECT CAST(@MigrationId AS VARCHAR(10)) + ',Migration with Id [' + CAST(@MigrationId AS VARCHAR(10)) + '] hashes updated';

END TRY
BEGIN CATCH

    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
