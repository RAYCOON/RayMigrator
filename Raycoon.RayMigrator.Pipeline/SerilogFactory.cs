using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Shared.Exceptions;
using Serilog;
using Serilog.Events;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Creates and configures the Serilog logger for the Direct mode execution pipeline.
/// Handles the database sink creation when DatabaseLogging is configured.
/// </summary>
public static class SerilogFactory
{
    /// <summary>
    /// Creates the Serilog logger from the RayMigrator configuration section.
    /// Also creates and wires the database sink if DatabaseLogging is configured.
    /// </summary>
    /// <param name="rayMigratorConfigSection">The RayMigrator configuration section containing Serilog settings.</param>
    /// <returns>A tuple of the typed Serilog logger and the optional database sink.</returns>
    /// <exception cref="ApplicationStartupException">Thrown when the logger cannot be created.</exception>
    public static (global::Serilog.ILogger SerilogLogger, RayMigratorDatabaseSink? DatabaseSink) Create(
        IConfigurationSection rayMigratorConfigSection)
    {
        RayMigratorDatabaseSink? databaseSink = null;

        try
        {
            // Database logging is on unless the section says Enabled: false (it goes into the repository without a
            // section). The sink is created before the options are bound, so it reads the section directly; the
            // minimum level is read early so the sink can be added to the initial logger.
            var dbLoggingSection = rayMigratorConfigSection.GetSection("DatabaseLogging");
            if (IsDatabaseLoggingEnabled(dbLoggingSection))
            {
                LogEventLevel dbSinkMinimumLevel = LogEventLevel.Information;
                var minLevelStr = dbLoggingSection["MinimumLevel"];
                if (!string.IsNullOrWhiteSpace(minLevelStr) && Enum.TryParse<LogLevel>(minLevelStr, true, out var parsedLevel))
                {
                    dbSinkMinimumLevel = MapToSerilogLevel(parsedLevel);
                }
                databaseSink = new RayMigratorDatabaseSink(dbSinkMinimumLevel);
            }

            var serilogConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(rayMigratorConfigSection)
                .Enrich.FromLogContext()
                .Enrich.With(new MigrationContextEnricher());

            if (databaseSink != null)
                serilogConfiguration = serilogConfiguration.WriteTo.Sink(databaseSink);

            Log.Logger = serilogConfiguration.CreateLogger();

            var serilogLogger = Log.ForContext(typeof(DirectModePipeline));

            return (serilogLogger, databaseSink);
        }
        catch (Exception ex)
        {
            throw new ApplicationStartupException("Could not create or use Serilog logger.", ex);
        }
    }

    /// <summary>
    /// Whether the written <c>DatabaseLogging</c> section leaves database logging on: it does not exist, or its
    /// <c>Enabled</c> value is not <c>false</c>.
    /// </summary>
    internal static bool IsDatabaseLoggingEnabled(IConfigurationSection dbLoggingSection)
    {
        if (!dbLoggingSection.Exists())
            return true;
        var enabled = dbLoggingSection["Enabled"];
        return !(bool.TryParse(enabled, out var value) && !value);
    }

    /// <summary>
    /// Maps Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel.
    /// </summary>
    internal static LogEventLevel MapToSerilogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}
