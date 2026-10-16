using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The orphan repair every engine runs when a later start (the auto-fix of migrate-up) or <c>fix</c> closes a run that
/// ended abruptly: each Pending or Executing record of that run ends as a failure at the block that was running would
/// have left it. A record with some committed blocks becomes Failed with them, so that the next migrate-up continues
/// after them; one with every block committed becomes Migrated; a rollback that was running becomes Failed with its
/// rollback blocks (migrate-up refuses the file, migrate-down continues), Failed without rollback progress, or
/// NotMigrated when every rollback block had run; a record without progress becomes NotMigrated. The repair sets
/// FinishedAt to the time of the repair where it is empty and leaves DurationInMs as it is: a file that was running gets
/// the time of the repair without a duration, a rollback that was running keeps the end and the duration of its last
/// terminal status. It closes the run as Error with the engine code <see cref="TemplateResultCode.OrphanedRunClosed"/>
/// and a message that names its last heartbeat in UTC. <c>fix</c> with an explicit status sets that status on every
/// open record.
/// The engine leaves the committed blocks where it can (a cancel, a full migrate-down); the two facts of an abrupt end
/// that no command leaves are written by SQL: the open run, whose heartbeat is older than the threshold of two minutes
/// (<see cref="ScenarioContext.InsertOrphanedMigrationRun"/>), and the open record of that run
/// (<see cref="ScenarioContext.SetMigrationRecordState"/>).
/// </summary>
internal static class OrphanRepairContract
{
    private const string Release1 = "Release_1.0";
    private const string Release2 = "Release_2.0";
    private const string MainDb = "MainDB";
    private const string Seed = "03_SeedDataA.sql";
    private const string CreateTableC = "01_CreateTableC.sql";
    private const string CreateTableD = "02_CreateTableD.sql";
    private const string SeedB = "03_SeedDataB.sql";
    private const string Reason = "stopped by the test";

    /// <summary>The three-block seed of release 1.0 on the second database, where every block commits on its own.</summary>
    private static ScenarioBuilder SeedOnTheSecondDatabase(ScenarioBuilder builder, EngineConfig config)
        => CancellationContract.WithThreeBlockSeed(builder, config).WithTargetConnectionString(config.ConnectionString2!);

    /// <summary>Runs release 1.0 and cancels it with Terminate after block 1 of the seed, which leaves the block committed.</summary>
    private static async Task CommitTheFirstBlockOfTheSeedAsync(ScenarioContext ctx, EngineConfig config)
    {
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
    }

    /// <summary>
    /// Inserts the open run of an abrupt end and returns its id: started now, so that it is the newest run of the product
    /// and environment, as an open run always is under the run lock, and its last heartbeat three minutes old, older than
    /// the orphan threshold of two minutes.
    /// </summary>
    private static int InsertOrphanRun(ScenarioContext ctx)
    {
        int runs = ctx.CountMigrationRuns();
        ctx.InsertOrphanedMigrationRun(minutesOld: 0, heartbeatMinutesOld: 3);
        return ctx.GetMigrationRun(runs + 1)!.Id;
    }

