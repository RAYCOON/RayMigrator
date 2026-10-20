using System.Diagnostics;
using System.Globalization;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The retention contract every engine keeps (ADR-027): the repository maintenance service deletes the history and the
/// log rows older than a cutoff in batches, the oldest first, and leaves <c>MigrationRun</c> and <c>MigrationRecord</c>
/// alone, so status and the next migrate-up do not change; the rows of an open run stay until it is closed, a log row
/// without a run falls by its age; a dry run counts the same rows; the batch size, the time budget and the token bound
/// a call; a log that is not in the repository database is skipped. The scenarios age rows with UPDATEs instead of
/// waiting, because SQLite, MySQL and MariaDB store whole seconds.
/// </summary>
internal static class RepositoryMaintenanceContract
{
    private const string Release1 = "Release_1.0";
    private const int HistoryRowsOfRelease1 = 3;
    private const int HistoryRowsOfAllReleases = 12;

    private static readonly DalSettings QuerySettings = new() { UseTransaction = false, DbCommandTimeoutInSeconds = 30 };

    private static DateTime Tomorrow => DateTime.UtcNow.AddDays(1);

    /// <summary>
    /// A cutoff in the future: every history and log row goes, the run and record rows stay, status reports the same and the
    /// next migrate-up runs no file. The run history of info reports no migrations for the cleaned runs.
    /// </summary>
    public static async Task VerifyFutureCutoffDeletesHistoryAndLogAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var statusBefore = await ctx.InfoAsync();
        ctx.FlushDatabaseLog();
        int runs = ctx.CountMigrationRuns();
        int records = ctx.CountMigrations();
        int runMeta = ctx.CountRepoRows("MigrationRunMeta");
        int history = ctx.CountMigrationHistory();
        int log = ctx.CountLogEntries();
        history.Should().Be(HistoryRowsOfAllReleases, because: "two runs migrated every file of the four releases");
        log.Should().BePositive(because: "the runs logged into the repository");

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        result.Should().Be(new RetentionResult(history, log, result.Batches, Completed: true, LogSkipped: false, Message: null));
        result.Batches.Should().BeGreaterThanOrEqualTo(2, because: "each table took at least one batch");
        ctx.CountMigrationHistory().Should().Be(0);
        ctx.CountLogEntries().Should().Be(0);
        ctx.CountMigrationRuns().Should().Be(runs, because: "MigrationRun is never deleted");
        ctx.CountMigrations().Should().Be(records, because: "MigrationRecord is never deleted");
        ctx.CountRepoRows("MigrationRunMeta").Should().Be(runMeta, because: "MigrationRunMeta is never deleted");

        var statusAfter = await ctx.InfoAsync();
        statusAfter.Should().BeEquivalentTo(statusBefore, because: "status reads MigrationRecord and MigrationRun only");
        var runHistory = await ctx.GetHistoryAsync();
        runHistory.Runs.Should().HaveCount(2, because: "the run rows stay")
            .And.OnlyContain(r => r.TotalMigrations == 0 && r.SuccessfulMigrations == 0 && r.FailedMigrations == 0,
                because: "the run history counts the history rows, which are gone");

        // Act: the next migrate-up decides on MigrationRecord and finds nothing to do
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var observer = new RecordingProgressObserver();
        ctx.UseProgressObserver(observer);
        var next = await ctx.MigrateUpAsync();

