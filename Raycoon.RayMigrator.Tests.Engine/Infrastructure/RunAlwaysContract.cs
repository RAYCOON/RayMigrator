using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The rule every engine keeps for a RunAlways file: migrate-up runs it in full on every run, from its first block. It
/// never skips the blocks that an earlier attempt committed before it failed, was cancelled or ended abruptly, and it
/// never finalizes a record of it that committed every block without its final status; the file starts with
/// <see cref="FileStartedEvent.ResumeFromBlock"/> 0 in both target orders, block by block and on the atomic shared
/// connection. The orphan repair settles an open record of a RunAlways file by its progress like that of any other file,
/// and the file still runs in full. The scenarios make the three-block seed of release 1.0 a RunAlways file; the blocks
/// of the earlier attempt come from a failure, a cancel or a record state written by SQL.
/// </summary>
internal static class RunAlwaysContract
{
    private const string Release1 = "Release_1.0";
    private const string Backend = "Backend";
    private const string MainDb = "MainDB";
    private const string Seed = "03_SeedDataA.sql";
    private const string Reason = "stopped by the test";

    /// <summary>The table block 2 of the seed inserts into before a test creates it, on the second database.</summary>
    private const string Probe = "runalwaysprobe";

    /// <summary>The three-block seed of release 1.0 (alpha, beta, gamma, unless a block is given) as a RunAlways file.</summary>
    private static ScenarioBuilder WithRunAlwaysSeed(ScenarioBuilder builder, EngineConfig config, string? secondBlock = null)
        => CancellationContract.WithThreeBlockSeed(builder, config, secondBlock).SetFileToml(Release1, Seed, "RunAlways", "true");

    /// <summary>Drops <see cref="Probe"/> on the second database when it exists; best effort, so that it never hides a result.</summary>
    private static void DropProbe(ScenarioContext ctx, EngineConfig config)
    {
        try
        {
            ctx.ExecuteOnConnection(config.ConnectionString2!, SqlDialect.GetDropSimpleTableIfExistsSql(config.DatabaseType, Probe));
        }
        catch
        {
            // A table that is left over is dropped by the next scenario that uses it
        }
    }

    /// <summary>Runs release 1.0 and cancels it with Terminate after block 1 of the seed, which leaves the block committed.</summary>
    private static async Task CommitTheFirstBlockOfTheSeedAsync(ScenarioContext ctx, EngineConfig config)
    {
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
    }

    /// <summary>The numbers of the blocks of the seed that a migrate-up completed, in their order.</summary>
    private static IEnumerable<int> SeedBlocks(IEnumerable<BlockCompletedEvent> blocks)
        => blocks.Where(b => b.FileName == Seed && b.Operation == MigrationOperation.MigrateUp).Select(b => b.BlockNumber);

    /// <summary>Asserts that the seed ran in full and ended Migrated with all three blocks.</summary>
    private static void AssertSeedRanInFull(
        ScenarioContext ctx, IReadOnlyList<FileStartedEvent> started, IEnumerable<BlockCompletedEvent> blocks)
    {
        started.Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "a RunAlways file starts at its first block");
        SeedBlocks(blocks).Should().Equal([1, 2, 3],
            because: "every block of a RunAlways file runs, the committed ones of the earlier attempt included");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 3,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// (a) A RunAlways file that failed after a committed block runs again from its first block: block 2 of the seed
    /// inserts into a table that does not exist yet, so the first run commits block 1 and fails at block 2, which leaves
    /// the record Failed with block 1, as for any file. Once the table exists, the next migrate-up starts the unchanged
    /// seed with ResumeFromBlock 0 and runs all three blocks, block 1 a second time. <paramref name="order"/>
    /// TargetByTarget covers the other file loop of migrate-up.
    /// </summary>
    public static async Task VerifyFailedAttemptRunsInFullAsync(
        ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order = TargetMigrationOrder.FileByFile)
    {
        // Arrange: block 2 inserts into a table that the test creates only before the next run
        await using var ctx = await WithRunAlwaysSeed(builder, config,
                SqlDialect.GetInsertIntoSimpleTableSql(config.DatabaseType, Probe, "from block 2"))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithTargetMigrationOrder(order)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();
        DropProbe(ctx, config);
        try
        {
            var first = await ctx.MigrateUpAsync(Release1);
            first.Success.Should().BeFalse(because: "block 2 of the seed finds no table");
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Failed,
                FileUpBlocksMigrated = 1,
                FileUpBlocksTotal = 3
            });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
            ctx.ExecuteOnConnection(config.ConnectionString2!, SqlDialect.GetCreateSimpleTableSql(config.DatabaseType, Probe));
            await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
            var recorder = new RecordingProgressObserver();
            ctx.UseProgressObserver(recorder);

            // Act
            var next = await ctx.MigrateUpAsync(Release1);