    /// <summary>Release 2.0 on the second database with a rollback of its seed in three blocks (one row each, unless given).</summary>
    private static ScenarioBuilder ReleaseTwoOnTheSecondDatabase(ScenarioBuilder builder, EngineConfig config, string? rollbackOfSeedB = null)
    {
        rollbackOfSeedB ??= SqlDialect.JoinBlocks(config.DatabaseType,
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product1"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product2"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product3"));
        return builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithFileBytes($"{Release2}/Backend/03_SeedDataB.rollback.sql", Encoding.UTF8.GetBytes(rollbackOfSeedB + "\n"));
    }

    /// <summary>Collects the rendered warnings of the host, from every thread that logs.</summary>
    private static ConcurrentQueue<string> CollectWarnings(ScenarioContext ctx)
    {
        var warnings = new ConcurrentQueue<string>();
        ctx.UseLogObserver(logEvent =>
        {
            if (logEvent.Level == LogEventLevel.Warning)
            {
                warnings.Enqueue(logEvent.RenderMessage());
            }
        });
        return warnings;
    }

    /// <summary>
    /// Asserts that the repair kept <paramref name="end"/>, the end and the duration of the last terminal status of a record
    /// whose rollback was running: the engine keeps them while it rolls back, and the repair sets FinishedAt only where it
    /// is empty and leaves DurationInMs as it is.
    /// </summary>
    private static void AssertEndKeptByTheRepair(ScenarioContext ctx, string filename, (string? FinishedAt, long? DurationInMs) end)
    {
        end.FinishedAt.Should().NotBeNull(because: "the record reached a terminal status before its rollback started");
        end.DurationInMs.Should().NotBeNull();
        ctx.GetMigrationRecordEnd(filename).Should().Be(end, because: "the repair of a rollback leaves the end of its last terminal status");
    }

    /// <summary>Asserts that the repair set FinishedAt of a record whose file was running and left DurationInMs empty.</summary>
    private static void AssertEndedByTheRepair(ScenarioContext ctx, string filename)
    {
        var (finishedAt, durationInMs) = ctx.GetMigrationRecordEnd(filename);
        finishedAt.Should().NotBeNull(because: "the repair ends the record it settles");
        durationInMs.Should().BeNull(because: "the time until the repair is no execution time");
    }

    /// <summary>
    /// Asserts that the repair closed the run at <paramref name="runIndex"/> (1-based, oldest first) as Error with the
    /// engine code <see cref="TemplateResultCode.OrphanedRunClosed"/> and a message that says so and names the last
    /// heartbeat in UTC, so that the history tells the abrupt end from a failure: the heartbeat the history reads from the
    /// run row as a UTC time, to the second, three minutes before the repair (<see cref="InsertOrphanRun"/>). A message
    /// in the time zone of the session differs from it by the offset of that zone, which the scenarios on PostgreSQL bring
    /// about in every environment, also in UTC, because their sessions run in a time zone other than UTC.
    /// </summary>
    private static async Task AssertClosedByTheRepairAsync(ScenarioContext ctx, int runIndex)
    {
        var run = ctx.GetMigrationRun(runIndex)!;
        run.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error, because: "the repair closes the orphaned run");
        run.ErrorCode.Should().Be(TemplateResultCode.OrphanedRunClosed.ToString(CultureInfo.InvariantCulture),
            because: "the run row says that the repair closed it");
        var message = Regex.Match(run.ErrorMessage ?? string.Empty,
            @"^Closed as orphaned by the repair: the run ended without finishing; last heartbeat (?<heartbeat>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) UTC$");
        message.Success.Should().BeTrue(because: $"the message says that the repair closed the run and names its last heartbeat: '{run.ErrorMessage}'");
        DateTime heartbeat = DateTime.ParseExact(message.Groups["heartbeat"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var history = await ctx.GetHistoryAsync();
        DateTime? lastHeartbeat = history.Runs.Should().ContainSingle(r => r.MigrationRunId == run.Id).Which.LastHeartbeatAt;
        lastHeartbeat.Should().NotBeNull(because: "the test wrote a heartbeat for the run");
        heartbeat.Should().Be(lastHeartbeat!.Value.AddTicks(-(lastHeartbeat.Value.Ticks % TimeSpan.TicksPerSecond)),
            because: "the message names the last heartbeat of the run in UTC, to the second");
        heartbeat.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(-3), TimeSpan.FromMinutes(5),
            because: "the test wrote the heartbeat three minutes in the past");
    }

    /// <summary>
    /// (a) The auto-fix of the next migrate-up keeps the committed blocks: the seed committed block 1 in a run that ended
    /// abruptly while block 2 ran (Executing with block 1). The next migrate-up closes that run as Error with the code and
    /// the message of the repair, repairs the record to Failed with block 1 and resumes the seed with block 2, so block 1
    /// does not run again.
    /// </summary>
    public static async Task VerifyAutoFixResumesAfterTheCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await CommitTheFirstBlockOfTheSeedAsync(ctx, config);
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var warnings = CollectWarnings(ctx);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        // Act
        var result = await ctx.MigrateUpAsync(Release1);

        // Assert
        result.Success.Should().BeTrue(because: $"the repaired seed is resumed: {result.ErrorMessage}");
        await AssertClosedByTheRepairAsync(ctx, 2);
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(1, because: "the repair kept block 1, which committed before the abrupt end");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        warnings.Should().Contain(w => w.Contains("Auto-fixed orphaned MigrationRun") && w.Contains("by their progress"),
            because: "the auto-fix says how it settled the open records");
    }

