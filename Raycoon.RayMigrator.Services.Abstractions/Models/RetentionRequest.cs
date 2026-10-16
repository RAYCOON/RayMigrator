namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// What <see cref="IRepositoryMaintenanceService.CleanUpAsync"/> deletes and how: the cutoff of each table, the size of a
/// batch, the time the cleanup may take and whether it only counts.
/// </summary>
/// <param name="HistoryCutoffUtc">Deletes the <c>MigrationRecordHistory</c> rows historized before this UTC time; null
/// leaves the history alone. The cutoff counts to the whole second, so a row is never deleted unless it is older.</param>
/// <param name="LogCutoffUtc">Deletes the <c>MigrationLog</c> rows created before this UTC time; null leaves the log
/// alone. At least one of the two cutoffs is set.</param>
/// <param name="BatchSize">The most rows one batch deletes, at least 1. The default stays below the number of row locks
/// at which SQL Server escalates a statement to a table lock, which would hold back the log writes of running hosts for
/// the length of the batch.</param>
/// <param name="TimeBudget">How long the cleanup may delete: after the batch in which it runs out, the cleanup stops and
/// reports that it did not complete; the first batch always runs. Null means <see cref="DefaultTimeBudget"/>,
/// <see cref="Timeout.InfiniteTimeSpan"/> no limit; any other negative value is refused.</param>
/// <param name="DryRun">Counts the rows the cleanup would delete and deletes nothing.</param>
public sealed record RetentionRequest(
    DateTime? HistoryCutoffUtc,
    DateTime? LogCutoffUtc,
    int BatchSize = RetentionRequest.DefaultBatchSize,
    TimeSpan? TimeBudget = null,
    bool DryRun = false)
{
    /// <summary>
    /// The batch size of a request that names none. SQL Server escalates a statement to a table lock once it holds 5,000
    /// locks on one index, and a delete takes a key lock per row and a page lock per page it touches in every index: with
    /// 2,000 rows a batch stays below 4,000 locks per index even when every row lies on a page of its own. Measured with
    /// the indexes of schema version 3, history batches of 4,800 rows escalated (about ten rows per page), log batches of
    /// 5,000 rows.
    /// </summary>
    public const int DefaultBatchSize = 2000;

    /// <summary>The time budget of a request that names none: one minute.</summary>
    public static TimeSpan DefaultTimeBudget { get; } = TimeSpan.FromSeconds(60);
}
