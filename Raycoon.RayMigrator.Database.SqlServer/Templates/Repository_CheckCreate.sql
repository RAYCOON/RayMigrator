/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-14.1"

[Description]
Function = """
Reports the schema version of the repository (ADR-024). Creates the schema and the
RepositorySchemaVersion table when they do not exist; creates nothing else. The engine
applies the missing Repository_Upgrade_{n} steps afterwards.
"""

Behaviour = """
- First code: highest applied schema version, 0 when the repository has none
- Second code: 1 when the MigratorMeta table exists, 0 otherwise. With version 0 this
  means a repository of a RayMigrator before 0.17.0, which the engine refuses
- Idempotent; a parallel start on the same repository is serialised with sp_getapplock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
# none

[ReturnValues]
# Format: SELECT 'schemaVersion,unversionedTables,message'
Success_N        = "N (schema version),0|1,RayMigrator repository has schema version [N]"
Success_Legacy   = "0,1,RayMigrator repository tables exist without a schema version"
Success_None     = "0,0,RayMigrator repository does not exist yet"

[ModificationNotes]
Note1 = "SELECT result format: 'schemaVersion,unversionedTables,message' - DO NOT change this format"
Note2 = "No commas allowed in messages"
Note3 = "Creates only the schema and the RepositorySchemaVersion table; the repository tables come from Repository_Upgrade_1"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

	BEGIN TRANSACTION;

	DECLARE @LockResult INT;
	EXEC @LockResult = sp_getapplock @Resource = 'RayMigrator_{CFG:SchemaName}_{CFG:TableBaseName}', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
	IF (@LockResult < 0)
	BEGIN
		ROLLBACK TRANSACTION;
		SELECT '-1,Could not acquire the repository schema lock (sp_getapplock returned [' + CAST(@LockResult AS VARCHAR(10)) + '])';
		RETURN;
	END;

	IF SCHEMA_ID('{CFG:SchemaName}') IS NULL EXECUTE('CREATE SCHEMA [{CFG:SchemaName}];');

	IF OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}RepositorySchemaVersion', 'U') IS NULL
	BEGIN
		CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] (
			Version              int            NOT NULL,
			AppliedAt            datetime2(3)   NOT NULL,
			RayMigratorVersion   nvarchar(100)  NOT NULL,
			CONSTRAINT pk_{CFG:TableBaseName}RepositorySchemaVersion PRIMARY KEY ( Version )
		);
	END;

	DECLARE @SchemaVersion INT = (SELECT COALESCE(MAX([Version]), 0) FROM [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion]);
	DECLARE @UnversionedTables INT = CASE WHEN OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigratorMeta', 'U') IS NULL THEN 0 ELSE 1 END;

	COMMIT TRANSACTION;

	SELECT CAST(@SchemaVersion AS VARCHAR(10)) + ',' + CAST(@UnversionedTables AS VARCHAR(1)) + ',' +
		CASE
			WHEN @SchemaVersion > 0 THEN 'RayMigrator repository has schema version [' + CAST(@SchemaVersion AS VARCHAR(10)) + ']'
			WHEN @UnversionedTables = 1 THEN 'RayMigrator repository tables exist without a schema version'
			ELSE 'RayMigrator repository does not exist yet'
		END;

END TRY
BEGIN CATCH

    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
