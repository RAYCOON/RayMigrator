/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Insert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Inserts a new log entry into the MigrationLog table.
Used for database-level logging of migration events and progress.
"""

Behaviour = """
- No return value (INSERT only, no SELECT)
- Called by RayMigrator framework, not by Serilog
- CreatedAt timestamp is set to SYSUTCDATETIME() automatically
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
# NOTE: For DatabaseLogging_* templates, values come from the 'Logging' section of appsettings
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'Log_')"

[Parameters]
# SQL parameters bound at runtime
LogLevelId       = "TINYINT | REQUIRED | Log level: 0=Trace, 1=Debug, 2=Info, 3=Warning, 4=Error, 5=Critical"
MigrationEventId = "INT | OPTIONAL | Event type ID from MigrationEvent table (see below)"
RunModeId        = "TINYINT | OPTIONAL | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
ProductId        = "INT | OPTIONAL | Product ID if available"
EnvironmentId    = "INT | OPTIONAL | Environment ID if available"
MigrationRunId   = "INT | OPTIONAL | MigrationRun ID if available"
MigrationRecordId      = "INT | OPTIONAL | Migration record ID if available"
ReleaseVersion   = "NVARCHAR(100) | OPTIONAL | Release version if applicable"
TargetGroupAlias = "NVARCHAR(100) | OPTIONAL | Target group alias if applicable"
TargetAlias      = "NVARCHAR(100) | OPTIONAL | Target alias if applicable"
Filename         = "NVARCHAR(300) | OPTIONAL | Migration filename if applicable"
FileOrderId      = "INT | OPTIONAL | File order ID if applicable"
FileBlockId      = "INT | OPTIONAL | Block ID within file if applicable"
Message          = "NVARCHAR(MAX) | OPTIONAL | Log message text"

[ReturnValues]
# This template performs INSERT only - no return value

[MigrationEventIds]
# Reference for MigrationEventId values (defined in DatabaseLogging_CheckCreate)
Event_0    = "UnspecifiedEvent"
Event_10   = "CommandLineParsing"
Event_20   = "EnvironmentVariableReplacement"
Event_31   = "CreateDatabaseLogger"
Event_32   = "CreateCompositeLogger"
Event_40   = "ValidateRayMigratorOptions"
Event_50   = "CreateApplicationHost"
Event_60   = "InitializeDalSpecificProperties"
Event_70   = "ValidateConnectionStrings"
Event_80   = "RayMigratorServiceStart"
Event_100  = "CreateAndStartRayMigratorService"
Event_1000 = "RayMigratorServiceShutdown"

[ModificationNotes]
Note1 = "This template performs INSERT only - no SELECT/return value"
Note2 = "Use SYSUTCDATETIME() for CreatedAt timestamp"
Note3 = "All parameters except LogLevelId are optional (nullable)"
Note4 = "Called directly by RayMigrator framework - not via Serilog"
================================================================================
*/


INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationLog]
(
	[LogLevelId],
	[MigrationEventId],
	[RunModeId],
	[ProductId],
	[EnvironmentId],
	[MigrationRunId],
	[MigrationRecordId],
	[ReleaseVersion],
	[TargetGroupAlias],
	[TargetAlias],
	[Filename],
	[FileOrderId],
	[FileBlockId],
	[Message],
	[CreatedAt]
)
VALUES
(
	@LogLevelId,
	@MigrationEventId,
	@RunModeId,
	@ProductId,
	@EnvironmentId,
	@MigrationRunId,
	@MigrationRecordId,
	@ReleaseVersion,
	@TargetGroupAlias,
	@TargetAlias,
	@Filename,
	@FileOrderId,
	@FileBlockId,
	@Message,
	SYSUTCDATETIME()
);
