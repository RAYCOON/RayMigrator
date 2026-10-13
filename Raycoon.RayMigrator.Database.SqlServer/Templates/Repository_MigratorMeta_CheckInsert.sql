/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigratorMeta_CheckInsert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Registers the RayMigrator version in MigratorMeta if it is not registered yet and returns
the id of its row, which every MigrationRun of this version references as MigratorMetaId.
Runs after Repository_CheckCreate and the Repository_Upgrade_{n} steps (ADR-024).
"""

Behaviour = """
- Return value > 0: MigratorMetaId (logged at Debug level)
- Return value < 0: Error (logged at Error level, command aborted)
- One row per RayMigratorVersion and RepositoryDatabaseType; a parallel first run of the
  same version is serialised with UPDLOCK/HOLDLOCK
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
RayMigratorVersion     = "VARCHAR(100) | REQUIRED | The RayMigrator application version (e.g., '0.17.0')"
RepositoryDatabaseType = "VARCHAR(100) | REQUIRED | The database type for the repository (e.g., 'SqlServer')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N         = "N (MigratorMetaId),RayMigrator version [X] is registered with MigratorMetaId [N]"
Success_N_New     = "N (MigratorMetaId),RayMigrator version [X] registered with new MigratorMetaId [N]"
Error_-12_Multi   = "-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [...] RepositoryDatabaseType [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Use SYSUTCDATETIME() for all audit timestamps"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

	DECLARE
		@VersionId INT,
		@VersionIdString VARCHAR(10),
		@NumberOfRows INT;

	BEGIN TRANSACTION;

	SELECT
		@VersionId = Id
	FROM
		[{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
	WHERE
		RayMigratorVersion = @RayMigratorVersion
		AND RepositoryDatabaseType = @RepositoryDatabaseType;

	SET @NumberOfRows = @@rowcount;

	IF (@NumberOfRows = 1)
	BEGIN
		COMMIT TRANSACTION;
		SET @VersionIdString = CAST(@VersionId AS VARCHAR(10));
		SELECT @VersionIdString + ',RayMigrator version [' + @RayMigratorVersion + '] is registered with MigratorMetaId [' + @VersionIdString + ']';
		RETURN;
	END;

	IF (@NumberOfRows = 0)
	BEGIN
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] (RayMigratorVersion, RepositoryDatabaseType, CreatedAt)
		VALUES (@RayMigratorVersion, @RepositoryDatabaseType, SYSUTCDATETIME());

		SET @VersionId = SCOPE_IDENTITY();

		COMMIT TRANSACTION;
		SET @VersionIdString = CAST(@VersionId AS VARCHAR(10));
		SELECT @VersionIdString + ',RayMigrator version [' + @RayMigratorVersion + '] registered with new MigratorMetaId [' + @VersionIdString + ']';
		RETURN;
	END;

	ROLLBACK TRANSACTION;
	SELECT '-12,Multiple [MigratorMeta]-entries found for RayMigratorVersion [' + COALESCE(@RayMigratorVersion, 'NULL') + '] RepositoryDatabaseType [' + COALESCE(@RepositoryDatabaseType, 'NULL') + '].';

END TRY
BEGIN CATCH

    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
