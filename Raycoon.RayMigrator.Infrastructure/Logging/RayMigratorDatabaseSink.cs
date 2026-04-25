
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

        // Only write to database in Migrate mode.
        // Early pipeline logs without RunModeId (null) pass through — they are
        // emitted before the run mode is known and carry infrastructure context only.
        var runModeId = GetByteProperty(logEvent, "RunModeId");
        if (runModeId.HasValue && runModeId.Value != (byte)MigrationRunMode.Migrate)
            return;

        var writer = _writer;
        if (writer == null || !writer.IsInitialized)
            return;

        var logLevel = MapToMicrosoftLogLevel(logEvent.Level);
        var eventId = GetIntProperty(logEvent, "EventId_Id");
        var message = logEvent.RenderMessage();

        if (logEvent.Exception != null)
            message += $"\nException:\n{logEvent.Exception}\n";

        // runModeId already retrieved above for the Migrate-mode gate
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

    private static int GetIntProperty(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue sv)
        {
            if (sv.Value is int i) return i;
            if (sv.Value != null && int.TryParse(sv.Value.ToString(), out var parsed)) return parsed;
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
