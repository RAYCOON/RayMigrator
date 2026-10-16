using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Default <see cref="IRepositoryMaintenanceService"/>: validates the repository options the way the repository schema
/// service does, masks their secrets for the log, checks the schema version of the repository and runs the retention
/// templates of <see cref="RepositoryMaintenanceExecutor"/> batch by batch within the time budget of the request.
/// </summary>
public sealed class RepositoryMaintenanceService : IRepositoryMaintenanceService
{
    /// <summary>
    /// The schema version whose step added the indexes the cleanup of the history filters by (<c>MigrationRunId</c>,
    /// <c>HistorizedAt</c>); an older repository is cleaned with a scan of the table per batch.
    /// </summary>
    private const int HistoryIndexesVersion = 3;

    private const string HistoryTable = "MigrationRecordHistory";
    private const string LogTable = "MigrationLog";

    private readonly TemplateCache _templateCache;
    private readonly ILogger<RepositoryMaintenanceService> _logger;

    /// <summary>Creates the service over the host's template cache.</summary>
    public RepositoryMaintenanceService(TemplateCache templateCache, ILogger<RepositoryMaintenanceService> logger)
    {
        ArgumentNullException.ThrowIfNull(templateCache);
        ArgumentNullException.ThrowIfNull(logger);

        _templateCache = templateCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RetentionResult> CleanUpAsync(RepositoryOptions repository, RetentionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(request);
        TimeSpan? budget = EffectiveTimeBudget(request);

        // The schema check logs the schema name and an error may quote the connection string: masked, as a run masks them.
        using var masking = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        IDal dal = RepositoryAccess.Prepare(repository, _templateCache, _logger);

        cancellationToken.ThrowIfCancellationRequested();
        await CheckSchemaVersionAsync(repository, dal, cancellationToken);

        _logger.LogDebug(
            "Cleaning up the repository: history before [{HistoryCutoff}], log before [{LogCutoff}], batch size [{BatchSize}], time budget [{TimeBudget}], dry run [{DryRun}]",
            Describe(request.HistoryCutoffUtc), Describe(request.LogCutoffUtc), request.BatchSize, budget?.ToString() ?? "none", request.DryRun);

        var executor = new RepositoryMaintenanceExecutor(_templateCache, repository, dal);
        return request.DryRun
            ? await CountAsync(executor, request, cancellationToken)
            : await DeleteAsync(executor, request, budget, cancellationToken);
    }

    /// <summary>
    /// The time budget of the request after its checks: null for no limit. The request is refused before the database is
    /// touched when it names no cutoff, a cutoff that is not UTC, a batch size below 1 or a negative budget.
    /// </summary>
    private static TimeSpan? EffectiveTimeBudget(RetentionRequest request)
    {
        if (request.HistoryCutoffUtc == null && request.LogCutoffUtc == null)
            throw new ArgumentException("The retention request names no cutoff: set HistoryCutoffUtc, LogCutoffUtc or both.", nameof(request));

        RequireUtc(request.HistoryCutoffUtc, nameof(RetentionRequest.HistoryCutoffUtc));
        RequireUtc(request.LogCutoffUtc, nameof(RetentionRequest.LogCutoffUtc));

        if (request.BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), request.BatchSize, $"The BatchSize of a retention request must be at least [1], not [{request.BatchSize}].");

        TimeSpan budget = request.TimeBudget ?? RetentionRequest.DefaultTimeBudget;
        if (budget == Timeout.InfiniteTimeSpan)
            return null;

        if (budget < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request), budget, $"The TimeBudget of a retention request must not be negative, not [{budget}]; Timeout.InfiniteTimeSpan sets no limit.");

