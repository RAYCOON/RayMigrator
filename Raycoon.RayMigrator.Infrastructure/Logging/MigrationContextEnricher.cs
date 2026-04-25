
using Raycoon.RayMigrator.Core;
using Serilog.Core;
using Serilog.Events;

namespace Raycoon.RayMigrator.Infrastructure.Logging;

/// <summary>
/// Serilog enricher that reads MigrationContext from MigrationLoggingContext.Current
/// and adds migration-specific properties to every log event.
/// </summary>
public class MigrationContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var ctx = MigrationLoggingContext.Current;
        if (ctx == null) return;

        // Snapshot all mutable state into local variables to prevent tearing
        // when the migration thread mutates MigrationState concurrently.
        // Each individual read is atomic (reference/int assignment on .NET).
        var state = ctx.MigrationState;
        var environment = ctx.RayMigratorConsoleOptions.Environment ?? string.Empty;
        var environmentId = state.EnvironmentId;
        var runModeId = (byte)ctx.RayMigratorConsoleOptions.RunMode;
        var migrationRunId = state.MigrationRunId;
        var targetGroupAlias = state.TargetGroupAlias ?? string.Empty;
        var targetAlias = state.TargetAlias ?? string.Empty;
        var filenameWithRelativePath = state.FilenameWithRelativePath ?? string.Empty;
        var fileOrderId = state.FileOrderId;
        var fileBlockId = state.FileBlockId;
        var productId = state.ProductId;
        var migrationRecordId = state.MigrationRecordId;
        var releaseVersion = state.ReleaseVersionFromFileNameWithPath ?? string.Empty;

        // Properties for Serilog console/file output — text environment preserved for readability
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Environment", environment));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MigrationRunId", migrationRunId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TargetGroupAlias", targetGroupAlias));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TargetAlias", targetAlias));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MigrationFilename", filenameWithRelativePath));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MigrationFileId", fileOrderId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MigrationBlockId", fileBlockId));

        // Additional properties for database sink
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RunModeId", runModeId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProductId", productId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("EnvironmentId", environmentId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MigrationRecordId", migrationRecordId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ReleaseVersion", releaseVersion));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("FileName", filenameWithRelativePath));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("FileOrderId", fileOrderId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("FileBlockId", fileBlockId));
    }
}
