using Microsoft.Extensions.Logging;

namespace Raycoon.RayMigrator.Core.Logging;

/// <summary>
/// Catalogue of the <see cref="EventId"/>s RayMigrator logs with. The Id is persisted as
/// <c>MigrationLog.MigrationEventId</c> by the DatabaseLogging sink, the name is the value of the
/// <c>MigrationEvent</c> lookup table that every <c>DatabaseLogging_CheckCreate</c> template seeds (#12).
/// Field name and event name are identical so that code, log output and lookup table agree.
/// </summary>
public static class MigrationEvent
{
    public static readonly EventId UnspecifiedEvent = new EventId(0, nameof(UnspecifiedEvent));

    // Application Startup
    public static readonly EventId CommandLineParsing = new EventId(10, nameof(CommandLineParsing));
    public static readonly EventId EnvironmentVariableReplacement = new EventId(20, nameof(EnvironmentVariableReplacement));
    public static readonly EventId CreateDatabaseLogger = new EventId(31, nameof(CreateDatabaseLogger));
    public static readonly EventId ValidateRayMigratorOptions = new EventId(40, nameof(ValidateRayMigratorOptions));
    public static readonly EventId CreateApplicationHost = new EventId(50, nameof(CreateApplicationHost));
    public static readonly EventId InitializeDalSpecificProperties = new EventId(60, nameof(InitializeDalSpecificProperties));
    public static readonly EventId ValidateConnectionStrings = new EventId(70, nameof(ValidateConnectionStrings));
    public static readonly EventId RayMigratorServiceStart = new EventId(80, nameof(RayMigratorServiceStart));

    // Template Execution - Repository Operations
    public static readonly EventId TemplateExecutionRepositoryCheckCreate = new EventId(100, nameof(TemplateExecutionRepositoryCheckCreate));
    public static readonly EventId TemplateExecutionRepositoryMigrationRunInsert = new EventId(110, nameof(TemplateExecutionRepositoryMigrationRunInsert));
    public static readonly EventId TemplateExecutionRepositoryMigrationRunUpdate = new EventId(111, nameof(TemplateExecutionRepositoryMigrationRunUpdate));
    public static readonly EventId TemplateExecutionRepositoryMigrationRunSelectOrphaned = new EventId(112, nameof(TemplateExecutionRepositoryMigrationRunSelectOrphaned));
    public static readonly EventId TemplateExecutionRepositoryMigrationRunFixOrphaned = new EventId(113, nameof(TemplateExecutionRepositoryMigrationRunFixOrphaned));
    public static readonly EventId TemplateExecutionRepositoryMigrationFixOrphaned = new EventId(114, nameof(TemplateExecutionRepositoryMigrationFixOrphaned));
    public static readonly EventId TemplateExecutionRepositoryProductCheckInsert = new EventId(120, nameof(TemplateExecutionRepositoryProductCheckInsert));
    public static readonly EventId TemplateExecutionRepositoryEnvironmentCheckInsert = new EventId(121, nameof(TemplateExecutionRepositoryEnvironmentCheckInsert));
    public static readonly EventId TemplateExecutionRepositoryProductSelect = new EventId(122, nameof(TemplateExecutionRepositoryProductSelect));
    public static readonly EventId TemplateExecutionRepositoryEnvironmentSelect = new EventId(123, nameof(TemplateExecutionRepositoryEnvironmentSelect));

    // Template Execution - Migration Operations
    public static readonly EventId TemplateExecutionRepositoryMigrationInsert = new EventId(130, nameof(TemplateExecutionRepositoryMigrationInsert));
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdate = new EventId(131, nameof(TemplateExecutionRepositoryMigrationUpdate));
    public static readonly EventId TemplateExecutionRepositoryMigrationGetInterrupted = new EventId(132, nameof(TemplateExecutionRepositoryMigrationGetInterrupted));
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdateRollback = new EventId(133, nameof(TemplateExecutionRepositoryMigrationUpdateRollback));
    public static readonly EventId TemplateExecutionRepositoryMigrationSelect = new EventId(134, nameof(TemplateExecutionRepositoryMigrationSelect));
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdateHash = new EventId(135, nameof(TemplateExecutionRepositoryMigrationUpdateHash));
    public static readonly EventId TemplateExecutionRepositoryMigrationRunSelect = new EventId(136, nameof(TemplateExecutionRepositoryMigrationRunSelect));
    public static readonly EventId TemplateExecutionRepositoryMigrationRecordHistorySelect = new EventId(137, nameof(TemplateExecutionRepositoryMigrationRecordHistorySelect));

    // Application Shutdown
    public static readonly EventId RayMigratorServiceShutdown = new EventId(1000, nameof(RayMigratorServiceShutdown));
}
