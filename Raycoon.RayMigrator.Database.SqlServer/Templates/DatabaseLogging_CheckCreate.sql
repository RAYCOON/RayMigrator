/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist, and the indexes the
readers of the log filter by (rows per run, rows per time) when they are missing.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists (also when only the indexes were added)
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Creates schema if not exists
- Inserts master data for MigrationEvent types
- Creates the indexes on MigrationLog (MigrationRunId) and (CreatedAt) when missing, also on a log
  table an older engine created; a start that finds both runs no DDL
- One transaction, serialised with sp_getapplock against a parallel start
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
# NOTE: For DatabaseLogging_* templates, values come from the 'Logging' section of appsettings
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'Log_')"

[Parameters]
# No SQL parameters required for this template

[ReturnValues]
# Format: SELECT 'code,message'
Success_0_Exists  = "0,Database logging infrastructure already exists"
Success_1_Created = "1,Database logging infrastructure successfully created"
Error_-1_Lock     = "-1,Could not acquire the database logging lock"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: MigrationEvent (lookup), MigrationLog (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "MigrationLog.CreatedAt defaults to SYSUTCDATETIME()"
Note6 = "The indexes stand outside the creation branch and are created only when sys.indexes does not list them"
Note7 = "sp_getapplock serialises a parallel start of another host: without it the second of two CREATE INDEX of the same name fails (1913); the resource differs from the repository schema lock, so the log never waits for a repository step"
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

	BEGIN TRANSACTION;

	-- A parallel start of another host on the same log tables waits here, then finds the tables and the indexes.
	DECLARE @LockResult INT;
	EXEC @LockResult = sp_getapplock @Resource = 'RayMigrator_Log_{CFG:SchemaName}_{CFG:TableBaseName}', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
	IF (@LockResult < 0)
	BEGIN
		ROLLBACK TRANSACTION;
		SELECT '-1,Could not acquire the database logging lock (sp_getapplock returned [' + CAST(@LockResult AS VARCHAR(10)) + '])';
		RETURN;
	END;

	DECLARE @Created BIT = 0;

	IF (OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigrationLog', 'U') IS NULL)
	BEGIN

				IF SCHEMA_ID('{CFG:SchemaName}') IS NULL EXECUTE('CREATE SCHEMA [{CFG:SchemaName}];'); -- AUTHORIZATION ???
		
				CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationEvent] ( 
					Id                   int      NOT NULL,
					Name                 nvarchar(100)     NOT NULL,
					Description          nvarchar(1000)      NULL,
					CONSTRAINT pk_{CFG:TableBaseName}MigrationEvent PRIMARY KEY  ( Id ) 
				 );


				CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] (
					Id                   bigint    IDENTITY(1,1)  NOT NULL,
					LogLevelId           tinyint      NOT NULL,
					MigrationEventId     int      NULL,
					RunModeId            tinyint      NULL,
					ProductId            int      NULL,
					EnvironmentId        int      NULL,
					MigrationRunId       int      NULL,
					MigrationRecordId          int      NULL,
					ReleaseVersion       nvarchar(100)      NULL,
					TargetGroupAlias     nvarchar(100)      NULL,
					TargetAlias          nvarchar(100)      NULL,
					Filename             nvarchar(300)      NULL,
					FileOrderId          int      NULL,
					FileBlockId          int      NULL,
					Message              nvarchar(max)      NULL,
					CreatedAt            datetime2(3) DEFAULT SYSUTCDATETIME()  NOT NULL,
					CONSTRAINT pk_{CFG:TableBaseName}MigrationLog PRIMARY KEY  ( Id )
				 );


				-- Master data: MigrationEvent
				INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationEvent] ([Id], [Name], [Description])
				VALUES
					(0, 'UnspecifiedEvent', N''),
					(10, 'CommandLineParsing', N''),
					(20, 'EnvironmentVariableReplacement', N''),
					(31, 'CreateDatabaseLogger', N''),
					(40, 'ValidateRayMigratorOptions', N''),
					(50, 'CreateApplicationHost', N''),
					(60, 'InitializeDalSpecificProperties', N''),
					(70, 'ValidateConnectionStrings', N''),
					(80, 'RayMigratorServiceStart', N''),
					(100, 'TemplateExecutionRepositoryCheckCreate', N''),
					(101, 'TemplateExecutionRepositoryUpgrade', N''),
					(102, 'TemplateExecutionRepositoryMigratorMetaCheckInsert', N''),
					(110, 'TemplateExecutionRepositoryMigrationRunInsert', N''),
					(111, 'TemplateExecutionRepositoryMigrationRunUpdate', N''),
					(112, 'TemplateExecutionRepositoryMigrationRunSelectOrphaned', N''),
					(113, 'TemplateExecutionRepositoryMigrationRunFixOrphaned', N''),
					(114, 'TemplateExecutionRepositoryMigrationFixOrphaned', N''),
					(115, 'TemplateExecutionRepositoryMigrationRunHeartbeat', N''),
					(120, 'TemplateExecutionRepositoryProductCheckInsert', N''),
					(121, 'TemplateExecutionRepositoryEnvironmentCheckInsert', N''),
					(122, 'TemplateExecutionRepositoryProductSelect', N''),
					(123, 'TemplateExecutionRepositoryEnvironmentSelect', N''),
					(130, 'TemplateExecutionRepositoryMigrationInsert', N''),
					(131, 'TemplateExecutionRepositoryMigrationUpdate', N''),
					(132, 'TemplateExecutionRepositoryMigrationGetInterrupted', N''),
					(133, 'TemplateExecutionRepositoryMigrationUpdateRollback', N''),
					(134, 'TemplateExecutionRepositoryMigrationSelect', N''),
					(135, 'TemplateExecutionRepositoryMigrationUpdateHash', N''),
					(136, 'TemplateExecutionRepositoryMigrationRunSelect', N''),
					(137, 'TemplateExecutionRepositoryMigrationRecordHistorySelect', N''),
					(1000, 'RayMigratorServiceShutdown', N'');

				SET @Created = 1;

	END;

	-- Outside the creation branch, so that a log table an older engine created gets the indexes at its next start.
	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationLog_MigrationRunId' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationLog', N'U'))
		CREATE INDEX ix_{CFG:TableBaseName}MigrationLog_MigrationRunId ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] ( MigrationRunId );

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationLog_CreatedAt' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationLog', N'U'))
		CREATE INDEX ix_{CFG:TableBaseName}MigrationLog_CreatedAt ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] ( CreatedAt );

	COMMIT TRANSACTION;

	IF (@Created = 1)
		SELECT '1,Database logging infrastructure successfully created';
	ELSE
		SELECT '0,Database logging infrastructure already exists';

END TRY
BEGIN CATCH
    
    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;
	
END CATCH;

