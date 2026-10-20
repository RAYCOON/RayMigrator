/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_FixOrphaned"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-17.1"

[Description]
Function = """
Settles the open MigrationRecord entries (Pending or Executing) of an orphaned MigrationRun, which a run that ended
abruptly left behind (a crash, a kill, a second Ctrl+C or SIGTERM). Repository_MigrationRun_FixOrphaned closes the run
before this template runs.
"""

Behaviour = """
- Return value >= 0: Number of updated entries (0 = no open entry found - not an error)
- Return value < 0: Error
- Only updates entries with MigrationStatusId IN (10, 20) (Pending or Executing) of the given MigrationRun
- MigrationStatusId = 0 decides per entry from its progress; the first matching rule wins:
  a rollback was running (FileDownHash set) and FileDownBlocksMigrated >= FileDownBlocksTotal > 0: 50 (NotMigrated), the rollback had completed
  a rollback was running (FileDownHash set) otherwise: 30 (Failed), migrate-down continues a rollback with progress
  FileUpBlocksMigrated > 0 and >= FileUpBlocksTotal: 100 (Migrated), every block had committed
  FileUpBlocksMigrated > 0: 30 (Failed), the next migrate-up resumes after the committed blocks
  otherwise: 50 (NotMigrated)
- MigrationStatusId = 30, 50 or 100 sets that status on every open entry
- Every entry it updates gets FinishedAt, the time of the repair, where FinishedAt is NULL, and keeps DurationInMs:
  a file that was running has neither and gets the time of the repair without a duration; a record whose rollback
  was running keeps the end and the duration of its last terminal status
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration"
TableBaseName = "Table name prefix from Repository configuration"

[Parameters]
MigrationRunId    = "INT | REQUIRED | The MigrationRun ID whose open entries to settle"
MigrationStatusId = "TINYINT UNSIGNED | REQUIRED | 0 = decide per entry from its progress (rule in Behaviour); 30 (Failed), 50 (NotMigrated) or 100 (Migrated) = set on every open entry"

[ReturnValues]
# Format: SELECT 'code,message'
Success_None    = "0,No orphaned Migration entry found for MigrationRunId [N]"
Success_Updated = "N,Updated N orphaned Migration entry for MigrationRunId [M]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "code=0 means no orphaned entries found (not an error)"
Note4 = "The block counters stay as they are; FinishedAt gets the time of the repair (CURRENT_TIMESTAMP) where it is NULL (a file that was running); DurationInMs stays as it is (NULL for a file that was running; the duration of the last terminal status for a record whose rollback was running), because the time until the repair is no execution time; no history row is written"
================================================================================
*/

UPDATE {CFG:TableBaseName}migration_record
SET
    migration_status_id = CASE
        WHEN @MigrationStatusId <> 0 THEN @MigrationStatusId
        WHEN file_down_hash IS NOT NULL THEN
            CASE
                WHEN COALESCE(file_down_blocks_total, 0) > 0
                     AND COALESCE(file_down_blocks_migrated, 0) >= file_down_blocks_total THEN 50
                ELSE 30
            END
        WHEN file_up_blocks_migrated > 0 AND file_up_blocks_migrated >= file_up_blocks_total THEN 100
        WHEN file_up_blocks_migrated > 0 THEN 30
        ELSE 50
    END,
    finished_at = COALESCE(finished_at, CURRENT_TIMESTAMP)
WHERE migration_run_id = @MigrationRunId
    AND migration_status_id IN (10, 20);

SET @v_affected = ROW_COUNT();

SELECT CASE WHEN @v_affected = 0
    THEN CONCAT('0,No orphaned Migration entry found for MigrationRunId [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), ']')
    ELSE CONCAT(CAST(@v_affected AS CHAR), ',Updated ', CAST(@v_affected AS CHAR), ' orphaned Migration entry for MigrationRunId [', IFNULL(CAST(@MigrationRunId AS CHAR), 'NULL'), ']')
END;
