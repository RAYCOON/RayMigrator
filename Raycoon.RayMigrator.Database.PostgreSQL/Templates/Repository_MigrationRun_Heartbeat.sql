/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Heartbeat"
DatabaseType   = "PostgreSQL"
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
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the running MigrationRun"

[ReturnValues]
# Format: RAISE NOTICE 'code,message'
Success_1 = "1,Heartbeat of MigrationRun [N] written"
Success_0 = "0,MigrationRun [N] is not open"

[ModificationNotes]
Note1 = "Result format: 'code,message' via RAISE NOTICE - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use NOW() for the heartbeat (writes to a TIMESTAMPTZ column)"
Note4 = "DAL-017: All identifiers use unquoted snake_case"
================================================================================
*/

DO $$
DECLARE
    v_affected INT;
BEGIN
    UPDATE {CFG:SchemaName}.{CFG:TableBaseName}migration_run
    SET last_heartbeat_at = NOW()
    WHERE id = @MigrationRunId
        AND finished_at IS NULL;

    GET DIAGNOSTICS v_affected = ROW_COUNT;

    IF v_affected = 0 THEN
        RAISE NOTICE '0,MigrationRun [%] is not open', COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL');
    ELSE
        RAISE NOTICE '1,Heartbeat of MigrationRun [%] written', @MigrationRunId;
    END IF;
END $$;
