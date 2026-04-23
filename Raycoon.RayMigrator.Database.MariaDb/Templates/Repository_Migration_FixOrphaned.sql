/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_FixOrphaned"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Updates orphaned Migration entries (Pending/Executing status) for a given MigrationRun.
Sets MigrationStatusId based on user choice (Failed, NotMigrated or Migrated).
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no orphaned migrations found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing)
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The MigrationRun ID whose orphaned migrations to fix"
MigrationStatusId = "TINYINT UNSIGNED | REQUIRED | Target status: 30 (Failed), 50 (NotMigrated) or 100 (Migrated)"

[ReturnValues]
# Format: SELECT 'code,message'
Success_None    = "0,No orphaned Migration entry found for MigrationRunId [N]"
Success_Updated = "N,Updated N orphaned Migration entry for MigrationRunId [M]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "code=0 means no orphaned entries found (not an error)"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_record
SET migration_status_id = @MigrationStatusId
WHERE migration_run_id = @MigrationRunId
    AND migration_status_id IN (10, 20);

SET @v_affected = ROW_COUNT();

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('0,No orphaned Migration entry found for MigrationRunId [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), ']')
    ELSE CONCAT(CAST(@v_affected AS CHAR), ',Updated ', CAST(@v_affected AS CHAR), ' orphaned Migration entry for MigrationRunId [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), ']')
END;
