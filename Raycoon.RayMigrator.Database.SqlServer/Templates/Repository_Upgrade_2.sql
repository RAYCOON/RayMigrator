/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_2"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-14.1"

[Description]
Function = """
Schema version 2 (ADR-024): adds the run details to MigrationRun (operation, source, initiator,
host, error code and message, last heartbeat) and the MigrationRunSource lookup table
(Cli = 10, Ci = 20, Studio = 30), and writes row 2 of the RepositorySchemaVersion table.
"""

Behaviour = """
- Return value 2: schema version 2 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: every object is created only when missing; one transaction, serialised with sp_getapplock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.17.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_2  = "2,RayMigrator repository schema version [2] applied: MigrationRun run details and MigrationRunSource"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "New MigrationRun columns are nullable: rows of schema version 1 keep NULL"
Note4 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
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

	IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] WHERE [Version] = 2)
	BEGIN

		IF OBJECT_ID('{CFG:SchemaName}.{CFG:TableBaseName}MigrationRunSource', 'U') IS NULL
		BEGIN
			CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunSource] (
				Id                   tinyint      NOT NULL,
				Name                 nvarchar(100)     NOT NULL,
				Description          nvarchar(1000)      NULL,
				CONSTRAINT pk_{CFG:TableBaseName}MigrationRunSource PRIMARY KEY  ( Id )
			);

			execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Cli = 10, Ci = 20, Studio = 30' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunSource';

			INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunSource] ([Id], [Name], [Description])
			VALUES
				(10, 'Cli', 'Started by the RayMigrator command line on a workstation or server'),
				(20, 'Ci', 'Started by the RayMigrator command line in a CI/CD pipeline'),
				(30, 'Studio', 'Started by RayMigrator Studio');
		END;

		IF COL_LENGTH('{CFG:SchemaName}.{CFG:TableBaseName}MigrationRun', 'MigrationOperationId') IS NULL
		BEGIN
			ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD
				MigrationOperationId tinyint            NULL,
				MigrationRunSourceId tinyint            NULL,
				InitiatedBy          nvarchar(200)      NULL,
				InitiatedBySubject   nvarchar(200)      NULL,
				Host                 nvarchar(200)      NULL,
				ErrorCode            nvarchar(100)      NULL,
				ErrorMessage         nvarchar(max)      NULL,
				LastHeartbeatAt      datetime2(3)       NULL;
		END;

		IF OBJECT_ID('{CFG:SchemaName}.fk_{CFG:TableBaseName}MigrationRun_MigrationOperation', 'F') IS NULL
			ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_{CFG:TableBaseName}MigrationRun_MigrationOperation FOREIGN KEY ( MigrationOperationId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationOperation]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;

		IF OBJECT_ID('{CFG:SchemaName}.fk_{CFG:TableBaseName}MigrationRun_MigrationRunSource', 'F') IS NULL
			ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ADD CONSTRAINT fk_{CFG:TableBaseName}MigrationRun_MigrationRunSource FOREIGN KEY ( MigrationRunSourceId ) REFERENCES [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunSource]( Id ) ON DELETE NO ACTION ON UPDATE NO ACTION;

		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] ([Version], [AppliedAt], [RayMigratorVersion])
		VALUES (2, SYSUTCDATETIME(), @RayMigratorVersion);

	END;

	COMMIT TRANSACTION;

	SELECT '2,RayMigrator repository schema version [2] applied: MigrationRun run details and MigrationRunSource';

END TRY
BEGIN CATCH

    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
