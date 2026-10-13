/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Heartbeat"
DatabaseType   = "MySql"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Writes one heartbeat for a running MigrationRun: sets last_heartbeat_at to now while the row
is still open (finished_at IS NULL). The engine calls it every Repository.HeartbeatIntervalInSeconds
from its own connection; the heartbeat age decides whether another process may treat the run
as orphaned (schema version 2).
"""

Behaviour = """
- Return value 1: the row was open and updated
- Return value 0: the run has already finished (not an error)
- Return value < 0: Error (logged at Warning level by the heartbeat loop, the run continues)
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MySQL schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MySQL (DAL-018)"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the running MigrationRun"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1 = "1,Heartbeat of MigrationRun [N] written"
Success_0 = "0,MigrationRun [N] is not open"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use CURRENT_TIMESTAMP for the heartbeat (the DAL sets the session time zone to UTC)"
Note4 = "DAL-018: All identifiers use unquoted snake_case"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_run
SET last_heartbeat_at = CURRENT_TIMESTAMP
WHERE id = @MigrationRunId
    AND finished_at IS NULL;

SET @v_affected = ROW_COUNT();

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('0,MigrationRun [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), '] is not open')
    ELSE CONCAT('1,Heartbeat of MigrationRun [', CAST(@MigrationRunId AS CHAR), '] written')
END;