        return budget;
    }

    private static void RequireUtc(DateTime? cutoff, string name)
    {
        if (cutoff != null && cutoff.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"The {name} of a retention request must be a UTC time (DateTimeKind.Utc), not [{cutoff.Value.Kind}].", "request");
    }

    /// <summary>
    /// Refuses a database without a repository, a repository of a RayMigrator before 0.17.0 and one of a newer schema
    /// version, whose tables this engine does not know. An older schema version is cleaned as it is: the templates read
    /// only columns of schema version 1, and an upgrade would lock out the engines that still use the repository. The
    /// check runs without the retries of the repository, as the retention templates do (see
    /// <see cref="RepositoryMaintenanceExecutor"/>), so that a time budget is not exceeded before the first batch.
    /// </summary>
    private async Task CheckSchemaVersionAsync(RepositoryOptions repository, IDal dal, CancellationToken cancellationToken)
    {
        int version = await new RepositorySchemaExecutor(_templateCache, _logger, WithoutRetries(repository), dal,
            AssemblyInfoHelper.GetRayMigratorVersion(), RepositorySchema.CurrentVersion).CheckVersionAsync(cancellationToken);

        if (version == 0)
        {
            throw new RepositorySchemaException(
                $"The database (DatabaseType [{dal.DatabaseType}], schema [{SensitiveDataMasker.Mask(repository.SchemaName)}]) has no RayMigrator repository under the configured prefix, so there is nothing to clean up. " +
                "Check the connection string, the schema name and the table prefix.",
                version, RepositorySchema.CurrentVersion);
        }

        if (version < HistoryIndexesVersion)
        {
            _logger.LogWarning(
                "The repository has schema version [{SchemaVersion}]: the indexes of version [{IndexVersion}] that the cleanup of the history filters by are missing, so every batch reads the whole table. The cleanup does not upgrade the repository; the next command of this engine does.",
                version, HistoryIndexesVersion);
        }
    }

    /// <summary>The repository options of the schema check: those of the host with no retry of a failed command.</summary>
    private static RepositoryOptions WithoutRetries(RepositoryOptions repository) => new()
    {
        DatabaseType = repository.DatabaseType,
        ConnectionString = repository.ConnectionString,
        SchemaName = repository.SchemaName,
        TableBaseName = repository.TableBaseName,
        DbCommandTimeoutInSeconds = repository.DbCommandTimeoutInSeconds,
        DbCommandMaxRetries = 0,
        DbCommandWaitTimeInMsBeforeRetry = repository.DbCommandWaitTimeInMsBeforeRetry,
        HeartbeatIntervalInSeconds = repository.HeartbeatIntervalInSeconds,
        OrphanedRunThresholdInMinutes = repository.OrphanedRunThresholdInMinutes
    };

    /// <summary>The dry run: counts the rows of each table the request names, without a batch and without deleting.</summary>
    private async Task<RetentionResult> CountAsync(RepositoryMaintenanceExecutor executor, RetentionRequest request, CancellationToken cancellationToken)
    {
        long historyRows = 0;
        long logRows = 0;
        bool logSkipped = false;

        if (request.HistoryCutoffUtc != null)
            historyRows = await executor.CountHistoryAsync(request.HistoryCutoffUtc.Value, cancellationToken);

        if (request.LogCutoffUtc != null)
        {
            long? rows = await executor.CountLogAsync(request.LogCutoffUtc.Value, cancellationToken);
            logSkipped = rows == null;
            logRows = rows ?? 0;
        }

        _logger.LogInformation(
            "Repository cleanup (dry run): [{HistoryRows}] history row(s) and [{LogRows}] log row(s) are older than their cutoff; nothing was deleted",
            historyRows, logRows);

        return new RetentionResult(historyRows, logRows, 0, true, logSkipped, Message(StopReason.None, null, logSkipped, dryRun: true));
    }

    /// <summary>
    /// Deletes the history, then the log, batch by batch. The token is checked before every batch and handed to the batch
    /// in progress; the budget is checked before every batch once a batch has deleted rows, so that a call always deletes
    /// one batch when there is one, also with a budget of zero. A failure after a batch deleted rows ends the call with
    /// what it deleted and the error in the message, because those rows are gone and the host must learn of them; a
    /// failure before that is thrown, as nothing changed.
    /// </summary>
    private async Task<RetentionResult> DeleteAsync(RepositoryMaintenanceExecutor executor, RetentionRequest request, TimeSpan? budget, CancellationToken cancellationToken)
    {
        var progress = new Progress(budget, Stopwatch.GetTimestamp());
        bool logSkipped = false;
        Exception? failure = null;

        try
        {
            if (request.HistoryCutoffUtc != null)
                await DeleteBatchesAsync(HistoryTable, request.HistoryCutoffUtc.Value, request.BatchSize, executor.DeleteHistoryAsync, progress, cancellationToken);

            if (request.LogCutoffUtc != null && progress.Stop == StopReason.None)
            {
                if (progress.BudgetSpent)
                {
                    progress.Stop = StopReason.TimeBudget;
                }
                else
                {
                    // The delete template assumes the log table is in the repository database; asking needs no count
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!await executor.LogTableExistsAsync(cancellationToken))
                    {
                        logSkipped = true;
                    }
                    else
                    {
                        await DeleteBatchesAsync(LogTable, request.LogCutoffUtc.Value, request.BatchSize, executor.DeleteLogAsync, progress, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The batch in progress rolled back or was never started; the batches before it stay deleted.
            progress.Stop = StopReason.Cancelled;
        }
        catch (Exception ex) when (progress.Batches > 0)
        {
            // The batches before the failure stay deleted: the result reports them instead of the exception
            progress.Stop = StopReason.Failed;
            failure = ex;
            _logger.LogWarning(ex, "Repository cleanup stopped at an error after [{Batches}] batch(es) had deleted rows", progress.Batches);
        }

        bool completed = progress.Stop == StopReason.None;
        string? message = Message(progress.Stop, budget, logSkipped, dryRun: false, failure);

        _logger.LogInformation(
            "Repository cleanup: [{HistoryRows}] history row(s) and [{LogRows}] log row(s) deleted in [{Batches}] batch(es) and [{ElapsedMs}] ms; completed [{Completed}]. {Message}",
            progress.HistoryRows, progress.LogRows, progress.Batches, (long)progress.Elapsed.TotalMilliseconds, completed, message ?? string.Empty);

        return new RetentionResult(progress.HistoryRows, progress.LogRows, progress.Batches, completed, logSkipped, message);
    }

    /// <summary>
    /// Deletes batches of one table until a batch finds fewer rows than it may delete, which leaves no matching row, or the
    /// budget is spent before the next batch.
    /// </summary>
    private async Task DeleteBatchesAsync(
        string table,
        DateTime cutoffUtc,
        int batchSize,
        Func<DateTime, int, CancellationToken, Task<int>> deleteBatchAsync,
        Progress progress,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (progress.BudgetSpent)
            {
                progress.Stop = StopReason.TimeBudget;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            int deleted = await deleteBatchAsync(cutoffUtc, batchSize, cancellationToken);
            progress.Add(table == HistoryTable, deleted);

            _logger.LogDebug("Deleted [{Rows}] row(s) of {Table} older than [{Cutoff}] in batch [{Batch}]", deleted, table, Describe(cutoffUtc), progress.Batches);

            if (deleted < batchSize)
                return;
        }
    }

    private static string? Message(StopReason stop, TimeSpan? budget, bool logSkipped, bool dryRun, Exception? failure = null)
    {
        List<string> sentences = [];
        if (dryRun)
            sentences.Add("Dry run: the rows were counted and nothing was deleted.");
        if (stop == StopReason.Failed)
            sentences.Add($"The cleanup stopped at an error before every row older than its cutoff was deleted: {ErrorText(failure!)} The rows deleted before stay deleted; run it again to delete the rest.");
        if (stop == StopReason.TimeBudget)
            sentences.Add($"The time budget of [{budget!.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)}] seconds ran out before every row older than its cutoff was deleted; run the cleanup again to delete the rest.");
        if (stop == StopReason.Cancelled)
            sentences.Add("The cleanup was cancelled before every row older than its cutoff was deleted; run it again to delete the rest.");
        if (logSkipped)
            sentences.Add("The repository database has no log table under the schema and prefix of the repository: the log is written elsewhere or not at all and was left alone.");

        return sentences.Count == 0 ? null : string.Join(" ", sentences);
    }

    private static string Describe(DateTime? cutoffUtc) => cutoffUtc == null ? "none" : RepositoryMaintenanceExecutor.FormatCutoff(cutoffUtc.Value);

    /// <summary>The messages of an exception and its inner exceptions: the provider's report is the innermost one.</summary>
    private static string ErrorText(Exception exception)
    {
        List<string> messages = [];
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (!messages.Contains(current.Message, StringComparer.Ordinal))
                messages.Add(current.Message.TrimEnd('.') + ".");
        }

        return string.Join(" ", messages);
    }

    private enum StopReason
    {
        None,
        TimeBudget,
        Cancelled,
        Failed
    }

    /// <summary>What the deletion did so far and whether its budget is spent.</summary>
    private sealed class Progress
    {
        private readonly TimeSpan? _budget;
        private readonly long _startedAt;

        public Progress(TimeSpan? budget, long startedAt)
        {
            _budget = budget;
            _startedAt = startedAt;
        }

        public long HistoryRows { get; private set; }
        public long LogRows { get; private set; }
        public int Batches { get; private set; }
        public StopReason Stop { get; set; }

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startedAt);

        /// <summary>Spent once a batch has deleted rows and the budget has run out; the first rows are always deleted.</summary>
        public bool BudgetSpent => Batches > 0 && _budget != null && Elapsed >= _budget.Value;

        public void Add(bool history, int deleted)
        {
            if (deleted == 0)
                return;

            Batches++;
            if (history)
                HistoryRows += deleted;
            else
                LogRows += deleted;
        }
    }
}
