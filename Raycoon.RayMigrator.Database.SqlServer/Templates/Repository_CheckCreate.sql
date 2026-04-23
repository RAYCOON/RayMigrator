/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Checks for repository existence and completeness. Creates RayMigrator
infrastructure on the target database if necessary. Returns the VersionId.
"""

Behaviour = """
- Return value >= 0: Success (logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Creates schema if not exists
- Creates all 11 repository tables with master data
- Inserts new MigratorMeta record on first run or version change
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
RayMigratorVersion     = "VARCHAR(20) | REQUIRED | The RayMigrator application version (e.g., '3.0.0')"
RepositoryDatabaseType = "VARCHAR(20) | REQUIRED | The database type for the repository (e.g., 'SqlServer')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N           = "N (VersionId),RayMigrator repository already exists. Using VersionId [N]."
Success_N_Created   = "N (VersionId),RayMigrator repository-tables with master data and new VersionId [N] successfully created"
Success_N_NewVer    = "N (VersionId),RayMigrator repository already exists. New VersionId [N] created."
Error_-10_Incomplete        = "-10,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of [11]."
Error_-11_PartialNoVersion  = "-11,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of the expected amount of [0]."
Error_-12_MultipleVersions  = "-12,Multiple [MigratorMeta]-entries found for RepositoryVersion [...] RepositoryDatabaseType [...] RayMigratorVersion [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for all timestamps"
Note4 = "RepositoryVersion constant MUST match Version in header"
Note5 = "Tables created: MigratorMeta, Product, Environment, MigrationRun, MigrationRunMeta, MigrationRecord, MigrationRecordHistory, MigrationRunMode, MigrationOperation, MigrationRunResult, MigrationStatus"
Note6 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/

-- Mandatory RepositoryVersion: DO NOT change manually, otherwise repository-inconsistencies may occur that results in migration errors !!!
DECLARE @RepositoryVersion VARCHAR(20) = '2026-04-18.1';
--DECLARE @RayMigratorVersion varchar(20) = '2025-02-13.1';
--DECLARE @RepositoryDatabaseType varchar(20) = 'SqlServer';

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY

	DECLARE 
		@VersionId INT,
		@VersionIdString VARCHAR(10),
		@NumberOfRows INT,
		@NumberOfTablesFound INT;

	SELECT 
		@NumberOfTablesFound = COUNT(*) 
	FROM sys.tables t
	INNER JOIN sys.schemas s 
		ON t.schema_id = s.schema_id
	WHERE 
		s.name = '{CFG:SchemaName}'
		AND t.name IN (
			'{CFG:TableBaseName}MigratorMeta',
			'{CFG:TableBaseName}Product',
			'{CFG:TableBaseName}MigrationRun',
			'{CFG:TableBaseName}MigrationRunMeta',
			'{CFG:TableBaseName}MigrationRecord',
			'{CFG:TableBaseName}MigrationRecordHistory',
			'{CFG:TableBaseName}MigrationRunMode',
			'{CFG:TableBaseName}MigrationOperation',
			'{CFG:TableBaseName}MigrationRunResult',
			'{CFG:TableBaseName}MigrationStatus',
			'{CFG:TableBaseName}Environment'
		);

	BEGIN TRANSACTION;

		-- Check for [Version]-Table and therefore for repository-existence

		IF OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigratorMeta', 'U') IS NOT NULL
		BEGIN

			-- Check for repository completeness
			IF (@NumberOfTablesFound != 11)
			BEGIN
				COMMIT TRANSACTION;

				SELECT '-10,RayMigrator repository incomplete or corrupt. Repository contains [' + CAST(@NumberOfTablesFound AS VARCHAR(10)) + '] tables instead of [11].';
				RETURN;
			END;

			-- Try to get VersionId
			SELECT 
				@VersionId = Id
			FROM 
				[{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
			WHERE 
				RepositoryVersion = @RepositoryVersion
				AND RepositoryDatabaseType = @RepositoryDatabaseType
				AND CreatedByRayMigratorVersion = @RayMigratorVersion;

			SET @NumberOfRows = @@rowcount;

			IF (@NumberOfRows = 1)
			BEGIN
				SET @VersionIdString = CAST(@VersionId AS VARCHAR(10));

				COMMIT TRANSACTION;
				SELECT @VersionIdString + ',RayMigrator repository already exists. Using VersionId [' + @VersionIdString + '].';
				RETURN;
			END
			ELSE IF (@NumberOfRows = 0)
			BEGIN

				-- VersionId does not yet exist. Create new VersionId
				INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] 
				(
					RepositoryVersion,
					RepositoryDatabaseType,
					CreatedByRayMigratorVersion,
					CreatedAt
				) 
				VALUES 
				(
					@RepositoryVersion,
					@RepositoryDatabaseType,
					@RayMigratorVersion,
					SYSUTCDATETIME()
				);

				SET @VersionId = SCOPE_IDENTITY();

				COMMIT TRANSACTION;
				SET @VersionIdString = CAST(@VersionId AS VARCHAR(10));
				SELECT @VersionIdString + ',RayMigrator repository already exists. New VersionId [' + @VersionIdString + '] created.';
				RETURN;

			END
			ELSE
			BEGIN
				ROLLBACK TRANSACTION;

				DECLARE @ErrorString VARCHAR(MAX);
				SET @ErrorString = 'Multiple [MigratorMeta]-entries found for RepositoryVersion [' + COALESCE(@RepositoryVersion,'NULL') + '], RepositoryDatabaseType [' + COALESCE(@RepositoryDatabaseType,'NULL') + '], RayMigratorVersion [' + COALESCE(@RayMigratorVersion,'NULL') + '].';
				SELECT '-12,' + @ErrorString;
				RETURN;
			END;

		END;


		-- No [Version]-Table found. Check for repository-existence and completeness
		IF (@NumberOfTablesFound != 0)
		BEGIN
			ROLLBACK TRANSACTION;

			SELECT '-11,RayMigrator repository incomplete or corrupt. Repository contains [' + CAST(@NumberOfTablesFound AS VARCHAR(10)) + '] tables instead of the expected amount of [0].';
			RETURN;
		END;

		-- Create Schema if not exist
		IF SCHEMA_ID('{CFG:SchemaName}') IS NULL EXECUTE('CREATE SCHEMA [{CFG:SchemaName}];'); -- AUTHORIZATION ???

		-- Create repository
CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationOperation] (
	Id                   tinyint      NOT NULL,
	Name                 nvarchar(100)     NOT NULL,
	Description          nvarchar(1000)      NULL,
	CONSTRAINT pk_MigrationOperation PRIMARY KEY  ( Id )
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult] ( 
	Id                   tinyint      NOT NULL,
	Name                 nvarchar(100)     NOT NULL,
	Description          nvarchar(1000)      NULL,
	CONSTRAINT pk_MigrationRunResult PRIMARY KEY  ( Id ) 
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMode] ( 
	Id                   tinyint      NOT NULL,
	Name                 nvarchar(100)     NOT NULL,
	Description          nvarchar(1000)      NULL,
	CONSTRAINT pk_MigrationRunMode PRIMARY KEY  ( Id ) 
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationStatus] (
	Id                   tinyint      NOT NULL,
	Name                 nvarchar(100)     NOT NULL,
	Description          nvarchar(1000)      NULL,
	CONSTRAINT pk_MigrationStatus PRIMARY KEY  ( Id )
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] ( 
	Id                   int    IDENTITY(1,1)  NOT NULL,
	RepositoryVersion    nvarchar(100)     NOT NULL,
	RepositoryDatabaseType nvarchar(100)     NOT NULL,
	CreatedByRayMigratorVersion nvarchar(100)     NOT NULL,
	CreatedAt            datetime2(3)   NOT NULL,
	CONSTRAINT pk_RepositoryVersion PRIMARY KEY  ( Id ) 
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}Product] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	Name                 nvarchar(100)      NOT NULL,
	NameLower            nvarchar(100)      NOT NULL,
	CreatedAt            datetime2(3)   NOT NULL,
	CONSTRAINT pk_Product PRIMARY KEY  ( Id )
 );

