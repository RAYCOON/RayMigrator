/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_CheckCreate"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Checks for database logging infrastructure existence.
Creates MigrationLog and MigrationEvent tables if they don't exist.
Used for database-level logging of migration events.
"""

Behaviour = """
- Return value = 0: Logging infrastructure already exists
- Return value = 1: Logging infrastructure was created
- Return value < 0: Error (logged at Error level)
- Creates schema if not exists
- Inserts master data for MigrationEvent types
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

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Tables created: MigrationEvent (lookup), MigrationLog (data)"
Note4 = "MigrationEvent master data includes event IDs 0-1000"
Note5 = "MigrationLog.CreatedAt defaults to SYSUTCDATETIME()"
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

        IF (OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigrationLog', 'U') IS NULL)
        BEGIN
			
			BEGIN TRANSACTION;

				IF SCHEMA_ID('{CFG:SchemaName}') IS NULL EXECUTE('CREATE SCHEMA [{CFG:SchemaName}];'); -- AUTHORIZATION ???
		
				CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationEvent] ( 
					Id                   int      NOT NULL,
					Name                 nvarchar(100)     NOT NULL,
					Description          nvarchar(1000)      NULL,
					CONSTRAINT pk_MigrationEvent PRIMARY KEY  ( Id ) 
				 );


				CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog] (
					Id                   bigint    IDENTITY(1,1)  NOT NULL,
					LogLevelId           tinyint      NOT NULL,
					MigrationEventId     int      NULL,
					RunModeId            tinyint      NULL,
					ProductId            int      NULL,
					EnvironmentId        int      NULL,
					MigrationRunId       int      NULL,
					MigrationId          int      NULL,
					ReleaseVersion       nvarchar(100)      NULL,
					TargetGroupAlias     nvarchar(100)      NULL,
					TargetAlias          nvarchar(100)      NULL,
					Filename             nvarchar(300)      NULL,
					FileOrderId          int      NULL,
					FileBlockId          int      NULL,
					Message              nvarchar(max)      NULL,
					CreatedAt            datetime2(3) DEFAULT SYSUTCDATETIME()  NOT NULL,
					CONSTRAINT pk_Log PRIMARY KEY  ( Id )
				 );


				-- Master data: MigrationEvent
				INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationEvent] ([Id], [Name], [Description])
				VALUES
					(0, 'UnspecifiedEvent', N''),
					(10, 'CommandLineParsing', N''),
					(20, 'EnvironmentVariableReplacement', N''),
					(31, 'CreateDatabaseLogger', N''),
					(32, 'CreateCompositeLogger', N''),
					(40, 'ValidateRayMigratorOptions', N''),
					(50, 'CreateApplicationHost', N''),
					(60, 'InitializeDalSpecificProperties', N''),
					(70, 'ValidateConnectionStrings', N''),
					(80, 'RayMigratorServiceStart', N''),
					(100, 'CreateAndStartRayMigratorService', N''),
					(1000, 'RayMigratorServiceShutdown', N'');

			COMMIT TRANSACTION;
			
			SELECT '1,Database logging infrastructure successfully created';
			RETURN;

		END
		ELSE
		BEGIN

		    SELECT '0,Database logging infrastructure already exists';
			RETURN;

		END;

END TRY
BEGIN CATCH
    
    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;
	
END CATCH;

