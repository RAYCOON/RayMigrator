/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Insert"
DatabaseType   = "Sqlite"
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
- CreatedAt timestamp is set to datetime('now') automatically
"""

[ConfigPlaceholders]
SchemaName    = "Not used for SQLite (no schema support)"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'Log_')"

[Parameters]
LogLevelId       = "INTEGER | REQUIRED | Log level: 0=Trace, 1=Debug, 2=Info, 3=Warning, 4=Error, 5=Critical"
MigrationEventId = "INTEGER | OPTIONAL | Event type ID from MigrationEvent table"
RunModeId        = "INTEGER | OPTIONAL | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
ProductId        = "INTEGER | OPTIONAL | Product ID if available"
EnvironmentId    = "INTEGER | OPTIONAL | Environment ID if available"
MigrationRunId   = "INTEGER | OPTIONAL | MigrationRun ID if available"
MigrationId      = "INTEGER | OPTIONAL | Migration record ID if available"
ReleaseVersion   = "TEXT | OPTIONAL | Release version if applicable"
TargetGroupAlias = "TEXT | OPTIONAL | Target group alias if applicable"
TargetAlias      = "TEXT | OPTIONAL | Target alias if applicable"
Filename         = "TEXT | OPTIONAL | Migration filename if applicable"
FileOrderId      = "INTEGER | OPTIONAL | File order ID if applicable"
FileBlockId      = "INTEGER | OPTIONAL | Block ID within file if applicable"
Message          = "TEXT | OPTIONAL | Log message text"

[ReturnValues]
# This template performs INSERT only - no return value

[ModificationNotes]
Note1 = "This template performs INSERT only - no SELECT/return value"
Note2 = "Use datetime('now') for CreatedAt timestamp"
Note3 = "All parameters except LogLevelId are optional (nullable)"
================================================================================
*/

INSERT INTO "{CFG:TableBaseName}MigrationLog"
(
    "LogLevelId",
    "MigrationEventId",
    "RunModeId",
    "ProductId",
    "EnvironmentId",
    "MigrationRunId",
    "MigrationId",
    "ReleaseVersion",
    "TargetGroupAlias",
    "TargetAlias",
    "Filename",
    "FileOrderId",
    "FileBlockId",
    "Message",
    "CreatedAt"
)
VALUES
(
    @LogLevelId,
    @MigrationEventId,
    @RunModeId,
    @ProductId,
    @EnvironmentId,
    @MigrationRunId,
    @MigrationId,
    @ReleaseVersion,
    @TargetGroupAlias,
    @TargetAlias,
    @Filename,
    @FileOrderId,
    @FileBlockId,
    @Message,
    datetime('now')
);