CREATE UNIQUE INDEX [uix_{CFG:TableBaseName}Product_NameLower] ON [{CFG:SchemaName}].[{CFG:TableBaseName}Product] ( NameLower );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}Environment] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	Name                 nvarchar(100)      NOT NULL,
	NameLower            nvarchar(100)      NOT NULL,
	CreatedAt            datetime2(3)   NOT NULL,
	CONSTRAINT pk_Environment PRIMARY KEY  ( Id )
 );

CREATE UNIQUE INDEX [uix_{CFG:TableBaseName}Environment_NameLower] ON [{CFG:SchemaName}].[{CFG:TableBaseName}Environment] ( NameLower );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	MigratorMetaId    int      NOT NULL,
	ProductId            int      NOT NULL,
	EnvironmentId        int      NOT NULL,
	MigrationRunModeId   tinyint      NOT NULL,
	MigrationRunResultId    tinyint      NOT NULL,
	FromReleaseVersion   nvarchar(100)      NULL,
	ToReleaseVersion     nvarchar(100)      NULL,
	StartedAt            datetime2(3)   NOT NULL,
	FinishedAt           datetime2(3)   NULL,
	DurationInMs         bigint      NULL,
	CONSTRAINT pk_MigrationRun PRIMARY KEY  ( Id )
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMeta] ( 
	MigrationRunId       int      NOT NULL,
	MigrationRunSettingsJson nvarchar(max)      NULL,
	Description          nvarchar(max)      NULL,
	CONSTRAINT pk_MigrationRunMeta PRIMARY KEY  ( MigrationRunId ) 
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	ProductId            int      NOT NULL,
	EnvironmentId        int      NOT NULL,
	MigrationRunId       int      NOT NULL,
	MigrationRunModeId   tinyint      NOT NULL,
	MigrationOperationId tinyint      NOT NULL,
	MigrationStatusId    tinyint      NOT NULL,
	ReleaseVersion       nvarchar(100)      NOT NULL,
	TargetGroupAlias     nvarchar(100)      NOT NULL,
	TargetAlias          nvarchar(100)      NOT NULL,
	Filename             nvarchar(200)      NOT NULL,
	FileOrderId          int      NOT NULL,
	FileUpHash           varchar(100)      NOT NULL,
	FileUpConfigHash     varchar(100)      NULL,
	FileUpBlocksHash     varchar(100)      NOT NULL,
	FileUpBlocksMigrated int      NOT NULL,
	FileUpBlocksTotal    int      NOT NULL,
	FileUpConfigJson     nvarchar(max)     NULL,
	MigrateDownFileExists bit      NOT NULL,
	FileDownHash         varchar(100)      NULL,
	FileDownConfigHash   varchar(100)      NULL,
	FileDownBlocksHash   varchar(100)      NULL,
	FileDownBlocksMigrated int      NULL,
	FileDownBlocksTotal  int      NULL,
	FileDownConfigJson   nvarchar(max)     NULL,
	StartedAt            datetime2(3)   NULL,
	FinishedAt           datetime2(3)   NULL,
	DurationInMs         bigint      NULL,
	CONSTRAINT pk_MigrationRecord PRIMARY KEY  ( Id ) 
 );


CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	MigrationRecordId          int      NOT NULL,
	ProductId            int      NOT NULL,
	EnvironmentId        int      NOT NULL,
	MigrationRunId       int      NOT NULL,
	MigrationRunModeId   tinyint      NOT NULL,
	MigrationOperationId tinyint      NOT NULL,
	MigrationStatusId    tinyint      NOT NULL,
	ReleaseVersion       nvarchar(100)      NOT NULL,
	TargetGroupAlias     nvarchar(100)      NOT NULL,
	TargetAlias          nvarchar(100)      NOT NULL,
	Filename             nvarchar(200)      NOT NULL,
	FileOrderId          int      NOT NULL,
	FileUpHash           varchar(100)      NOT NULL,
	FileUpConfigHash     varchar(100)      NULL,
	FileUpBlocksHash     varchar(100)      NOT NULL,
	FileUpBlocksMigrated int      NOT NULL,
	FileUpBlocksTotal    int      NOT NULL,
	FileUpConfigJson     nvarchar(max)     NULL,
	MigrateDownFileExists bit      NOT NULL,
	FileDownHash         varchar(100)      NULL,
	FileDownConfigHash   varchar(100)      NULL,
	FileDownBlocksHash   varchar(100)      NULL,
	FileDownBlocksMigrated int      NULL,
	FileDownBlocksTotal  int      NULL,
	FileDownConfigJson   nvarchar(max)     NULL,
	StartedAt            datetime2(3)   NULL,
	FinishedAt           datetime2(3)   NULL,
	DurationInMs         bigint      NULL,
	HistorizedAt         datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
	CONSTRAINT pk_MigrationRecordHistory PRIMARY KEY  ( Id ) 
 );