    /// <summary>
    /// (b) <c>fix</c> without a status applies the same rule: it closes the run as Error with the code and the message of
    /// the repair, the record of the seed becomes Failed with block 1 and gets an end, and the next migrate-up resumes with
    /// block 2.
    /// </summary>
    public static async Task VerifyFixKeepsTheCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await CommitTheFirstBlockOfTheSeedAsync(ctx, config);
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

        // Act
        var fix = await ctx.FixIssuesAsync();

        // Assert
        fix.Success.Should().BeTrue(because: $"fix closes the orphaned run: {fix.ErrorMessage}");
        fix.OrphanedRunsFixed.Should().Be(1);
        await AssertClosedByTheRepairAsync(ctx, 2);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        AssertEndedByTheRepair(ctx, Seed);

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"the seed is resumed: {next.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>
    /// (c) <c>fix</c> with an explicit status sets it on the open record and keeps its counters: NotMigrated with block 1,
    /// with an end; the next migrate-up runs the seed from block 1, so block 1 runs a second time.
    /// </summary>
    public static async Task VerifyFixWithAnExplicitStatusSetsItAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await CommitTheFirstBlockOfTheSeedAsync(ctx, config);
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

        // Act
        var fix = await ctx.FixIssuesAsync(assumedMigrationStatus: MigrationStatus.NotMigrated);

