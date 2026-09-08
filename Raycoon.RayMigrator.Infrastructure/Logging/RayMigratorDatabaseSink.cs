using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Serilog.Core;
using Serilog.Events;

namespace Raycoon.RayMigrator.Infrastructure.Logging;

/// <summary>
/// Serilog sink that bridges the Serilog pipeline to the DatabaseLogWriter.
/// Extracts enriched properties from LogEvents (set by MigrationContextEnricher)
/// and delegates to DatabaseLogWriter for database persistence via DAL + SQL templates.
/// </summary>
public class RayMigratorDatabaseSink : ILogEventSink, IDisposable
{
    private volatile DatabaseLogWriter? _writer;
    private readonly LogEventLevel _minimumLevel;

    /// <summary>
    /// Creates a deferred sink without a writer. The writer must be set later via <see cref="SetWriter"/>.
    /// </summary>
    public RayMigratorDatabaseSink(LogEventLevel minimumLevel)
    {
        _minimumLevel = minimumLevel;
    }

    /// <summary>
    /// Creates a sink with an immediately available writer.
    /// </summary>
    public RayMigratorDatabaseSink(DatabaseLogWriter writer, LogEventLevel minimumLevel)
    {
        _writer = writer;
        _minimumLevel = minimumLevel;
    }

    /// <summary>
    /// Sets the writer after deferred construction. Thread-safe via volatile field.
    /// </summary>
    public void SetWriter(DatabaseLogWriter writer) => _writer = writer;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < _minimumLevel)
            return;

        // Only write to database when the command's profile says so (DbLogEnabled, emitted by
        // MigrationContextEnricher from CommandProfile.WritesDatabaseLog): migrate-up/-down in Migrate mode,
        // update-hash, baseline and fix - not info, validate-hash, simulate/validate runs or fix --dry-run (#6).
        // Early pipeline logs without the property (null) pass through - they are
        // emitted before the context is known and carry infrastructure context only.
        var dbLogEnabled = GetBoolProperty(logEvent, "DbLogEnabled");
        if (dbLogEnabled.HasValue && !dbLogEnabled.Value)
            return;

        var runModeId = GetByteProperty(logEvent, "RunModeId");

        var writer = _writer;
        if (writer == null || !writer.IsInitialized)
            return;

        var logLevel = MapToMicrosoftLogLevel(logEvent.Level);
        var eventId = GetEventId(logEvent);
        var message = logEvent.RenderMessage();

        if (logEvent.Exception != null)
            message += $"\nException:\n{logEvent.Exception}\n";

        var productId = GetNullableIntProperty(logEvent, "ProductId");
        var environmentId = GetNullableIntProperty(logEvent, "EnvironmentId");
        var migrationRunId = GetNullableIntProperty(logEvent, "MigrationRunId");
        var migrationRecordId = GetNullableIntProperty(logEvent, "MigrationRecordId");
        var releaseVersion = GetStringProperty(logEvent, "ReleaseVersion");
        var targetGroupAlias = GetStringProperty(logEvent, "TargetGroupAlias");
        var targetAlias = GetStringProperty(logEvent, "TargetAlias");
        var fileName = GetStringProperty(logEvent, "FileName");
        var fileOrderId = GetNullableIntProperty(logEvent, "FileOrderId");
        var fileBlockId = GetNullableIntProperty(logEvent, "FileBlockId");

        writer.EnqueueLogEntry(
            logLevel, eventId, message,
            runModeId, productId, environmentId, migrationRunId, migrationRecordId,
            releaseVersion, targetGroupAlias, targetAlias,
            fileName, fileOrderId, fileBlockId);
    }

    public void Dispose()
    {
        // DatabaseLogWriter lifecycle is managed externally
    }

    private static LogLevel MapToMicrosoftLogLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.None
    };

    /// <summary>
    /// Serilog.Extensions.Logging attaches the Microsoft <c>EventId</c> of a logger call as one property named
    /// <c>EventId</c> whose value is a <see cref="StructureValue"/> with <c>Id</c> and <c>Name</c>. Events logged
    /// without an EventId carry no such property and are stored as 0 (<c>MigrationEvent.UnspecifiedEvent</c>) (#12).
    /// </summary>
    private static int GetEventId(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("EventId", out var value) && value is StructureValue structure)
        {
            var id = structure.Properties.FirstOrDefault(p => p.Name == "Id")?.Value as ScalarValue;
            if (id?.Value is int i) return i;
            if (id?.Value != null && int.TryParse(id.Value.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }

    private static int? GetNullableIntProperty(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue sv)
        {
            if (sv.Value is int i) return i;
            if (sv.Value != null && int.TryParse(sv.Value.ToString(), out var parsed)) return parsed;
        }
        return null;
    }

    private static bool? GetBoolProperty(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue sv)
        {
            if (sv.Value is bool b) return b;
            if (sv.Value != null && bool.TryParse(sv.Value.ToString(), out var parsed)) return parsed;
        }
        return null;
    }

    private static byte? GetByteProperty(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue sv)
        {
            if (sv.Value is byte b) return b;
            if (sv.Value != null && byte.TryParse(sv.Value.ToString(), out var parsed)) return parsed;
        }
        return null;
    }

    private static string? GetStringProperty(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue sv)
        {
            return sv.Value?.ToString();
        }
        return null;
    }
}