CREATE  INDEX ix_MigrationRecordHistory ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ( MigrationRecordId );


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_Product FOREIGN KEY ( ProductId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Product]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_Environment FOREIGN KEY ( EnvironmentId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Environment]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_MigrationRun FOREIGN KEY ( MigrationRunId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_MigrationRunMode FOREIGN KEY ( MigrationRunModeId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMode]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_MigrationOperation FOREIGN KEY ( MigrationOperationId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationOperation]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ADD CONSTRAINT fk_MigrationRecord_MigrationStatus FOREIGN KEY ( MigrationStatusId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationStatus]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ADD CONSTRAINT fk_MigrationRecordHistory_MigrationRun FOREIGN KEY ( MigrationRunId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ADD CONSTRAINT fk_MigrationRecordHistory_Product FOREIGN KEY ( ProductId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Product]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ADD CONSTRAINT fk_MigrationRecordHistory_Environment FOREIGN KEY ( EnvironmentId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Environment]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_MigrationRun_Product FOREIGN KEY ( ProductId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Product]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_MigrationRun_Environment FOREIGN KEY ( EnvironmentId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}Environment]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_MigrationRun_MigrationRunResult FOREIGN KEY ( MigrationRunResultId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_MigrationRun_MigrationRunMode FOREIGN KEY ( MigrationRunModeId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMode]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_MigrationRun_MigratorMeta FOREIGN KEY ( MigratorMetaId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMeta] ADD CONSTRAINT fk_MigrationRunMeta_MigrationRun FOREIGN KEY ( MigrationRunId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'- Rollback (MigrateDown) = 5
- MigrateDown = 50
- MigrateUp = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationOperation';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Running = 10, Error = 90, Ok = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunResult';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Validate = 10,
Simulate = 20,
Migrate = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunMode';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'- Pending = 10
  (Record created, execution pending)
- Executing = 20
  (SQL blocks are being executed)
- Failed = 30
  (Execution failed, DB state unclear)
- NotMigrated = 50
  (Not deployed / rolled back)
- Migrated = 100
  (Successfully deployed)' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationStatus';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'From Repository''s create-script (sql-file)' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigratorMeta', @level2type=N'COLUMN',@level2name=N'RepositoryVersion';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'From appsettings.json Repository configuration' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigratorMeta', @level2type=N'COLUMN',@level2name=N'RepositoryDatabaseType';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'From RayMigrator-build' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigratorMeta', @level2type=N'COLUMN',@level2name=N'CreatedByRayMigratorVersion';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'= RayMigratorSettings for current product' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunMeta', @level2type=N'COLUMN',@level2name=N'MigrationRunSettingsJson';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Represents all migration-files found at time of last migration attempt' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'FK to Environment lookup table (DEV, QA, PROD)' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'EnvironmentId';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'1st part of the path of the FilenameWithRelativePath' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'ReleaseVersion';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'2nd part of the path of the FilenameWithRelativePath' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'TargetGroupAlias';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The alias of the target database' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'TargetAlias';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Filename without (relative) path since relative path consists of ReleaseVersion, TargetGroupAlias and Filename' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'Filename';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The Id of the file''s occurence ordered by the FilenameWithRelative path - which contains of ReleaseVersion, TargetGroupAlias and Filename' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileOrderId';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash value of the entire file' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the [RayMigrator] configuration section''s content that may be empty' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpConfigHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the file''s content - excluding the [RayMigrator] configuration section''s content. Therefore it only remains the content to be executed against the target-database.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpBlocksHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The amount of blocks within a migration-file, delimited by "GO" or "\" that have already been successfully migrated' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpBlocksMigrated';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The total number of blocks - separated by a delimiter like ''GO'' or Backslash - found within the migration-file''s content.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpBlocksTotal';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'IMigrationFileSettings' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileUpConfigJson';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'True if a corresponding migration.down.sql - file exists' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'MigrateDownFileExists';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash value of the entire file' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileDownHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the [RayMigrator] configuration section''s content that may be empty' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileDownConfigHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the file''s content - excluding the [RayMigrator] configuration section''s content. Therefore it only remains the content to be executed against the target-database.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileDownBlocksHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The number of blocks - separated by a delimiter like ''GO'' or Backslash - found within the migration-file''s content.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecord', @level2type=N'COLUMN',@level2name=N'FileDownBlocksTotal';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Represents all migration-files found at time of last migration attempt' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'FK to Environment lookup table (DEV, QA, PROD)' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'EnvironmentId';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'1st part of the path of the FilenameWithRelativePath' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'ReleaseVersion';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'2nd part of the path of the FilenameWithRelativePath' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'TargetGroupAlias';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The alias of the target database' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'TargetAlias';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Filename without (relative) path since relative path consists of ReleaseVersion, TargetGroupAlias and Filename' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'Filename';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The Id of the file''s occurence ordered by the FilenameWithRelative path - which contains of ReleaseVersion, TargetGroupAlias and Filename' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileOrderId';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash value of the entire file' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the [RayMigrator] configuration section''s content that may be empty' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpConfigHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the file''s content - excluding the [RayMigrator] configuration section''s content. Therefore it only remains the content to be executed against the target-database.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpBlocksHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The amount of blocks within a migration-file, delimited by "GO" or "\" that have already been successfully migrated' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpBlocksMigrated';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The total number of blocks - separated by a delimiter like ''GO'' or Backslash - found within the migration-file''s content.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpBlocksTotal';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'IMigrationFileSettings' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileUpConfigJson';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'True if a corresponding migration.down.sql - file exists' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'MigrateDownFileExists';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash value of the entire file' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileDownHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the [RayMigrator] configuration section''s content that may be empty' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileDownConfigHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Hash of the file''s content - excluding the [RayMigrator] configuration section''s content. Therefore it only remains the content to be executed against the target-database.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileDownBlocksHash';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'The number of blocks - separated by a delimiter like ''GO'' or Backslash - found within the migration-file''s content.' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRecordHistory', @level2type=N'COLUMN',@level2name=N'FileDownBlocksTotal';





		-- Data for Table "MigrationRunMode"
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunMode] ([Id], [Name], [Description])
		VALUES
			(10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
			(20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
			(100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.');

		-- Data for Table "MigrationOperation"
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationOperation] ([Id], [Name], [Description])
		VALUES
			(5, 'Rollback', 'Performing Rollback of current MigrationRun'),
			(50, 'MigrateDown', 'Performing Down-Migration'),
			(100, 'MigrateUp', 'Performing Up-Migration');

		-- Data for Table "MigrationRunResult"
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult] ([Id], [Name], [Description])
		VALUES
			(10, 'Running', 'Migration process is currently running'),
            (90, 'Error', 'Migration(s) stopped due to error(s)'),
			(100, 'Ok', 'Migration(s) successfully executed');

		-- Data for Table "MigrationStatus"
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationStatus] ([Id], [Name], [Description])
		VALUES
			(10, 'Pending', 'Record created, execution pending'),
			(20, 'Executing', 'SQL blocks are being executed'),
			(30, 'Failed', 'Execution failed, DB state unclear'),
			(50, 'NotMigrated', 'Not deployed / rolled back'),
			(100, 'Migrated', 'Successfully deployed');


		-- Create VersionId
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigratorMeta] 
		(
			RepositoryVersion,
			RepositoryDatabaseType,
			CreatedByRayMigratorVersion,
			CreatedAt
		)
		VALUES 
		(
			@RepositoryVersion,
			@RepositoryDatabaseType,
			@RayMigratorVersion,
			SYSUTCDATETIME()
		);

		SET @VersionId = SCOPE_IDENTITY();

		COMMIT TRANSACTION;

		SET @VersionIdString = CAST(@VersionId AS VARCHAR(10));
		SELECT @VersionIdString + ',RayMigrator repository-tables with master data and new VersionId [' + @VersionIdString + '] successfully created';

END TRY
BEGIN CATCH
    
    -- Rollback transaction on error
    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;
	
END CATCH;
