/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_3"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Schema version 3 (ADR-024): adds the run result Cancelled = 70 to the MigrationRunResult lookup
table, the indexes the readers of the repository filter by (runs per product, environment and
start, history rows per run and per time, records per product and environment), and writes
row 3 of the RepositorySchemaVersion table.
"""

Behaviour = """
- Return value 3: schema version 3 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: every row and index is created only when missing; one transaction, serialised with sp_getapplock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
RayMigratorVersion = "VARCHAR(100) | REQUIRED | The RayMigrator version applying the step (e.g., '0.20.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_3  = "3,RayMigrator repository schema version [3] applied: MigrationRunResult Cancelled and the reader indexes"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "The lookup row and the indexes are data and access paths only: rows of schema version 2 stay as they are"
Note4 = "The description of the MigrationRunResult table names the new value; it is updated when step 1 wrote it and added otherwise"
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

	IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] WHERE [Version] = 3)
	BEGIN

		IF NOT EXISTS (SELECT 1 FROM [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult] WHERE [Id] = 70)
			INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRunResult] ([Id], [Name], [Description])
			VALUES (70, 'Cancelled', 'Migration(s) stopped on request before completion');

		IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'SCHEMA', N'{CFG:SchemaName}', N'TABLE', N'{CFG:TableBaseName}MigrationRunResult', NULL, NULL))
			execute sys.sp_updateextendedproperty  @name=N'MS_Description', @value=N'Running = 10, PartialSuccess = 50, Cancelled = 70, Recovered = 80, Error = 90, Ok = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunResult';
		ELSE
			execute sys.sp_addextendedproperty  @name=N'MS_Description', @value=N'Running = 10, PartialSuccess = 50, Cancelled = 70, Recovered = 80, Error = 90, Ok = 100' , @level0type=N'SCHEMA',@level0name=N'{CFG:SchemaName}', @level1type=N'TABLE',@level1name=N'{CFG:TableBaseName}MigrationRunResult';

		IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationRun', N'U'))
			CREATE INDEX ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun] ( ProductId, EnvironmentId, StartedAt );

		IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationRecordHistory_MigrationRunId' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationRecordHistory', N'U'))
			CREATE INDEX ix_{CFG:TableBaseName}MigrationRecordHistory_MigrationRunId ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ( MigrationRunId );

		IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationRecordHistory_HistorizedAt' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationRecordHistory', N'U'))
			CREATE INDEX ix_{CFG:TableBaseName}MigrationRecordHistory_HistorizedAt ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] ( HistorizedAt );

		IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_{CFG:TableBaseName}MigrationRecord_Product_Environment' AND object_id = OBJECT_ID(N'{CFG:SchemaName}.{CFG:TableBaseName}MigrationRecord', N'U'))
			CREATE INDEX ix_{CFG:TableBaseName}MigrationRecord_Product_Environment ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] ( ProductId, EnvironmentId );

		INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}RepositorySchemaVersion] ([Version], [AppliedAt], [RayMigratorVersion])
		VALUES (3, SYSUTCDATETIME(), @RayMigratorVersion);

	END;

	COMMIT TRANSACTION;

	SELECT '3,RayMigrator repository schema version [3] applied: MigrationRunResult Cancelled and the reader indexes';

END TRY
BEGIN CATCH

    IF (@@TRANCOUNT > 0)
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    ;THROW;

END CATCH;