            // Assert
            next.Success.Should().BeTrue(because: $"block 2 finds its table now: {next.ErrorMessage}");
            AssertSeedRanInFull(ctx, recorder.Of<FileStartedEvent>(), recorder.Of<BlockCompletedEvent>());
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, Probe, 1);
        }
        finally
        {
            DropProbe(ctx, config);
        }
    }

    /// <summary>
    /// (b) A RunAlways file cancelled with Terminate after its first block runs again from its first block: the cancel
    /// leaves the record Failed with block 1, as for any file. When the next migrate-up starts the seed, its record counts
    /// no committed block, because nothing of the earlier attempt carries over; the seed starts with ResumeFromBlock 0 and
    /// runs all three blocks, block 1 a second time.
    /// </summary>
    public static async Task VerifyCancelledAttemptRunsInFullAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await WithRunAlwaysSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        await CommitTheFirstBlockOfTheSeedAsync(ctx, config);
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        MigrationRecordDto? atTheStart = null;
        var probe = new ProbingProgressObserver(e =>
        {
            if (e is FileStartedEvent { FileName: Seed })
            {
                atTheStart = ctx.GetMigrationRecord(Seed);
            }
        });
        ctx.UseProgressObserver(probe);

        // Act
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"the seed runs again: {next.ErrorMessage}");
        atTheStart.Should().NotBeNull();
        atTheStart!.MigrationStatusId.Should().Be((int)MigrationStatus.Pending);
        atTheStart.FileUpBlocksMigrated.Should().Be(0,
            because: "the record of a RunAlways file starts without the blocks of the earlier attempt");
        AssertSeedRanInFull(ctx, probe.Of<FileStartedEvent>(), probe.Of<BlockCompletedEvent>());
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 4);
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
    }

    /// <summary>
    /// (c) On the atomic shared connection a RunAlways file runs in full as well: after release 1.0 migrated, the record of
    /// the seed is set by SQL to what an earlier attempt leaves, Failed with block 1 (an attempt that committed block by
    /// block) or Executing with every block (an attempt whose final status never reached the repository, in a run that
    /// ended). The next migrate-up neither resumes the seed after the committed blocks nor finalizes the record: it runs
    /// all three blocks in one transaction and reports the seed Migrated, not Recovered.
    /// </summary>
    public static async Task VerifyEarlierAttemptOnTheSharedConnectionRunsInFullAsync(
        ScenarioBuilder builder, EngineConfig config, MigrationStatus status, int committedBlocks)
    {
        // Arrange: the target shares the repository connection, so a file with UseTransaction runs atomically
        await using var ctx = await WithRunAlwaysSeed(builder, config).BuildAsync();
        var first = await ctx.MigrateUpAsync(Release1);
        first.Success.Should().BeTrue(because: $"release 1.0 migrates: {first.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString, "tablea", 3);
        ctx.SetMigrationRecordState(Seed, MainDb, status, fileUpBlocksMigrated: committedBlocks);
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        // Act
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"the seed runs again: {next.ErrorMessage}");
        recorder.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.Outcome.Should().Be(FileOutcome.Migrated,
                because: "the record of a RunAlways file is never finalized from an earlier attempt");
        AssertSeedRanInFull(ctx, recorder.Of<FileStartedEvent>(), recorder.Of<BlockCompletedEvent>());
        ctx.AssertRowCountOnConnection(config.ConnectionString, "tablea", 6);
    }

    /// <summary>
    /// (d) The orphan repair settles an open record of a RunAlways file by its progress, and the file still runs in full:
    /// block 1 of the seed committed, and a run that ended abruptly left the record Executing with block 1 (the open run,
    /// whose heartbeat is three minutes old, and the record of that run are written by SQL). While the run is open, info
    /// lists the seed RunAlways without a resume point. The next migrate-up closes the run as orphaned and repairs the
    /// record to Failed with block 1 before it starts, as for any file, and then runs the seed from block 1.
    /// </summary>
    public static async Task VerifyOrphanedRecordRunsInFullAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 1 committed, then the footprint of a run that ended abruptly while block 2 ran
        await using var ctx = await WithRunAlwaysSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        await CommitTheFirstBlockOfTheSeedAsync(ctx, config);
        int runs = ctx.CountMigrationRuns();
        ctx.InsertOrphanedMigrationRun(minutesOld: 0, heartbeatMinutesOld: 3);
        int orphanRunIndex = runs + 1;
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1,
            migrationRunId: ctx.GetMigrationRun(orphanRunIndex)!.Id);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act: the pending list while the run is open
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.RunAlways, null),
            because: "the repair keeps block 1, but migrate-up runs a RunAlways file in full");

        // Act: the next migrate-up repairs the record before its first file and runs the seed
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        MigrationRecordDto? afterTheRepair = null;
        var probe = new ProbingProgressObserver(e =>
        {
            if (e is RunStartedEvent)
            {
                afterTheRepair = ctx.GetMigrationRecord(Seed);
            }
        });
        ctx.UseProgressObserver(probe);
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"the seed runs after the repair: {next.ErrorMessage}");
        ctx.GetMigrationRun(orphanRunIndex)!.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error,
            because: "the auto-fix closes the orphaned run");
        afterTheRepair.Should().NotBeNull();
        afterTheRepair!.MigrationStatusId.Should().Be((int)MigrationStatus.Failed,
            because: "the repair settles the record by its progress, whatever the file");
        afterTheRepair.FileUpBlocksMigrated.Should().Be(1);
        AssertSeedRanInFull(ctx, probe.Of<FileStartedEvent>(), probe.Of<BlockCompletedEvent>());
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 4);
    }
}
