/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "DatabaseLogging_Insert"
DatabaseType   = "MariaDb"
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
- CreatedAt timestamp is set to CURRENT_TIMESTAMP automatically
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Logging configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Logging configuration (e.g., '' or 'Log_')"

[Parameters]
LogLevelId       = "TINYINT UNSIGNED | REQUIRED | Log level: 0=Trace, 1=Debug, 2=Info, 3=Warning, 4=Error, 5=Critical"
MigrationEventId = "INT | OPTIONAL | Event type ID from MigrationEvent table"
RunModeId        = "TINYINT UNSIGNED | OPTIONAL | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
ProductId        = "INT | OPTIONAL | Product ID if available"
EnvironmentId    = "INT | OPTIONAL | Environment ID if available"
MigrationRunId   = "INT | OPTIONAL | MigrationRun ID if available"
MigrationId      = "INT | OPTIONAL | Migration record ID if available"
ReleaseVersion   = "VARCHAR(100) | OPTIONAL | Release version if applicable"
TargetGroupAlias = "VARCHAR(100) | OPTIONAL | Target group alias if applicable"
TargetAlias      = "VARCHAR(100) | OPTIONAL | Target alias if applicable"
Filename         = "VARCHAR(300) | OPTIONAL | Migration filename if applicable"
FileOrderId      = "INT | OPTIONAL | File order ID if applicable"
FileBlockId      = "INT | OPTIONAL | Block ID within file if applicable"
Message          = "TEXT | OPTIONAL | Log message text"

[ReturnValues]
# This template performs INSERT only - no return value

[ModificationNotes]
Note1 = "This template performs INSERT only - no SELECT/return value"
Note2 = "Use CURRENT_TIMESTAMP for CreatedAt (session time_zone='+00:00' ensures UTC)"
Note3 = "All parameters except LogLevelId are optional (nullable)"
================================================================================
*/

INSERT INTO {CFG:TableBaseName}migration_log
(
    log_level_id,
    migration_event_id,
    run_mode_id,
    product_id,
    environment_id,
    migration_run_id,
    migration_id,
    release_version,
    target_group_alias,
    target_alias,
    filename,
    file_order_id,
    file_block_id,
    message,
    created_at
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
    CURRENT_TIMESTAMP
);
