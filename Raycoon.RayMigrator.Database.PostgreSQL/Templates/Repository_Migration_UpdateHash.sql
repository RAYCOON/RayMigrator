/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_UpdateHash"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2025-02-03.1"

[Description]
Function = """
Updates the hash fields of an existing Migration record.
Used by the Update-Hash command to synchronize repository hashes with changed files on disk.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level)
- Only updates hash-related fields, does not change state or result
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationId      = "INT | REQUIRED | The Migration record ID to update"
FileUpHash       = "VARCHAR(64) | REQUIRED | New SHA256 hash of the entire file"
FileUpConfigHash = "VARCHAR(64) | OPTIONAL | New SHA256 hash of TOML config section"
FileUpBlocksHash = "VARCHAR(64) | REQUIRED | New SHA256 hash of SQL blocks"

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

DO $$
DECLARE
    v_exists INT;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM {CFG:SchemaName}.{CFG:TableBaseName}migration_record
    WHERE id = @MigrationId;

    IF v_exists = 0 THEN
        RAISE EXCEPTION '-1,Migration with Id [%] does not exist', @MigrationId;
    END IF;

    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_record
    SET
        file_up_hash = @FileUpHash,
        file_up_config_hash = @FileUpConfigHash,
        file_up_blocks_hash = @FileUpBlocksHash
    WHERE id = @MigrationId;
END $$;

SELECT CAST(@MigrationId AS VARCHAR(10)) || ',Migration with Id [' || CAST(@MigrationId AS VARCHAR(10)) || '] hashes updated';
