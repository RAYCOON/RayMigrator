/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_1"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-13.1"

[Description]
Function = """
Schema version 1 (ADR-024): creates the 11 repository tables with their indexes, foreign
keys, extended properties and lookup master data, and writes row 1 of the
RepositorySchemaVersion table. Runs on a repository that has no schema version yet.
"""

Behaviour = """
- Return value 1: schema version 1 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: the tables are created only when MigratorMeta does not exist, the version
  row only when it is missing; one transaction, serialised with sp_getapplock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_1  = "1,RayMigrator repository schema version [1] applied: 11 tables with master data"
Error_-1   = "-1,Could not acquire the repository schema lock"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "Use SYSUTCDATETIME() for all audit timestamps"
Note4 = "Tables created: MigratorMeta, Product, Environment, MigrationRun, MigrationRunMeta, MigrationRecord, MigrationRecordHistory, MigrationRunMode, MigrationOperation, MigrationRunResult, MigrationStatus"
Note5 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
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

	IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] WHERE [Version] = 1)
	BEGIN

		IF OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigratorMeta', 'U') IS NULL
		BEGIN

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
	RayMigratorVersion   nvarchar(100)     NOT NULL,
	RepositoryDatabaseType nvarchar(100)     NOT NULL,
	CreatedAt            datetime2(3)   NOT NULL,
	CONSTRAINT pk_MigratorMeta PRIMARY KEY  ( Id ) 
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
- MigrateUp = 100
- Baseline = 110' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationOperation';;


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Running = 10, PartialSuccess = 50, Recovered = 80, Error = 90, Ok = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunResult';;


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


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'RayMigrator version that used the repository; the first row is the version that created it and identifies the schema' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigratorMeta', @level2type=N'COLUMN',@level2name=N'RayMigratorVersion';


execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'From appsettings.json Repository configuration' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigratorMeta', @level2type=N'COLUMN',@level2name=N'RepositoryDatabaseType';


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
			(100, 'MigrateUp', 'Performing Up-Migration'),
			(110, 'Baseline', 'Marking migration files as migrated without executing them (baseline command)');

		-- Data for Table "MigrationRunResult"
		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult] ([Id], [Name], [Description])
		VALUES
			(10, 'Running', 'Migration process is currently running'),
			(50, 'PartialSuccess', 'Migration(s) finished but at least one file was skipped or left Failed'),
			(80, 'Recovered', 'Migration(s) failed and the configured error recovery rolled back cleanly'),
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

		END;

		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] ([Version], [AppliedAt], [RayMigratorVersion])
		VALUES (1, SYSUTCDATETIME(), @RayMigratorVersion);

	END;

	COMMIT TRANSACTION;

	SELECT '1,RayMigrator repository schema version [1] applied: 11 tables with master data';

END TRY
BEGIN CATCH

    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