        // Assert
        fix.OrphanedRunsFixed.Should().Be(1);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileUpBlocksMigrated = 1
        });
        AssertEndedByTheRepair(ctx, Seed);

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"the seed runs again: {next.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(0);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 4);
    }

    /// <summary>
    /// (d) A record without a committed block becomes NotMigrated, as before: the run ended abruptly while block 1 of the
    /// seed ran (Executing with no block), and the next migrate-up runs the whole seed.
    /// </summary>
    public static async Task VerifyAutoFixWithoutProgressRunsTheWholeFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the seed started and was cancelled before its first block, so nothing of it committed
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(Seed, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 0, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        // Act
        var result = await ctx.MigrateUpAsync(Release1);

        // Assert
        result.Success.Should().BeTrue(because: $"the seed runs: {result.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(0);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>
    /// (e) A record with every block committed becomes Migrated: the run ended abruptly after the last block of the seed,
    /// before its final status (Executing with 3 of 3). <c>info</c> no longer lists the seed while the run is open, and
    /// the next migrate-up runs nothing of it.
    /// </summary>
    public static async Task VerifyAutoFixFinishesAFileWithEveryBlockCommittedAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated, then the footprint of a run that ended before the final status of the seed
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        (await ctx.MigrateUpAsync(Release1)).Success.Should().BeTrue();
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 3, migrationRunId: orphanRunId);

        // Act: the pending list while the run is open
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().NotContain(p => p.Filename == Seed,
            because: "the repair of the next migrate-up finishes the record, so the seed does not run again");

        // Act
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var result = await ctx.MigrateUpAsync(Release1);

        // Assert
        result.Success.Should().BeTrue(because: $"nothing is left to run: {result.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().NotContain(f => f.FileName == Seed);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
        AssertEndedByTheRepair(ctx, Seed);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>
    /// (f) A record that <c>fix</c> finishes as Migrated counts for the last migration of its target group: the repair ends
    /// it at the time of the repair, later than every other record, so <c>TargetGroupStatus.LastMigrationDate</c> moves to
    /// that end.
    /// </summary>
    public static async Task VerifyRepairedRecordCountsForTheLastMigrationAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated, the seed open with every block in an orphaned run, its end gone
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        (await ctx.MigrateUpAsync(Release1)).Success.Should().BeTrue();
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 3, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var before = (await ctx.InfoAsync()).TargetGroups["Backend"].LastMigrationDate;
        before.Should().NotBeNull(because: "the other files of release 1.0 are Migrated with an end");

        // The databases store the end with whole seconds at least, so the repair must come a second later to be later
        await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

        // Act
        (await ctx.FixIssuesAsync()).OrphanedRunsFixed.Should().Be(1);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var after = (await ctx.InfoAsync()).TargetGroups["Backend"].LastMigrationDate;

        // Assert
        ctx.AssertFileStatus(Seed, MigrationStatus.Migrated);
        after.Should().NotBeNull();
        after!.Value.Should().BeAfter(before!.Value, because: "the record the repair finished ends at the repair, after the other records");
    }

    /// <summary>
    /// (g) A rollback that was running with rollback progress becomes Failed with its rollback blocks: the seed of release
    /// 2.0 failed, the error recovery of the migrate-up rolled back block 1 of 3 of it, and the run ended abruptly while
    /// rollback block 2 ran. The next migrate-up repairs the record before its first file and then refuses, as after a
    /// cancelled rollback, and the migrate-down after it continues the rollback with block 2. The rollback progress comes
    /// from the error recovery, so that the orphan is the second run of the repository.
    /// </summary>
    public static async Task VerifyAutoFixKeepsAnIncompleteRollbackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the seed fails, the error recovery runs rollback block 1 of it and is cancelled before block 2
        await using var ctx = await ReleaseTwoOnTheSecondDatabase(builder, config)
            .InjectError(Release2, SeedB)
            .WithMigrationErrorAction(MigrationErrorAction.RollbackErrorOnly)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.Rollback),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release2, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Error,
            because: "the seed failed before the cancel stopped its error recovery");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(SeedB, MainDb, MigrationStatus.Executing, migrationRunId: orphanRunId);
        var end = ctx.GetMigrationRecordEnd(SeedB);
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        // Act
        var refused = await ctx.MigrateUpAsync(Release2);

        // Assert
        refused.Success.Should().BeFalse(because: "the seed must not run again before its rollback has completed");
        refused.ErrorMessage.Should().StartWith("Incomplete rollback: 1 file(s)")
            .And.Contain($"{Release2}/{SeedB} on target {MainDb}: 1 of 3 rollback blocks, migrate-down continues at block 2");
        ctx.GetMigrationRun(2)!.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error, because: "the auto-fix closes the orphaned run first");
        recorder.Of<FileStartedEvent>().Should().BeEmpty(because: "the run refuses before its first file");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        AssertEndKeptByTheRepair(ctx, SeedB, end);

        // Act: the next migrate-down continues the rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"migrate-down continues the rollback: {down.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertFileStatus(SeedB, MigrationStatus.NotMigrated);
    }

    /// <summary>
    /// (h) A rollback that had run every rollback block becomes NotMigrated, as a completed rollback: the migrate-down ended
    /// abruptly after the last rollback block of the seed, before its final status. <c>fix</c> repairs the record to
    /// NotMigrated with an end, and the next migrate-up runs release 2.0 again.
    /// </summary>
    public static async Task VerifyFixCompletesAFinishedRollbackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 rolled back completely, then the footprint of a migrate-down that ended before the final status
        await using var ctx = await ReleaseTwoOnTheSecondDatabase(builder, config).BuildAsync();
        (await ctx.MigrateUpAsync(Release2)).Success.Should().BeTrue();
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        (await ctx.MigrateDownAsync(Release1)).Success.Should().BeTrue();
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(SeedB, MainDb, MigrationStatus.Executing, fileDownBlocksMigrated: 3, migrationRunId: orphanRunId);
        var end = ctx.GetMigrationRecordEnd(SeedB);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

        // Act
        (await ctx.FixIssuesAsync()).OrphanedRunsFixed.Should().Be(1);

        // Assert
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileDownBlocksMigrated = 3,
            FileDownBlocksTotal = 3
        });
        AssertEndKeptByTheRepair(ctx, SeedB, end);

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var up = await ctx.MigrateUpAsync(Release2);

        // Assert
        up.Success.Should().BeTrue(because: $"release 2.0 runs again: {up.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(0);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 3);
    }

    /// <summary>
    /// (i) A rollback that was running without rollback progress becomes Failed: the first rollback block of the seed
    /// failed (the record Failed with no rollback block), and the footprint shows the record open in a run that ended
    /// abruptly. <c>fix</c> repairs it to Failed with an end, migrate-up does not refuse it, and the next migrate-up runs
    /// the seed from block 1.
    /// </summary>
    public static async Task VerifyFixFailsARollbackWithoutProgressAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated, the rollback of its seed fails in its first block and leaves the rows
        await using var ctx = await ReleaseTwoOnTheSecondDatabase(builder, config, SqlDialect.GetBrokenRollbackSql(config.DatabaseType))
            .BuildAsync();
        (await ctx.MigrateUpAsync(Release2)).Success.Should().BeTrue();
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        (await ctx.MigrateDownAsync(Release1)).Success.Should().BeFalse(because: "the rollback of the seed fails in its first block");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileDownBlocksMigrated = 0 });
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(SeedB, MainDb, MigrationStatus.Executing, migrationRunId: orphanRunId);
        var end = ctx.GetMigrationRecordEnd(SeedB);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

        // Act
        (await ctx.FixIssuesAsync()).OrphanedRunsFixed.Should().Be(1);

        // Assert
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 0
        });
        AssertEndKeptByTheRepair(ctx, SeedB, end);

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var up = await ctx.MigrateUpAsync(Release2);

        // Assert
        up.Success.Should().BeTrue(because: $"no rollback block ran, so nothing refuses the seed: {up.ErrorMessage}");
        recorder.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(0);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 6);
    }

    /// <summary>
    /// (j) A rollback record that stays Executing with part of its rollback blocks in a run that has ended is an
    /// incomplete rollback as well: the process ended between the two steps of the repair, which closed the run before it
    /// settled its records. The status lists the seed as RollbackIncomplete, the next migrate-up refuses it, as after a
    /// cancelled rollback, and migrate-down continues the rollback with block 2.
    /// </summary>
    public static async Task VerifyAnOpenRollbackOfAnEndedRunIsAnIncompleteRollbackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated, a migrate-down cancelled after rollback block 1 of 3 of the seed, then the footprint
        // of a run that took the record over and ended abruptly, and the first step of the repair, which closes that run
        await using var ctx = await ReleaseTwoOnTheSecondDatabase(builder, config).BuildAsync();
        (await ctx.MigrateUpAsync(Release2)).Success.Should().BeTrue();
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateDownAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(SeedB, MainDb, MigrationStatus.Executing, migrationRunId: orphanRunId);
        (await ctx.FixOrphanedRunAsync(orphanRunId, 2)).Should().BeTrue(because: "the first step of the repair closes the run");
        var openRollback = new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Executing,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        };
        ctx.AssertMigrationRecord(SeedB, openRollback);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: the pending list
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == SeedB)
            .Which.Reason.Should().Be(PendingMigrationReason.RollbackIncomplete, because: "the next migrate-up refuses the seed");

        // Act
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var refused = await ctx.MigrateUpAsync(Release2);

        // Assert
        refused.Success.Should().BeFalse(because: "running the seed again from block 1 would duplicate the rows the rollback kept");
        refused.ErrorMessage.Should().StartWith("Incomplete rollback: 1 file(s)")
            .And.Contain($"{Release2}/{SeedB} on target {MainDb}: 1 of 3 rollback blocks, migrate-down continues at block 2");
        recorder.Of<FileStartedEvent>().Should().BeEmpty(because: "the run refuses before its first file");
        ctx.AssertMigrationRecord(SeedB, openRollback);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: the next migrate-down continues the rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"migrate-down continues the rollback: {down.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertFileStatuses(
            (SeedB, MigrationStatus.NotMigrated),
            (CreateTableD, MigrationStatus.NotMigrated),
            (CreateTableC, MigrationStatus.NotMigrated));
    }

    /// <summary>
    /// (k) A record that the repair finishes as Migrated counts for the current release: release 1.0 migrated, and the
    /// first file of release 2.0 committed every block in a run that ended before its final status (Executing with every
    /// block), the only record of release 2.0. While that run is open, the status names release 2.0 as the current release
    /// and counts the file as executed, as the repair of the next migrate-up leaves it; that migrate-up repairs the record
    /// before it reads the release it starts from, so its run starts from release 2.0.
    /// </summary>
    public static async Task VerifyRecordFinishedByTheRepairCountsForTheCurrentReleaseAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated, then a migrate-up of release 2.0 stopped after its first file, whose record then
        // shows the footprint of a run that ended after its last block
        await using var ctx = await builder.WithTargetConnectionString(config.ConnectionString2!).BuildAsync();
        (await ctx.MigrateUpAsync(Release1)).Success.Should().BeTrue();
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterFile(CreateTableC, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release2, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        int blocks = ctx.GetMigrationRecord(CreateTableC)!.FileUpBlocksTotal;
        int orphanRunId = InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(CreateTableC, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: blocks, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act: the status while the run is open
        var status = await ctx.InfoAsync();

        // Assert
        status.CurrentRelease.Should().Be(Release2, because: "the repair finishes the record of release 2.0, as the pending list sees it");
        status.TargetGroups["Backend"].CurrentRelease.Should().Be(Release2);
        status.TotalMigrationsExecuted.Should().Be(4, because: "the three files of release 1.0 and the finished first file of release 2.0 are executed");
        status.PendingFiles.Should().NotContain(p => p.Filename == CreateTableC);

        // Act: the next migrate-up repairs the run first
        int runs = ctx.CountMigrationRuns();
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var up = await ctx.MigrateUpAsync(Release2);

        // Assert
        up.Success.Should().BeTrue(because: $"the rest of release 2.0 runs: {up.ErrorMessage}");
        ctx.AssertMigrationRun(runs + 1, new MigrationRunExpectation { FromReleaseVersion = Release2 });
    }

    /// <summary>
    /// The state of an open record for <see cref="VerifyTheRepairRuleMatchesTheProjectionAsync"/>: its status, its up
    /// counters, whether a rollback was running (FileDownHash set) and its rollback counters, NULL where null.
    /// </summary>
    private sealed record OpenRecord(
        MigrationStatus Status, int Up, int UpTotal, bool RollbackRunning = false, int? Down = null, int? DownTotal = null)
    {
        /// <summary>The record as the status reads it, for the projection of the repair.</summary>
        public MigrationRecord ToRecord() => new()
        {
            MigrationStatusId = Status,
            FileUpBlocksMigrated = Up,
            FileUpBlocksTotal = UpTotal,
            FileDownHash = RollbackRunning ? "rollback-hash" : null,
            FileDownBlocksMigrated = Down,
            FileDownBlocksTotal = DownTotal
        };
    }

    /// <summary>
    /// The open records of the table: a file in progress with no, some, every and more than every block committed, also
    /// Pending and with no block at all; a rollback in progress with no, some, every and more than every rollback block,
    /// with NULL counters, with no rollback block at all and over up counters that alone would decide otherwise; and
    /// rollback counters without FileDownHash, which decide nothing.
    /// </summary>
    private static readonly OpenRecord[] OpenRecords =
    [
        new(MigrationStatus.Pending, 0, 3),
        new(MigrationStatus.Executing, 0, 3),
        new(MigrationStatus.Executing, 1, 3),
        new(MigrationStatus.Executing, 3, 3),
        new(MigrationStatus.Executing, 4, 3),
        new(MigrationStatus.Pending, 2, 3),
        new(MigrationStatus.Executing, 0, 0),
        new(MigrationStatus.Executing, 1, 0),
        new(MigrationStatus.Executing, 3, 3, true, 0, 3),
        new(MigrationStatus.Executing, 3, 3, true, 1, 3),
        new(MigrationStatus.Executing, 3, 3, true, 3, 3),
        new(MigrationStatus.Executing, 3, 3, true, 4, 3),
        new(MigrationStatus.Executing, 3, 3, true, null, 3),
        new(MigrationStatus.Executing, 3, 3, true, 2, null),
        new(MigrationStatus.Executing, 3, 3, true, null, null),
        new(MigrationStatus.Executing, 1, 3, true, 0, 0),
        new(MigrationStatus.Pending, 1, 3, true, 3, 3),
        new(MigrationStatus.Executing, 2, 3, false, 3, 3)
    ];

    /// <summary>The files of the four releases of the scenario, one record each on MainDB.</summary>
    private static readonly string[] AllFiles =
    [
        "01_CreateTableA.sql", "02_CreateTableB.sql", Seed,
        CreateTableC, CreateTableD, SeedB,
        "01_CreateTableE.sql", "02_CreateTableF.sql", "03_SeedDataC.sql",
        "01_CreateTableG.sql", "02_CreateTableH.sql", "03_SeedDataD.sql"
    ];

    /// <summary>Puts the record of <paramref name="filename"/> into <paramref name="state"/>, open in the run <paramref name="migrationRunId"/>.</summary>
    private static void SetOpenRecord(ScenarioContext ctx, EngineConfig config, string filename, OpenRecord state, int migrationRunId)
    {
        var naming = new RepositoryNaming(config.DatabaseType, ctx.RepositoryOptions.SchemaName, ctx.RepositoryOptions.TableBaseName);
        string Column(string name) => naming.QualifiedColumnName(name);
        static string Value(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";
        string sql = $"UPDATE {naming.QualifiedTableName(RepositoryTable.MigrationRecord)} SET " +
                     $"{Column("MigrationStatusId")} = {(int)state.Status}, " +
                     $"{Column("FileUpBlocksMigrated")} = {state.Up}, {Column("FileUpBlocksTotal")} = {state.UpTotal}, " +
                     $"{Column("FileDownHash")} = {(state.RollbackRunning ? "'rollback-hash'" : "NULL")}, " +
                     $"{Column("FileDownBlocksMigrated")} = {Value(state.Down)}, {Column("FileDownBlocksTotal")} = {Value(state.DownTotal)}, " +
                     $"{Column("MigrationRunId")} = {migrationRunId} " +
                     $"WHERE {Column("Filename")} = '{filename}' AND {Column("TargetAlias")} = '{MainDb}'";
        ctx.ExecuteOnConnection(ctx.RepositoryOptions.ConnectionString!, sql);
    }

    /// <summary>
    /// (l) The rule of <c>Repository_MigrationRecord_FixOrphaned</c> is the rule of the projection with which the status
    /// lists the pending migrations (<c>MigrationService.OrphanRepairStatus</c>): for every open record of
    /// <see cref="OpenRecords"/>, <c>fix</c> without a status settles it as the projection does. The records of the
    /// twelve files of the scenario take the states in turns, each turn in a run of its own that ended abruptly.
    /// </summary>
    public static async Task VerifyTheRepairRuleMatchesTheProjectionAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: every file of the four releases migrated, twelve records to put into the states of the table
        await using var ctx = await builder.BuildAsync();
        (await ctx.MigrateUpAsync()).Success.Should().BeTrue();
        var outcomes = new HashSet<MigrationStatus>();

        foreach (var turn in OpenRecords.Chunk(AllFiles.Length))
        {
            int orphanRunId = InsertOrphanRun(ctx);
            var expected = new List<(string File, MigrationStatus Status)>();
            for (int i = 0; i < turn.Length; i++)
            {
                SetOpenRecord(ctx, config, AllFiles[i], turn[i], orphanRunId);
                expected.Add((AllFiles[i], MigrationService.OrphanRepairStatus(turn[i].ToRecord())));
            }

            await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);

            // Act
            (await ctx.FixIssuesAsync()).OrphanedRunsFixed.Should().Be(1);

            // Assert
            var repaired = expected.Select(e => (e.File, (MigrationStatus)ctx.GetMigrationRecord(e.File)!.MigrationStatusId)).ToList();
            repaired.Should().Equal(expected, because: "the template settles each open record as the projection of the status does");
            outcomes.UnionWith(expected.Select(e => e.Status));
        }

        outcomes.Order().Should().Equal([MigrationStatus.Failed, MigrationStatus.NotMigrated, MigrationStatus.Migrated],
            because: "the table reaches every status the rule gives");
    }
}