        // Assert
        next.Success.Should().BeTrue(because: $"a migrate-up after the cleanup behaves as before it: {next.ErrorMessage}");
        observer.Of<FileStartedEvent>().Should().BeEmpty(because: "every file is migrated, whatever the history says");
        ctx.CountMigrations().Should().Be(records);
    }

    /// <summary>
    /// The rows of a run that is still open stay whatever their age; once the orphan fix closes the run, the next cleanup
    /// deletes them.
    /// </summary>
    public static async Task VerifyOpenRunKeepsItsRowsAsync(ScenarioBuilder builder)
    {
        // Arrange: an open run whose heartbeat is ten minutes old owns the history and log rows of the second run
        await using var ctx = await TwoRunsAsync(builder);
        var naming = NamingOf(ctx);
        int secondRun = ctx.GetMigrationRun(2)!.Id;
        ctx.InsertOpenMigrationRun(DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddMinutes(-10));
        int openRun = ctx.GetMigrationRun(3)!.Id;
        Execute(ctx, $"UPDATE {Table(naming, RepositoryTable.MigrationRecordHistory)} SET {Column(naming, "MigrationRunId")} = {openRun} WHERE {Column(naming, "MigrationRunId")} = {secondRun}");
        Execute(ctx, $"UPDATE {Table(naming, RepositoryTable.MigrationLog)} SET {Column(naming, "MigrationRunId")} = {openRun} WHERE {Column(naming, "MigrationRunId")} = {secondRun}");
        long openHistory = await CountOfRunAsync(ctx, RepositoryTable.MigrationRecordHistory, openRun);
        long openLog = await CountOfRunAsync(ctx, RepositoryTable.MigrationLog, openRun);
        openHistory.Should().Be(HistoryRowsOfAllReleases - HistoryRowsOfRelease1);
        openLog.Should().BePositive();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        result.Completed.Should().BeTrue();
        ctx.CountMigrationHistory().Should().Be((int)openHistory, because: "the history of an open run stays");
        ctx.CountLogEntries().Should().Be((int)openLog, because: "the log of an open run stays, every other row was older than the cutoff");
        result.HistoryRows.Should().Be(HistoryRowsOfRelease1);

        // Act: the orphan fix closes the run, then its rows go as well
        (await ctx.FixOrphanedRunAsync(openRun, thresholdMinutes: 2)).Should().BeTrue(because: "the heartbeat of the open run is ten minutes old");
        ctx.FlushDatabaseLog();
        int logBefore = ctx.CountLogEntries();
        var second = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        second.HistoryRows.Should().Be(openHistory);
        second.LogRows.Should().Be(logBefore);
        ctx.CountMigrationHistory().Should().Be(0);
        ctx.CountLogEntries().Should().Be(0);
    }

    /// <summary>A log row without a run falls by its age: the old one goes, the recent one and the rows of the runs stay.</summary>
    public static async Task VerifyLogRowsWithoutRunFallByAgeAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        var naming = NamingOf(ctx);
        string log = Table(naming, RepositoryTable.MigrationLog);
        string columns = string.Join(",", new[] { "LogLevelId", "MigrationRunId", "Message", "CreatedAt" }.Select(c => Column(naming, c)));
        Execute(ctx, $"INSERT INTO {log} ({columns}) VALUES (2, NULL, 'old', {TimestampSql(naming, DateTime.UtcNow.AddDays(-3))})");
        Execute(ctx, $"INSERT INTO {log} ({columns}) VALUES (2, NULL, 'recent', {TimestampSql(naming, DateTime.UtcNow)})");
        int logBefore = ctx.CountLogEntries();
        int history = ctx.CountMigrationHistory();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(null, DateTime.UtcNow.AddDays(-1)));

        // Assert
        result.LogRows.Should().Be(1, because: "only the row without a run is older than the cutoff");
        result.HistoryRows.Should().Be(0, because: "the request names no history cutoff");
        (await CountWhereAsync(ctx, RepositoryTable.MigrationLog, $"{Column(naming, "Message")} = 'old'")).Should().Be(0);
        (await CountWhereAsync(ctx, RepositoryTable.MigrationLog, $"{Column(naming, "Message")} = 'recent'")).Should().Be(1);
        ctx.CountLogEntries().Should().Be(logBefore - 1);
        ctx.CountMigrationHistory().Should().Be(history);
    }

    /// <summary>A cutoff between the runs deletes the rows of the older run only.</summary>
    public static async Task VerifyCutoffBetweenTheRunsDeletesTheOlderRowsAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        var (firstRun, secondRun) = AgeTheFirstRun(ctx);
        long firstHistory = await CountOfRunAsync(ctx, RepositoryTable.MigrationRecordHistory, firstRun);
        long firstLog = await CountOfRunAsync(ctx, RepositoryTable.MigrationLog, firstRun);
        long secondHistory = await CountOfRunAsync(ctx, RepositoryTable.MigrationRecordHistory, secondRun);
        int logBefore = ctx.CountLogEntries();
        firstHistory.Should().Be(HistoryRowsOfRelease1);
        firstLog.Should().BePositive();
        var cutoff = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(cutoff, cutoff));

        // Assert
        result.HistoryRows.Should().Be(firstHistory);
        result.LogRows.Should().Be(firstLog);
        result.Completed.Should().BeTrue();
        (await CountOfRunAsync(ctx, RepositoryTable.MigrationRecordHistory, firstRun)).Should().Be(0);
        (await CountOfRunAsync(ctx, RepositoryTable.MigrationLog, firstRun)).Should().Be(0);
        (await CountOfRunAsync(ctx, RepositoryTable.MigrationRecordHistory, secondRun)).Should().Be(secondHistory, because: "the second run is younger than the cutoff");
        ctx.CountLogEntries().Should().Be(logBefore - (int)firstLog);
    }

    /// <summary>A dry run counts exactly the rows the cleanup then deletes, and deletes nothing.</summary>
    public static async Task VerifyDryRunCountsWithoutDeletingAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        AgeTheFirstRun(ctx);
        int history = ctx.CountMigrationHistory();
        int log = ctx.CountLogEntries();
        var cutoff = DateTime.UtcNow.AddDays(-1);

        // Act
        var dryRun = await CleanUpAsync(ctx, new RetentionRequest(cutoff, cutoff, DryRun: true));

        // Assert
        dryRun.HistoryRows.Should().Be(HistoryRowsOfRelease1);
        dryRun.LogRows.Should().BePositive();
        dryRun.Batches.Should().Be(0);
        dryRun.Completed.Should().BeTrue();
        dryRun.LogSkipped.Should().BeFalse();
        dryRun.Message.Should().StartWith("Dry run");
        ctx.CountMigrationHistory().Should().Be(history, because: "a dry run deletes nothing");
        ctx.CountLogEntries().Should().Be(log);

        // Act
        var cleanup = await CleanUpAsync(ctx, new RetentionRequest(cutoff, cutoff));

        // Assert
        cleanup.HistoryRows.Should().Be(dryRun.HistoryRows, because: "the count templates count the rows the delete templates delete");
        cleanup.LogRows.Should().Be(dryRun.LogRows);
    }

    /// <summary>With a batch size of one, n rows take n batches.</summary>
    public static async Task VerifyBatchSizeOneDeletesRowByRowAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, null, BatchSize: 1));

        // Assert
        result.HistoryRows.Should().Be(history);
        result.Batches.Should().Be(history, because: "every batch deletes one row");
        result.Completed.Should().BeTrue();
        ctx.CountMigrationHistory().Should().Be(0);
    }

    /// <summary>A time budget of zero stops after the first batch; the next call deletes the rest.</summary>
    public static async Task VerifyZeroTimeBudgetStopsAfterOneBatchAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();
        int log = ctx.CountLogEntries();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow, BatchSize: 2, TimeBudget: TimeSpan.Zero));

        // Assert
        result.Completed.Should().BeFalse(because: "the budget ran out after the first batch");
        result.Batches.Should().Be(1);
        result.HistoryRows.Should().Be(2);
        result.LogRows.Should().Be(0, because: "the log comes after the history");
        result.Message.Should().Contain("run the cleanup again");
        ctx.CountMigrationHistory().Should().Be(history - 2);
        ctx.CountLogEntries().Should().Be(log);

        // Act
        var rest = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        rest.Completed.Should().BeTrue();
        rest.HistoryRows.Should().Be(history - 2);
        rest.LogRows.Should().Be(log);
        ctx.CountMigrationHistory().Should().Be(0);
        ctx.CountLogEntries().Should().Be(0);
    }

    /// <summary>
    /// A cleanup that stops early has deleted the oldest rows. The history rows are aged one day apart in the order they
    /// were written, which is also the order of the clustered key in which SQL Server deletes, and the two log rows written
    /// first are made the oldest; a budget of zero stops after one batch of two rows, and exactly the oldest are gone.
    /// </summary>
    public static async Task VerifyPartialCleanupDeletesTheOldestRowsFirstAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        var naming = NamingOf(ctx);
        string id = Column(naming, "Id");
        string history = Table(naming, RepositoryTable.MigrationRecordHistory);
        string log = Table(naming, RepositoryTable.MigrationLog);
        var historyIds = await IdsAsync(ctx, RepositoryTable.MigrationRecordHistory);
        historyIds.Should().HaveCount(HistoryRowsOfAllReleases);
        for (int i = 0; i < historyIds.Count; i++)
        {
            Execute(ctx, $"UPDATE {history} SET {Column(naming, "HistorizedAt")} = {TimestampSql(naming, DateTime.UtcNow.AddDays(i - historyIds.Count))} WHERE {id} = {historyIds[i]}");
        }

        var logIds = await IdsAsync(ctx, RepositoryTable.MigrationLog);
        logIds.Should().HaveCountGreaterThan(2);
        string createdAt = Column(naming, "CreatedAt");
        Execute(ctx, $"UPDATE {log} SET {createdAt} = {TimestampSql(naming, DateTime.UtcNow.AddDays(-2))}");
        Execute(ctx, $"UPDATE {log} SET {createdAt} = {TimestampSql(naming, DateTime.UtcNow.AddDays(-3))} WHERE {id} IN ({logIds[0]}, {logIds[1]})");

        // Act: one batch of two rows of each table
        var historyResult = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, null, BatchSize: 2, TimeBudget: TimeSpan.Zero));
        var logResult = await CleanUpAsync(ctx, new RetentionRequest(null, Tomorrow, BatchSize: 2, TimeBudget: TimeSpan.Zero));

        // Assert
        historyResult.HistoryRows.Should().Be(2);
        historyResult.Completed.Should().BeFalse(because: "the budget stopped the cleanup after its first batch");
        (await IdsAsync(ctx, RepositoryTable.MigrationRecordHistory)).Should().Equal(historyIds.Skip(2),
            because: "the batch took the two oldest history rows, the younger ones stay for the next call");
        logResult.LogRows.Should().Be(2);
        (await IdsAsync(ctx, RepositoryTable.MigrationLog)).Should().Equal(logIds.Skip(2),
            because: "the batch took the two oldest log rows");
    }

    /// <summary>A repository without log tables: the log is skipped, the history is cleaned, no log table appears.</summary>
    public static async Task VerifyRepositoryWithoutLogTablesSkipsTheLogAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.WithDatabaseLoggingDisabled().BuildAsync();
        (await ctx.MigrateUpAsync()).Success.Should().BeTrue();
        ctx.AssertRepositoryTableExists("MigrationLog", false);
        int history = ctx.CountMigrationHistory();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        result.LogSkipped.Should().BeTrue();
        result.LogRows.Should().Be(0);
        result.HistoryRows.Should().Be(history);
        result.Completed.Should().BeTrue();
        result.Message.Should().Contain("no log table");
        ctx.AssertRepositoryTableExists("MigrationLog", false);
    }

    /// <summary>A log in a database of its own is not the repository's: skipped and left as it is.</summary>
    public static async Task VerifyLogInADatabaseOfItsOwnIsLeftAloneAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder.WithDatabaseLoggingInto(config.ConnectionString2!).WithSerilogMinimumLevel("Debug").BuildAsync();
        (await ctx.MigrateUpAsync()).Success.Should().BeTrue();
        ctx.FlushDatabaseLog();
        var logDatabase = new RepositoryQueryHelper(config.DatabaseType, config.ConnectionString2!, config.SchemaName);
        int log = logDatabase.CountLogEntries();
        log.Should().BePositive(because: "the run logged into the database of its own");
        ctx.AssertRepositoryTableExists("MigrationLog", false);
        int history = ctx.CountMigrationHistory();

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow));

        // Assert
        result.LogSkipped.Should().BeTrue();
        result.LogRows.Should().Be(0);
        result.HistoryRows.Should().Be(history, because: "the history of the repository is cleaned all the same");
        ctx.CountMigrationHistory().Should().Be(0);
        logDatabase.CountLogEntries().Should().Be(log, because: "the cleanup never reaches a log outside the repository database");
        ctx.AssertRepositoryTableExists("MigrationLog", false);
    }

    /// <summary>A cancel between two batches returns what was deleted so far; the rest stays for the next call.</summary>
    public static async Task VerifyCancelBetweenBatchesAsync(ScenarioBuilder builder)
    {
        // Arrange: the logger cancels when the service reports the first batch
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();
        int log = ctx.CountLogEntries();
        using var cancellation = new CancellationTokenSource();
        var logger = new CancellingLogger(cancellation, "in batch [1]");

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow, BatchSize: 1), cancellation.Token, logger);

        // Assert
        logger.Cancelled.Should().BeTrue(because: "the first batch was reported");
        result.Completed.Should().BeFalse();
        result.Batches.Should().Be(1, because: "the token is checked before the second batch");
        result.HistoryRows.Should().Be(1);
        result.LogRows.Should().Be(0);
        result.Message.Should().Contain("cancelled");
        ctx.CountMigrationHistory().Should().Be(history - 1);
        ctx.CountLogEntries().Should().Be(log);
    }

    /// <summary>
    /// SQLite: an error after a batch deleted rows ends the cleanup with what it deleted (not completed, the error in the
    /// message) and logs it as a warning with the exception. Another connection takes the write lock after the first
    /// batch, so the second batch fails when the command timeout of one second has passed; the cleanup does not retry it.
    /// </summary>
    public static async Task VerifyFailureAfterADeletedBatchReturnsWhatWasDeletedAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();
        var logger = new LockingLogger(ctx.RepositoryOptions.ConnectionString!, "in batch [1]");
        var stopwatch = Stopwatch.StartNew();

        // Act
        RetentionResult result;
        try
        {
            result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow, BatchSize: 1, TimeBudget: TimeSpan.FromSeconds(30)),
                logger: logger, commandTimeoutInSeconds: 1);
        }
        finally
        {
            logger.Release();
        }

        stopwatch.Stop();

        // Assert
        logger.Locked.Should().BeTrue(because: "the first batch was reported");
        result.Completed.Should().BeFalse(because: "the second batch failed");
        result.Batches.Should().Be(1);
        result.HistoryRows.Should().Be(1, because: "the rows of the first batch stay deleted and the result reports them");
        result.LogRows.Should().Be(0);
        result.Message.Should().Contain("database is locked").And.Contain("run it again");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), because: "the failed batch is not retried");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception != null, because: "the failure is logged with its exception");
        ctx.CountMigrationHistory().Should().Be(history - 1);
    }

    /// <summary>
    /// A cancel in a dry run ends the call with an <see cref="OperationCanceledException"/>: nothing was deleted, so there is
    /// nothing to report. The logger cancels when the service starts to count, after the checks.
    /// </summary>
    public static async Task VerifyCancelInADryRunThrowsAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();
        using var cancellation = new CancellationTokenSource();
        var logger = new CancellingLogger(cancellation, "Cleaning up the repository");

        // Act
        var act = () => CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow, DryRun: true), cancellation.Token, logger);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Cancelled.Should().BeTrue();
        ctx.CountMigrationHistory().Should().Be(history, because: "a dry run deletes nothing");
    }

    /// <summary>
    /// A cancel between the history and the check of the log table returns what the cleanup deleted so far and does not
    /// throw: the history rows are gone, the log is untouched, and the next call deletes it. The logger cancels when the
    /// service reports the batch of the history.
    /// </summary>
    public static async Task VerifyCancelAtTheCheckOfTheLogReturnsTheDeletedHistoryAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await TwoRunsAsync(builder);
        int history = ctx.CountMigrationHistory();
        int log = ctx.CountLogEntries();
        using var cancellation = new CancellationTokenSource();
        var logger = new CancellingLogger(cancellation, "row(s) of MigrationRecordHistory older than");

        // Act
        var result = await CleanUpAsync(ctx, new RetentionRequest(Tomorrow, Tomorrow), cancellation.Token, logger);

        // Assert
        logger.Cancelled.Should().BeTrue(because: "the history took one batch");
        result.Completed.Should().BeFalse();
        result.HistoryRows.Should().Be(history);
        result.LogRows.Should().Be(0);
        result.LogSkipped.Should().BeFalse(because: "the cleanup did not get to ask for the log table");
        result.Message.Should().Contain("cancelled");
        ctx.CountMigrationHistory().Should().Be(0);
        ctx.CountLogEntries().Should().Be(log);
    }

    /// <summary>Two runs on a fresh repository with the log in it: release 1.0, then the rest; the log is flushed.</summary>
    private static async Task<ScenarioContext> TwoRunsAsync(ScenarioBuilder builder)
    {
        var ctx = await builder.WithDatabaseLogging("Debug").WithSerilogMinimumLevel("Debug").BuildAsync();
        try
        {
            var first = await ctx.MigrateUpAsync(Release1);
            first.Success.Should().BeTrue(because: $"the first run migrates release 1.0: {first.ErrorMessage}");
            var second = await ctx.MigrateUpAsync();
            second.Success.Should().BeTrue(because: $"the second run migrates the rest: {second.ErrorMessage}");
            ctx.FlushDatabaseLog();
            return ctx;
        }
        catch
        {
            await ctx.DisposeAsync();
            throw;
        }
    }

    /// <summary>Moves the history and log rows of the first run two days into the past; returns the ids of both runs.</summary>
    private static (int FirstRun, int SecondRun) AgeTheFirstRun(ScenarioContext ctx)
    {
        var naming = NamingOf(ctx);
        int firstRun = ctx.GetMigrationRun(1)!.Id;
        int secondRun = ctx.GetMigrationRun(2)!.Id;
        string twoDaysAgo = TimestampSql(naming, DateTime.UtcNow.AddDays(-2));
        string runId = Column(naming, "MigrationRunId");
        Execute(ctx, $"UPDATE {Table(naming, RepositoryTable.MigrationRecordHistory)} SET {Column(naming, "HistorizedAt")} = {twoDaysAgo} WHERE {runId} = {firstRun}");
        Execute(ctx, $"UPDATE {Table(naming, RepositoryTable.MigrationLog)} SET {Column(naming, "CreatedAt")} = {twoDaysAgo} WHERE {runId} = {firstRun}");
        return (firstRun, secondRun);
    }

    private static Task<RetentionResult> CleanUpAsync(ScenarioContext ctx, RetentionRequest request,
        CancellationToken? cancellationToken = null, ILogger<RepositoryMaintenanceService>? logger = null, int? commandTimeoutInSeconds = null)
    {
        var service = new RepositoryMaintenanceService(new TemplateCache(false, NullLogger<TemplateCache>.Instance),
            logger ?? NullLogger<RepositoryMaintenanceService>.Instance);

        // The options as a host holds them: a fresh copy, which the service completes with its defaults
        var repository = new RepositoryOptions
        {
            DatabaseType = ctx.RepositoryOptions.DatabaseType,
            ConnectionString = ctx.RepositoryOptions.ConnectionString,
            SchemaName = ctx.RepositoryOptions.SchemaName,
            TableBaseName = ctx.RepositoryOptions.TableBaseName,
            DbCommandTimeoutInSeconds = commandTimeoutInSeconds
        };
        return service.CleanUpAsync(repository, request, cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private static RepositoryNaming NamingOf(ScenarioContext ctx)
    {
        var repository = ctx.RepositoryOptions;
        return new RepositoryNaming(repository.DatabaseType!, string.IsNullOrEmpty(repository.SchemaName) ? null : repository.SchemaName, repository.TableBaseName);
    }

    private static string Table(RepositoryNaming naming, RepositoryTable table) => naming.QualifiedTableName(table);

    private static string Column(RepositoryNaming naming, string column) => naming.QualifiedColumnName(column);

    /// <summary>A UTC timestamp literal with second precision in the dialect of the engine (the MySQL session runs in UTC).</summary>
    private static string TimestampSql(RepositoryNaming naming, DateTime utc)
    {
        string text = utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return naming.DatabaseType switch
        {
            "SqlServer" => $"CONVERT(datetime2(3), '{text.Replace(' ', 'T')}', 126)",
            "PostgreSQL" => $"TIMESTAMPTZ '{text}+00'",
            "MariaDb" or "MySql" => $"TIMESTAMP '{text}'",
            _ => $"'{text}'"
        };
    }

    private static void Execute(ScenarioContext ctx, string sql) => ctx.ExecuteOnConnection(ctx.RepositoryOptions.ConnectionString!, sql);

    /// <summary>The ids of the rows of <paramref name="table"/> in ascending order.</summary>
    private static async Task<List<long>> IdsAsync(ScenarioContext ctx, RepositoryTable table)
    {
        var naming = NamingOf(ctx);
        DalFactory.TryGetDal(naming.DatabaseType, ctx.RepositoryOptions.ConnectionString!, out IDal? dal).Should().BeTrue();
        string id = Column(naming, "Id");
        var rows = await dal!.ExecuteReaderAsync($"SELECT {id} FROM {Table(naming, table)} ORDER BY {id}", QuerySettings, null,
            TestContext.Current.CancellationToken);
        return rows.Select(row => Convert.ToInt64(row.Values.First(), CultureInfo.InvariantCulture)).ToList();
    }

    private static Task<long> CountOfRunAsync(ScenarioContext ctx, RepositoryTable table, int migrationRunId)
        => CountWhereAsync(ctx, table, $"{Column(NamingOf(ctx), "MigrationRunId")} = {migrationRunId}");

    private static async Task<long> CountWhereAsync(ScenarioContext ctx, RepositoryTable table, string condition)
    {
        var naming = NamingOf(ctx);
        DalFactory.TryGetDal(naming.DatabaseType, ctx.RepositoryOptions.ConnectionString!, out IDal? dal).Should().BeTrue();
        object? count = await dal!.ExecuteScalarAsync($"SELECT COUNT(*) FROM {Table(naming, table)} WHERE {condition}", QuerySettings, null,
            TestContext.Current.CancellationToken);
        return Convert.ToInt64(count, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A logger that records every entry and, when a message with <c>marker</c> is logged, takes the write lock of the
    /// SQLite database on a connection of its own until <see cref="Release"/>.
    /// </summary>
    private sealed class LockingLogger : ILogger<RepositoryMaintenanceService>
    {
        private readonly string _connectionString;
        private readonly string _marker;
        private SqliteConnection? _lock;

        public LockingLogger(string connectionString, string marker)
        {
            _connectionString = connectionString;
            _marker = marker;
        }

        public bool Locked => _lock != null;

        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            Entries.Add((logLevel, message, exception));
            if (_lock != null || !message.Contains(_marker, StringComparison.Ordinal))
                return;

            _lock = new SqliteConnection(_connectionString + ";Pooling=False");
            _lock.Open();
            using var begin = _lock.CreateCommand();
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        public void Release()
        {
            _lock?.Dispose();
        }
    }

    /// <summary>A logger that cancels a token when a message with <c>marker</c> is logged.</summary>
    private sealed class CancellingLogger : ILogger<RepositoryMaintenanceService>
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly string _marker;

        public CancellingLogger(CancellationTokenSource cancellation, string marker)
        {
            _cancellation = cancellation;
            _marker = marker;
        }

        public bool Cancelled { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (Cancelled || !formatter(state, exception).Contains(_marker, StringComparison.Ordinal))
                return;

            Cancelled = true;
            _cancellation.Cancel();
        }
    }
}
