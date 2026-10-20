/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRun_Heartbeat"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Writes one heartbeat for a running MigrationRun: sets LastHeartbeatAt to now while the row
is still open (FinishedAt IS NULL). The engine calls it every Repository.HeartbeatIntervalInSeconds
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
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
MigrationRunId = "INT | REQUIRED | The ID of the running MigrationRun"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1 = "1,Heartbeat of MigrationRun [N] written"
Success_0 = "0,MigrationRun [N] is not open"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use SYSUTCDATETIME() for the heartbeat; the orphan rule compares it with the engine's clock-free SQL date arithmetic"
================================================================================
*/

SET NOCOUNT ON;

UPDATE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]
SET [LastHeartbeatAt] = SYSUTCDATETIME()
WHERE [Id] = @MigrationRunId
    AND [FinishedAt] IS NULL;

IF @@ROWCOUNT = 0
    SELECT '0,MigrationRun [' + COALESCE(CAST(@MigrationRunId AS VARCHAR(10)), 'NULL') + '] is not open';
ELSE
    SELECT '1,Heartbeat of MigrationRun [' + CAST(@MigrationRunId AS VARCHAR(10)) + '] written';
