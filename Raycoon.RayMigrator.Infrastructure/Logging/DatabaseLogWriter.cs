
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Infrastructure.Logging;

/// <summary>
/// Writes log entries to the database via DAL and SQL templates.
/// This is a pure service class (not an ILogger) that handles database log persistence.
/// Used by RayMigratorDatabaseSink to bridge between Serilog pipeline and database logging.
/// </summary>
public class DatabaseLogWriter
{
    private readonly RayMigratorOptions _options;
    private readonly IDal? _dal;
    private readonly DalSettings? _dalSettings;
    private readonly LogLevel _minimumLogLevel;
    private readonly DatabaseLoggerQueue _databaseLoggerQueue;

    // Initialized from method-call InitDatabaseLogger()
    private Template? _templateLoggingInsert;
    private volatile bool _isDatabaseLoggingInitialized;

    /// <summary>
    /// Creates a DatabaseLogWriter with a functional DAL for database logging.
    /// </summary>
    public DatabaseLogWriter(RayMigratorOptions options, IDal? dal = null)
    {
        _dal = dal;
        _options = options;
        _databaseLoggerQueue = new DatabaseLoggerQueue();

        if (dal != null)
        {
            _dalSettings = new DalSettings
            {
                UseTransaction = false,
                DbCommandTimeoutInSeconds = (int)_options.DatabaseLogging!.DbCommandTimeoutInSeconds!
            };

            _minimumLogLevel = Enum.Parse<LogLevel>(options.DatabaseLogging!.MinimumLevel!, true);
        }
    }

    /// <summary>
    /// Returns the configured minimum log level for database logging.
    /// </summary>
    public LogLevel MinimumLogLevel => _minimumLogLevel;

    /// <summary>
    /// Returns true if database logging has been initialized and is ready for use.
    /// </summary>
    public bool IsInitialized => _isDatabaseLoggingInitialized;

    /// <summary>
    /// Returns true if there are pending log entries in the queue.
    /// </summary>
    public bool HasLogEntries => _databaseLoggerQueue.HasLogEntries;

    /// <summary>
    /// Flushes all pending log entries to the database.
    /// After this call, no more log entries will be accepted.
    /// </summary>
    public void Flush(TimeSpan? timeout = null)
    {
        _databaseLoggerQueue.Flush(timeout);
    }

    /// <summary>
    /// Enqueues a log entry for asynchronous writing to the database.
    /// EnvironmentId is nullable: early infrastructure logs emitted before
    /// Repository_Environment_CheckInsert resolves the ID receive null.
    /// </summary>
    public void EnqueueLogEntry(
        LogLevel logLevel,
        int eventId,
        string message,
        byte? runModeId,
        int? productId,
        int? environmentId,
        int? migrationRunId,
        int? migrationRecordId,
        string? releaseVersion,
        string? targetGroupAlias,
        string? targetAlias,
        string? fileName,
        int? fileOrderId,
        int? fileBlockId)
    {
        if (!_isDatabaseLoggingInitialized)
            return;

        if (logLevel < _minimumLogLevel)
            return;

        _databaseLoggerQueue.EnqueueLog(() => WriteToDatabase(
            logLevel, eventId, message,
            runModeId, productId, environmentId, migrationRunId, migrationRecordId,
            releaseVersion, targetGroupAlias, targetAlias,
            fileName, fileOrderId, fileBlockId));
    }

    /// <summary>
    /// Writes a log entry to the database via DAL.
    /// </summary>
    private void WriteToDatabase(
        LogLevel logLevel,
        int eventId,
        string message,
        byte? runModeId,
        int? productId,
        int? environmentId,
        int? migrationRunId,
        int? migrationRecordId,
        string? releaseVersion,
        string? targetGroupAlias,
        string? targetAlias,
        string? fileName,
        int? fileOrderId,
        int? fileBlockId)
    {
        DalParameterList dalParameterList = new DalParameterList();

        dalParameterList.AddParameter(new DalParameter("LogLevelId", (byte)logLevel, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationEventId", eventId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("Message", message, typeof(string)));

        dalParameterList.AddParameter(new DalParameter("RunModeId", runModeId, runModeId != null ? typeof(byte) : typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("ProductId", productId > 0 ? productId : null, typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", environmentId > 0 ? environmentId : null, typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId > 0 ? migrationRunId : null, typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId > 0 ? migrationRecordId : null, typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("ReleaseVersion", string.IsNullOrWhiteSpace(releaseVersion) ? null : releaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetGroupAlias", string.IsNullOrWhiteSpace(targetGroupAlias) ? null : targetGroupAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetAlias", string.IsNullOrWhiteSpace(targetAlias) ? null : targetAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("Filename", string.IsNullOrWhiteSpace(fileName) ? null : fileName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileOrderId", fileOrderId > 0 ? fileOrderId : null, typeof(int?)));
        dalParameterList.AddParameter(new DalParameter("FileBlockId", fileBlockId > 0 ? fileBlockId : null, typeof(int?)));

        try
        {
            _dal!.ExecuteNonQuery(_templateLoggingInsert!.Content, _dalSettings!, dalParameterList);
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{_templateLoggingInsert}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Initializes database logging by creating the required schema and loading templates.
    /// </summary>
    /// <returns>True if the logging infrastructure was newly created, false if it already existed.</returns>
    public bool InitDatabaseLogger(string loggingDatabaseType, TemplateCache templateCache, TemplateExecutor templateExecutor)
    {
        var templateLoggingCheckCreate = templateCache.GetTemplate(loggingDatabaseType, TemplateType.DatabaseLogging_CheckCreate, _options.DatabaseLogging);
        _templateLoggingInsert = templateCache.GetTemplate(loggingDatabaseType, TemplateType.DatabaseLogging_Insert, _options.DatabaseLogging);

        // Create DB logging-infrastructure if needed
        TemplateResponse templateResponseCheckPrerequisites = templateExecutor.ExecuteScalarWithNegativeResultCodeException(templateLoggingCheckCreate, _dal!, _dalSettings!, null);

        _isDatabaseLoggingInitialized = true;

        bool hasCreatedRepository = templateResponseCheckPrerequisites.ResultCode == 1;
        return hasCreatedRepository;
    }
}
