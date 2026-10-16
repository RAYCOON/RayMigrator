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

    // Initialized from method-call InitDatabaseLoggerAsync()
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

            // A section without MinimumLevel means Information; the defaults fill it, but a host may build the options by hand.
            _minimumLogLevel = Enum.TryParse<LogLevel>(options.DatabaseLogging?.MinimumLevel, true, out var level) ? level : LogLevel.Information;
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
    /// Waits until every enqueued log entry has been written, without closing the queue: a host that runs several
    /// migrations in one process calls it at the end of a run; the CLI calls <see cref="Flush"/> at shutdown instead.
    /// </summary>
    /// <param name="timeout">How long to wait at most; null waits without limit.</param>
    /// <returns>True when the queue is empty; false when the timeout elapsed first.</returns>
    public Task<bool> FlushAsync(TimeSpan? timeout = null)
    {
        return _databaseLoggerQueue.WaitForEmptyAsync(timeout);
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
    /// Writes a log entry to the database via DAL. Runs on the queue's own thread, outside any async flow, so it calls
    /// the synchronous <see cref="IDal.ExecuteNonQuery"/>.
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
    /// <param name="loggingDatabaseType">The database type of the log tables.</param>
    /// <param name="templateCache">The templates of the logging database type.</param>
    /// <param name="templateExecutor">Runs <c>DatabaseLogging_CheckCreate</c>.</param>
    /// <param name="cancellationToken">Reaches the DAL call that creates the log tables.</param>
    /// <returns>True if the logging infrastructure was newly created, false if it already existed.</returns>
    public async Task<bool> InitDatabaseLoggerAsync(
        string loggingDatabaseType,
        TemplateCache templateCache,
        TemplateExecutor templateExecutor,
        CancellationToken cancellationToken = default)
    {
        var templateLoggingCheckCreate = templateCache.GetTemplate(loggingDatabaseType, TemplateType.DatabaseLogging_CheckCreate, _options.DatabaseLogging);
        _templateLoggingInsert = templateCache.GetTemplate(loggingDatabaseType, TemplateType.DatabaseLogging_Insert, _options.DatabaseLogging);

        // Create DB logging-infrastructure if needed
        TemplateResponse templateResponseCheckPrerequisites = await CheckCreateAsync(templateLoggingCheckCreate, templateExecutor, cancellationToken);

        _isDatabaseLoggingInitialized = true;

        bool hasCreatedRepository = templateResponseCheckPrerequisites.ResultCode == 1;
        return hasCreatedRepository;
    }

    /// <summary>
    /// Runs <c>DatabaseLogging_CheckCreate</c>, which since 0.20.0 also builds the two indexes of the log table over the
    /// rows already there. A transient failure is retried as a log write is, a timeout is not: the attempt was rolled
    /// back whole, so a retry would build the index from the start again, hold back the log writes of other hosts once
    /// more and time out again. A timeout ends the initialization with a message that names the index build and the
    /// command timeout to raise for the first start on a large log.
    /// </summary>
    private async Task<TemplateResponse> CheckCreateAsync(Template template, TemplateExecutor templateExecutor, CancellationToken cancellationToken)
    {
        var dal = _dal!;
        var dalBase = dal as DalBase;
        var settings = new DalSettings
        {
            UseTransaction = _dalSettings!.UseTransaction,
            DbCommandTimeoutInSeconds = _dalSettings.DbCommandTimeoutInSeconds,
            MaxRetries = 0
        };

        try
        {
            return await RetryHelper.ExecuteWithRetryAsync(
                () => templateExecutor.ExecuteScalarWithNegativeResultCodeExceptionAsync(template, dal, settings, null, cancellationToken: cancellationToken),
                _dalSettings.MaxRetries,
                _dalSettings.RetryDelayMs,
                ex => dalBase == null || dalBase.IsTimeout(ex) ? (false, null) : dalBase.IsTransient(ex),
                operationDescription: TemplateType.DatabaseLogging_CheckCreate.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && dalBase != null && dalBase.IsTimeout(ex))
        {
            throw new TemplateExecutionException(
                $"Template [{template}] ran into the command timeout of the database log ({settings.DbCommandTimeoutInSeconds} s). " +
                "Since 0.20.0 it creates the indexes of the log table on MigrationRunId and CreatedAt over the rows already there, " +
                "which a large log may need longer for: raise DatabaseLogging.DbCommandTimeoutInSeconds for the first start after the update.",
                ex);
        }
    }
}
