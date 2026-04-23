-- noinspection SqlNoDataSourceInspectionForFile

/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Migration_FixOrphaned"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-02-12.1"

[Description]
Function = """
Updates orphaned Migration entries (Pending/Executing status) for a given MigrationRun.
Sets MigrationStatusId based on user choice (Migrated or NotMigrated).
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no orphaned migrations found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing)
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The MigrationRun ID whose orphaned migrations to fix"
MigrationStatusId = "TINYINT | REQUIRED | Target status: 50 (NotMigrated) or 100 (Migrated)"

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

SET NOCOUNT ON;

UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
SET
    [MigrationStatusId] = @MigrationStatusId
WHERE [MigrationRunId] = @MigrationRunId
    AND [MigrationStatusId] IN (10, 20);

DECLARE @v_affected INT = @@ROWCOUNT;

IF @v_affected = 0
    SELECT '0,No orphaned Migration entry found for MigrationRunId [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + ']';
ELSE
    SELECT CAST(@v_affected AS VARCHAR(10)) + ',Updated ' + CAST(@v_affected AS VARCHAR(10)) + ' orphaned Migration entry for MigrationRunId [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + ']';
