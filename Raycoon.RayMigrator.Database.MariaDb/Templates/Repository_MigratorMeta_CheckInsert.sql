/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigratorMeta_CheckInsert"
DatabaseType   = "MariaDb"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Registers the RayMigrator version in migrator_meta if it is not registered yet and returns
the id of its row, which every migration_run of this version references as migrator_meta_id.
Runs after Repository_CheckCreate and the Repository_Upgrade_{n} steps (ADR-024).
"""

Behaviour = """
- Return value > 0: MigratorMetaId (logged at Debug level)
- Return value < 0: Error (logged at Error level, command aborted)
- One row per RayMigratorVersion and RepositoryDatabaseType; a parallel first run of the
  same version is serialised with GET_LOCK()
"""

[ConfigPlaceholders]
SchemaName    = "Not used - MariaDB schema equals the database of the connection"
TableBaseName = "Table name prefix from Repository configuration - MUST be lowercase for MariaDB (DAL-018)"

[Parameters]
RayMigratorVersion     = "VARCHAR(100) | REQUIRED | The RayMigrator application version (e.g., '0.17.0')"
RepositoryDatabaseType = "VARCHAR(100) | REQUIRED | The database type for the repository (e.g., 'MariaDb')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N         = "N (MigratorMetaId),RayMigrator version [X] is registered with MigratorMetaId [N]"
Success_N_New     = "N (MigratorMetaId),RayMigrator version [X] registered with new MigratorMetaId [N]"
Error_-12_Multi   = "-12,Multiple [migrator_meta]-entries found for RayMigratorVersion [...] RepositoryDatabaseType [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use CURRENT_TIMESTAMP for all audit timestamps (the DAL sets the session time zone to UTC)"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

-- Serialise a parallel first run of the same RayMigrator version: the lookup and the insert are two statements.
SET @v_lock_name = CONCAT('raymigrator_meta_', DATABASE());
SET @v_lock_acquired = GET_LOCK(@v_lock_name, 60);

SET @v_version_id = (
    SELECT id FROM {CFG:TableBaseName}migrator_meta
    WHERE raymigrator_version = @RayMigratorVersion
      AND repository_database_type = @RepositoryDatabaseType
    LIMIT 1
);

SET @v_number_of_rows = (
    SELECT COUNT(*) FROM {CFG:TableBaseName}migrator_meta
    WHERE raymigrator_version = @RayMigratorVersion
      AND repository_database_type = @RepositoryDatabaseType
);

INSERT INTO {CFG:TableBaseName}migrator_meta (raymigrator_version, repository_database_type, created_at)
SELECT @RayMigratorVersion, @RepositoryDatabaseType, CURRENT_TIMESTAMP
FROM DUAL
WHERE @v_number_of_rows = 0;

SET @v_final_version_id = IF(@v_version_id IS NOT NULL, @v_version_id, LAST_INSERT_ID());

DO RELEASE_LOCK(@v_lock_name);

SELECT CASE
    WHEN @v_number_of_rows > 1 THEN
        CONCAT('-12,Multiple [migrator_meta]-entries found for RayMigratorVersion [', IFNULL(@RayMigratorVersion, 'NULL'), '] RepositoryDatabaseType [', IFNULL(@RepositoryDatabaseType, 'NULL'), '].')
    WHEN @v_number_of_rows = 1 THEN
        CONCAT(CAST(@v_version_id AS CHAR), ',RayMigrator version [', @RayMigratorVersion, '] is registered with MigratorMetaId [', CAST(@v_version_id AS CHAR), ']')
    ELSE
        CONCAT(CAST(@v_final_version_id AS CHAR), ',RayMigrator version [', @RayMigratorVersion, '] registered with new MigratorMetaId [', CAST(@v_final_version_id AS CHAR), ']')
END;
