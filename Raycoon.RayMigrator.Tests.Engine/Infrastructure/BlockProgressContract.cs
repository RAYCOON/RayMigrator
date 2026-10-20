using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Services.Abstractions;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The block counters every engine keeps on a migration record: <c>FileUpBlocksMigrated</c> and
/// <c>FileDownBlocksMigrated</c> count the leading blocks that committed on the target at every moment of a run, so that
/// a run that ends abruptly leaves a counter the next start can trust. A file that a CLI tool runs counts none while the
/// tool runs, and it runs the whole file as one block, so that it resumes and finalizes no earlier attempt, and a
/// rollback through the tool continues none; with
/// MigrationErrorAction=Ignore a failed block ends the count, and the record keeps it when the file ends or a cancel stops
/// it, so that the next migrate-up resumes at the failed block; with RollbackErrorAction=Ignore the count of the rollback
/// blocks ends the same way and a cancel keeps it, until the rollback ends, which stores its block count; a failed
/// rollback keeps the rollback blocks before it, also on the atomic path and through a CLI tool, whatever made the tool
/// fail, and a rollback block whose progress write failed counts; a resumed file and a continued rollback carry their
/// committed blocks from their start. The scenarios read the record in the flow of the run, at a progress event or a log
/// line.
/// </summary>
internal static class BlockProgressContract
{
    private const string Release1 = "Release_1.0";
    private const string Release2 = "Release_2.0";
    private const string CreateTableA = "01_CreateTableA.sql";
    private const string CreateTableC = "01_CreateTableC.sql";
    private const string CreateTableD = "02_CreateTableD.sql";
    private const string Seed = "03_SeedDataA.sql";
    private const string SeedB = "03_SeedDataB.sql";
    private const string SeedBRollback = "03_SeedDataB.rollback.sql";
    private const string Reason = "stopped by the test";

    /// <summary>The table a block of the seed inserts into before a test creates it, on the second database.</summary>
    private const string Probe = "ignoreprobe";

    /// <summary>The name of the check constraint or trigger with which <see cref="RefuseRollbackProgressWrite"/> refuses a write.</summary>
    private const string Refusal = "ck_test_refuse_rollback_progress";

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

    /// <summary>Whether a log event is the line the template executor writes after a <c>Repository_MigrationRecord_Update</c>.</summary>
    private static bool IsRecordUpdate(LogEvent logEvent)
        => logEvent.MessageTemplate.Text.StartsWith("Execution of Template", StringComparison.Ordinal)
           && logEvent.RenderMessage().Contains("TemplateType: Repository_MigrationRecord_Update,", StringComparison.Ordinal);

    /// <summary>The seed of release 2.0 rolls back in three blocks, one row each; <paramref name="secondBlock"/> replaces the second.</summary>
    private static ScenarioBuilder WithThreeBlockRollbackOfSeedB(ScenarioBuilder builder, EngineConfig config, string? secondBlock = null)
        => builder.WithFileBytes($"{Release2}/Backend/{SeedBRollback}", Encoding.UTF8.GetBytes(ThreeBlockRollbackOfSeedB(config, secondBlock) + "\n"));

    /// <summary>The body of the three-block rollback of the seed of release 2.0 (<see cref="WithThreeBlockRollbackOfSeedB"/>).</summary>
    private static string ThreeBlockRollbackOfSeedB(EngineConfig config, string? secondBlock = null)
        => SqlDialect.JoinBlocks(config.DatabaseType,
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product1"),
            secondBlock ?? SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product2"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product3"));

    /// <summary>
    /// (a) A file that a CLI tool runs counts no committed block while the tool runs: the tool commits nothing the engine
    /// can count, so a run that ends while it runs leaves nothing to resume or to finalize. The completed file counts its
    /// one block, and a rollback through the tool leaves the up counter of the record alone. The tool is a shell that
    /// exits at once, so the test needs no database client (not on Windows).
    /// </summary>
    public static async Task VerifyCliToolFileCountsNoCommittedBlockAsync(ScenarioBuilder builder, EngineConfig config)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: every file runs through the tool; the first record update of the run marks the first file before its
        // tool starts, and the probe reads the record right after it
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithCliTool("shell", "/bin/sh", "-c \"exit 0\" shell {FilePath}", timeoutInSeconds: 60)
            .WithUseCliToolAlias("shell")
            .WithSerilogMinimumLevel("Debug")
            .BuildAsync();
        MigrationRecordDto? whileTheToolRuns = null;
        ctx.UseLogObserver(logEvent =>
        {
            if (whileTheToolRuns == null && IsRecordUpdate(logEvent))
            {
                whileTheToolRuns = ctx.GetMigrationRecord(CreateTableA);
            }
        });

        // Act
        var up = await ctx.MigrateUpAsync(Release2);

        // Assert
        up.Success.Should().BeTrue(because: $"the tool succeeds for every file: {up.ErrorMessage}");
        whileTheToolRuns.Should().NotBeNull(because: "the run marks the record of the file before the tool starts");
        whileTheToolRuns!.MigrationStatusId.Should().Be((int)MigrationStatus.Executing);
        whileTheToolRuns.FileUpBlocksMigrated.Should().Be(0, because: "a tool commits nothing the engine can count");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 1
        });

        // Act: migrate-down rolls release 2.0 back through the tool
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"the tool succeeds for every rollback file: {down.ErrorMessage}");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileUpBlocksMigrated = 1,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 1
        });
    }

    /// <summary>
    /// (b) With MigrationErrorAction=Ignore a failed block ends the count: block 2 of the seed fails and the file goes on
    /// with block 3, but the record keeps counting 1 block, because block 3 committed after a gap, and the file that ran
    /// to its end stores that block with Failed, so that the next migrate-up resumes at the failed block (#11).
    /// </summary>
    public static async Task VerifyIgnoredBlockFailureEndsTheCountAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        var counted = new List<(int Block, int Counted)>();
        ctx.UseProgressObserver(new ProbingProgressObserver(e =>
        {
            if (e is BlockCompletedEvent { FileName: Seed, Operation: MigrationOperation.MigrateUp } block)
            {
                counted.Add((block.BlockNumber, ctx.GetMigrationRecord(Seed)!.FileUpBlocksMigrated));
            }
        }));

        // Act
        var result = await ctx.MigrateUpAsync(Release1);

        // Assert
        result.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the run went on past the failed block");
        counted.Should().Equal([(1, 1), (2, 1), (3, 1)],
            because: "block 1 committed, block 2 failed, and block 3 committed after the gap, so one leading block counts");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
    }

    /// <summary>
    /// (c) A cancel with MigrationErrorAction=Ignore keeps the leading blocks that committed, as a run that ends abruptly
    /// does: block 2 of the seed fails for good, and the run is cancelled before block 3. The record is Failed with block 1,
    /// the pending list names the seed Resumable after block 1, and the next migrate-up resumes with block 2: it fails
    /// again, block 3 runs again, and the file that ran to its end keeps block 1 as well.
    /// </summary>
    public static async Task VerifyCancelAfterAnIgnoredBlockFailureKeepsTheLeadingBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 2, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));

        // Act
        var cancelled = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);

        // Act: the pending list
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, "Backend", "MainDB", Seed, 3, PendingMigrationReason.Resumable, 1),
            because: "the next migrate-up resumes at the failed block");

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var next = await ctx.MigrateUpAsync(Release1);

        // Assert
        next.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "block 2 fails again and is ignored again");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
    }

    /// <summary>
    /// (d) With MigrationErrorAction=Ignore a file that ran to its end with a failed block resumes at that block: block 2
    /// of the seed inserts into a table that does not exist yet and fails, block 3 runs, and the record ends Failed with
    /// block 1, the blocks before the failed one; the pending list names the seed Resumable after block 1. Once the
    /// table exists, the next migrate-up resumes with block 2 and does not run block 1 again; block 3 runs again.
    /// </summary>
    public static async Task VerifyIgnoredFailureResumesAtTheFailedBlockAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 2 inserts into a table that the test creates only before the next run
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config,
                secondBlock: SqlDialect.GetInsertIntoSimpleTableSql(config.DatabaseType, Probe, "from block 2"))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        DropProbe(ctx, config);
        try
        {
            // Act
            var first = await ctx.MigrateUpAsync(Release1);

            // Assert
            first.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the run went on past the failed block");
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Failed,
                FileUpBlocksMigrated = 1,
                FileUpBlocksTotal = 3
            });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);

            // Act: the pending list
            await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
            var status = await ctx.InfoAsync();

            // Assert
            status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
                new PendingMigrationInfo(Release1, "Backend", "MainDB", Seed, 3, PendingMigrationReason.Resumable, 1),
                because: "the next migrate-up resumes at the failed block");

            // Act: the next migrate-up, with the table of block 2 created
            ctx.ExecuteOnConnection(config.ConnectionString2!, SqlDialect.GetCreateSimpleTableSql(config.DatabaseType, Probe));
            await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
            var resume = new RecordingProgressObserver();
            ctx.UseProgressObserver(resume);
            var next = await ctx.MigrateUpAsync(Release1);

            // Assert
            next.Success.Should().BeTrue(because: $"block 2 finds its table now: {next.ErrorMessage}");
            resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(1);
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Migrated,
                FileUpBlocksMigrated = 3,
                FileUpBlocksTotal = 3
            });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, Probe, 1);
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        }
        finally
        {
            DropProbe(ctx, config);
        }
    }

    /// <summary>
    /// (e) With MigrationErrorAction=Ignore a failed first block leaves no block to resume after: block 1 of the seed fails,
    /// blocks 2 and 3 run, and the record ends Failed with no block, so the pending list names the seed Failed and the
    /// next migrate-up runs the whole file.
    /// </summary>
    public static async Task VerifyIgnoredFailureOfTheFirstBlockRunsTheWholeFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 1 inserts into a table that the test creates only before the next run
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config,
                firstBlock: SqlDialect.GetInsertIntoSimpleTableSql(config.DatabaseType, Probe, "from block 1"))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        DropProbe(ctx, config);
        try
        {
            // Act
            var first = await ctx.MigrateUpAsync(Release1);

            // Assert
            first.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the run went on past the failed block");
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Failed,
                FileUpBlocksMigrated = 0,
                FileUpBlocksTotal = 3
            });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);

            // Act: the pending list
            await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
            var status = await ctx.InfoAsync();

            // Assert
            status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
                new PendingMigrationInfo(Release1, "Backend", "MainDB", Seed, 3, PendingMigrationReason.Failed, null),
                because: "no block committed before the failed one");

            // Act: the next migrate-up, with the table of block 1 created
            ctx.ExecuteOnConnection(config.ConnectionString2!, SqlDialect.GetCreateSimpleTableSql(config.DatabaseType, Probe));
            await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
            var rerun = new RecordingProgressObserver();
            ctx.UseProgressObserver(rerun);
            var next = await ctx.MigrateUpAsync(Release1);

            // Assert
            next.Success.Should().BeTrue(because: $"block 1 finds its table now: {next.ErrorMessage}");
            rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(0);
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Migrated,
                FileUpBlocksMigrated = 3,
                FileUpBlocksTotal = 3
            });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, Probe, 1);
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 4);
        }
        finally
        {
            DropProbe(ctx, config);
        }
    }

    /// <summary>
    /// (f) With RollbackErrorAction=Ignore a failed rollback block ends the count the same way: rollback block 2 of the seed
    /// of release 2.0 fails and block 3 runs, but the record keeps 1 rollback block until the rollback ends; the completed
    /// rollback stores its block count with Failed.
    /// </summary>
    public static async Task VerifyIgnoredRollbackBlockFailureEndsTheCountAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var counted = new List<(int Block, int? Counted)>();
        ctx.UseProgressObserver(new ProbingProgressObserver(e =>
        {
            if (e is BlockCompletedEvent { FileName: SeedB, Operation: MigrationOperation.MigrateDown } block)
            {
                counted.Add((block.BlockNumber, ctx.GetMigrationRecord(SeedB)!.FileDownBlocksMigrated));
            }
        }));

        // Act
        await ctx.MigrateDownAsync(Release1);

        // Assert
        counted.Should().Equal([(1, 1), (2, 1), (3, 1)],
            because: "rollback block 1 ran, block 2 failed, and block 3 ran after the gap, so one leading rollback block counts");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 3,
            FileDownBlocksTotal = 3
        });
    }

    /// <summary>
    /// (g) A resumed file carries its committed blocks from the start of the run: the record insert of the next attempt
    /// resets the counter, and the run writes the blocks it resumes after right away, before its first block, so that a
    /// run that ends before that block commits leaves them on the record. A cancel after block 1 leaves the seed Failed
    /// with block 1; the next run reads the record when the seed starts and is cancelled there, before block 2; the run
    /// after it resumes with block 2. <paramref name="order"/> TargetByTarget covers the other file loop.
    /// </summary>
    public static async Task VerifyResumedFileCountsItsBlocksFromTheStartAsync(ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order)
    {
        // Arrange: the first run commits block 1 of the seed and is cancelled
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithTargetMigrationOrder(order)
            .BuildAsync();
        using var first = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => first.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: first)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 1 });

        // Act: the second run resumes the seed and is cancelled when the seed starts
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        using var second = new RunCancellation();
        MigrationRecordDto? atTheStart = null;
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(Seed, MigrationOperation.MigrateUp),
            () =>
            {
                atTheStart = ctx.GetMigrationRecord(Seed);
                second.Cancel(MigrationCancelledAction.Terminate, Reason);
            }));
        var cancelled = await ctx.MigrateUpAsync(Release1, cancellation: second);

        // Assert
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled);
        atTheStart.Should().NotBeNull();
        atTheStart!.MigrationStatusId.Should().Be((int)MigrationStatus.Executing,
            because: "the run marks the resumed record before its first block");
        atTheStart.FileUpBlocksMigrated.Should().Be(1, because: "block 1 committed in the first run, and the record says so from the start");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 1 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);

        // Act: the third run resumes with block 2
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var third = await ctx.MigrateUpAsync(Release1);

        // Assert
        third.Success.Should().BeTrue(because: $"the resumed run finishes the seed: {third.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>
    /// (h) A continued rollback carries its rollback blocks from its start: a migrate-down cancelled after rollback block 1
    /// of 3 leaves the seed of release 2.0 Failed with 1 rollback block; the next migrate-down marks the record with that
    /// block when it starts the rollback again, before block 2 runs, so that a migrate-down that ends there still leaves a
    /// rollback that migrate-up refuses and migrate-down continues. A cancel there keeps 1 rollback block, and the
    /// migrate-down after it completes the rollback.
    /// </summary>
    public static async Task VerifyContinuedRollbackCountsItsBlocksFromTheStartAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated, then a migrate-down cancelled after rollback block 1 of the seed
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var first = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => first.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateDownAsync(Release1, cancellation: first)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });

        // Act: the next migrate-down continues the rollback and is cancelled when the seed starts
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var second = new RunCancellation();
        MigrationRecordDto? atTheStart = null;
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(SeedB, MigrationOperation.MigrateDown),
            () =>
            {
                atTheStart = ctx.GetMigrationRecord(SeedB);
                second.Cancel(MigrationCancelledAction.Terminate, Reason);
            }));
        var cancelled = await ctx.MigrateDownAsync(Release1, cancellation: second);

        // Assert
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled);
        atTheStart.Should().NotBeNull();
        atTheStart!.MigrationStatusId.Should().Be((int)MigrationStatus.Executing,
            because: "the migrate-down marks the record before the next rollback block");
        atTheStart.FileDownBlocksMigrated.Should().Be(1, because: "rollback block 1 ran in the first migrate-down, and the record says so from the start");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: the migrate-down after it completes the rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var third = await ctx.MigrateDownAsync(Release1);

        // Assert
        third.Success.Should().BeTrue(because: $"the rollback continues: {third.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertFileStatus(SeedB, MigrationStatus.NotMigrated);
    }

    /// <summary>
    /// (i) A cancel with RollbackErrorAction=Ignore keeps the leading rollback blocks that ran, as a migrate-down that ends
    /// abruptly does: rollback block 2 of the seed of release 2.0 fails, and the migrate-down is cancelled before block 3.
    /// The record is Failed with rollback block 1, so the next migrate-down continues the rollback at the failed block 2
    /// instead of skipping it: block 2 fails again, block 3 runs, and the rollback that ran to its end stores its block
    /// count with Failed.
    /// </summary>
    public static async Task VerifyCancelAfterAnIgnoredRollbackBlockFailureKeepsTheLeadingRollbackBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 2, MigrationOperation.MigrateDown),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));

        // Act
        var cancelled = await ctx.MigrateDownAsync(Release1, cancellation: cancellation);

        // Assert
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: the next migrate-down continues the rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var next = await ctx.MigrateDownAsync(Release1);

        // Assert
        next.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "rollback block 2 fails again and is ignored again");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(1);
        resume.Of<BlockCompletedEvent>().Where(b => b.FileName == SeedB).Select(b => (b.BlockNumber, b.Success))
            .Should().Equal([(2, false), (3, true)], because: "the rollback continues at the failed block, not after it");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 3,
            FileDownBlocksTotal = 3
        });
    }

    /// <summary>
    /// (j) A failed atomic rollback keeps the rollback blocks of the earlier attempt it continued: the target shares the
    /// repository connection, and a first migrate-down, whose rollback file sets RollbackErrorAction=Ignore so that every
    /// rollback block commits on its own, is cancelled after rollback block 1 of the seed of release 2.0. Without that
    /// setting the next migrate-down takes the atomic path, and rollback block 2 fails there: the transaction takes back
    /// only what this attempt did, so the record stays Failed with rollback block 1, and migrate-up refuses the file until
    /// migrate-down completes the rollback.
    /// </summary>
    public static async Task VerifyFailedAtomicRollbackKeepsTheRollbackBlocksOfTheEarlierAttemptAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated on the repository connection, then a migrate-down cancelled after rollback block 1
        string rollback = ThreeBlockRollbackOfSeedB(config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType));
        const string IgnoreHeader = "/*\n[RayMigrator]\nRollbackErrorAction = \"Ignore\"\n*/\n\n";
        await using var ctx = await builder
            .WithFileBytes($"{Release2}/Backend/{SeedBRollback}", Encoding.UTF8.GetBytes(IgnoreHeader + rollback + "\n"))
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateDownAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        var halfRolledBack = new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        };
        ctx.AssertMigrationRecord(SeedB, halfRolledBack);

        // Act: the next migrate-down runs the rollback with Terminate, the default, on the atomic path
        File.WriteAllText(Path.Combine(ctx.WorkDirectory, Release2, "Backend", SeedBRollback), rollback + "\n");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        ctx.UseProgressObserver(new RecordingProgressObserver());
        var failed = await ctx.MigrateDownAsync(Release1);

        // Assert
        failed.Result.Should().Be(MigrationRunResult.Error, because: "rollback block 2 fails and Terminate aborts the chain");
        failed.ErrorMessage.Should().Contain($"atomic rollback of {SeedB} failed", because: "the rollback ran in one transaction with its record");
        ctx.AssertMigrationRecord(SeedB, halfRolledBack);
        ctx.AssertRowCount("tablec", 2);

        // Act: migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var refused = await ctx.MigrateUpAsync(Release2);

        // Assert
        refused.Success.Should().BeFalse(because: "running the seed again from block 1 would duplicate the rows the rollback kept");
        refused.ErrorMessage.Should().StartWith("Incomplete rollback: 1 file(s)")
            .And.Contain($"{Release2}/{SeedB} on target MainDB: 1 of 3 rollback blocks, migrate-down continues at block 2");
        ctx.AssertRowCount("tablec", 2);
    }

    /// <summary>
    /// (k) A rollback through a CLI tool that fails ends like a failed rollback block: with RollbackErrorAction=Terminate the
    /// record is Failed with the rollback blocks that ran before the tool (none, the tool counts none), and the chain
    /// aborts. The record does not stay Executing in a run that has ended. The tool is a shell that fails for a rollback
    /// file and succeeds for every other file, so the test needs no database client (not on Windows).
    /// </summary>
    public static async Task VerifyFailedRollbackThroughACliToolEndsFailedAsync(ScenarioBuilder builder, EngineConfig config)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: release 2.0 migrated through the tool
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithCliTool("shell", "/bin/sh", "-c \"case $1 in *.rollback.*) exit 3;; esac\" shell {FilePath}", timeoutInSeconds: 60)
            .WithUseCliToolAlias("shell")
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"the tool succeeds for every migration file: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);

        // Act
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Error, because: "the tool fails for the first rollback file and Terminate aborts the chain");
        down.ErrorMessage.Should().Contain("ExitCode=3");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileDownBlocksMigrated = 0,
            FileDownBlocksTotal = 1
        });
        ctx.AssertFileStatuses((CreateTableD, MigrationStatus.Migrated), (CreateTableC, MigrationStatus.Migrated));
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });
    }

    /// <summary>
    /// (l) A file that a CLI tool runs does not resume the record of an earlier attempt: the tool runs the whole file, so the
    /// committed blocks of that record are no blocks it skips. A migrate-up on the DAL is cancelled after block 1 of the seed
    /// (Failed with 1 of 3), then every target gets the tool, and the next migrate-up starts the seed at its first block.
    /// The tool is a shell that exits at once (not on Windows). <paramref name="order"/> TargetByTarget covers the other
    /// file loop.
    /// </summary>
    public static async Task VerifyCliToolFileDoesNotResumeAnEarlierAttemptAsync(ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: block 1 of the seed committed on the DAL, then every target gets the tool
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithTargetMigrationOrder(order)
            .WithCliTool("shell", "/bin/sh", "-c \"exit 0\" shell {FilePath}", timeoutInSeconds: 60)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 1, FileUpBlocksTotal = 3 });
        UseCliToolOnEveryTarget(ctx, "shell");
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var afterCommittedBlocks = new RecordingProgressObserver();
        ctx.UseProgressObserver(afterCommittedBlocks);

        // Act
        var second = await ctx.MigrateUpAsync(Release1);

        // Assert
        second.Success.Should().BeTrue(because: $"the tool runs the seed: {second.ErrorMessage}");
        afterCommittedBlocks.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "the tool runs the whole file and skips no block");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 1, FileUpBlocksTotal = 1 });
    }

    /// <summary>
    /// (m) A rollback that a CLI tool runs does not continue the rollback of an earlier attempt: the tool runs the whole
    /// rollback file, so the rollback blocks of that attempt are no blocks it skips. A migrate-down on the DAL is cancelled
    /// after rollback block 1 of the seed of release 2.0 (Failed with 1 of 3 rollback blocks), then every target gets the
    /// tool, and the next migrate-down starts the rollback of the seed at its first block. The tool is a shell that exits
    /// at once (not on Windows).
    /// </summary>
    public static async Task VerifyRollbackThroughACliToolDoesNotContinueAnEarlierAttemptAsync(ScenarioBuilder builder, EngineConfig config)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: rollback block 1 of the seed ran on the DAL in a cancelled migrate-down, then every target gets the tool
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithCliTool("shell", "/bin/sh", "-c \"exit 0\" shell {FilePath}", timeoutInSeconds: 60)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateDownAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        UseCliToolOnEveryTarget(ctx, "shell");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var afterRollbackBlocks = new RecordingProgressObserver();
        ctx.UseProgressObserver(afterRollbackBlocks);

        // Act
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"the tool runs the rollback files: {down.ErrorMessage}");
        afterRollbackBlocks.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB)
            .Which.ResumeFromBlock.Should().Be(0, because: "the tool runs the whole rollback file and skips no rollback block");
        ctx.AssertFileStatus(SeedB, MigrationStatus.NotMigrated);
    }

    /// <summary>
    /// (n) A rollback whose progress write fails after a rollback block ran counts that block: with
    /// RollbackErrorAction=Terminate the record is Failed with the rollback blocks that ran, the one whose write failed
    /// included, so that the next migrate-down continues after it instead of running it again. The repository refuses the
    /// write that counts rollback block 2 of the seed of release 2.0 (a check constraint, on SQLite a trigger).
    /// </summary>
    public static async Task VerifyFailedRollbackProgressWriteCountsTheBlockThatRanAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated on the second database, then the repository refuses the progress write of rollback block 2
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        RefuseRollbackProgressWrite(ctx, config, rollbackBlocks: 2);

        // Act
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Error, because: "the progress write failed and Terminate aborts the chain");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 2,
            FileDownBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 1);

        // Act: the repository takes the write again, and the next migrate-down continues the rollback
        AllowRollbackProgressWrite(ctx, config);
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var next = await ctx.MigrateDownAsync(Release1);

        // Assert
        next.Success.Should().BeTrue(because: $"migrate-down continues the rollback: {next.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB)
            .Which.ResumeFromBlock.Should().Be(2, because: "rollback block 2 ran before its progress write failed and does not run again");
        ctx.AssertFileStatuses((SeedB, MigrationStatus.NotMigrated), (CreateTableD, MigrationStatus.NotMigrated), (CreateTableC, MigrationStatus.NotMigrated));
    }

    /// <summary>
    /// (o) A rollback that a CLI tool runs reports one block, as a file that the tool runs on the way up, also where the
    /// tool runs on one target of the group only and the rollback file is split into its blocks for the other: MainDB
    /// rolls the seed of release 2.0 back through the tool, SecondDB in its three rollback blocks on the DAL, and the next
    /// migrate-up runs the seed alike. The tool is a shell that exits at once (not on Windows).
    /// </summary>
    public static async Task VerifyRollbackThroughACliToolReportsOneBlockAsync(ScenarioBuilder builder, EngineConfig config)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: release 2.0 migrated on both targets, then MainDB gets the tool
        await using var ctx = await WithThreeBlockRollbackOfSeedB(builder, config)
            .WithMultiTarget(config.ConnectionString2!)
            .WithCliTool("shell", "/bin/sh", "-c \"exit 0\" shell {FilePath}", timeoutInSeconds: 60)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"release 2.0 migrates on both targets: {up.ErrorMessage}");
        UseCliToolOnTarget(ctx, "MainDB", "shell");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var rollback = new RecordingProgressObserver();
        ctx.UseProgressObserver(rollback);

        // Act
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"the tool and the DAL roll the release back: {down.ErrorMessage}");
        var rolledBack = rollback.Of<FileStartedEvent>().Where(f => f.FileName == SeedB).ToList();
        rolledBack.Should().ContainSingle(f => f.TargetAlias == "MainDB")
            .Which.BlocksTotal.Should().Be(1, because: "the tool runs the rollback file as one unit");
        rolledBack.Should().ContainSingle(f => f.TargetAlias == "SecondDB")
            .Which.BlocksTotal.Should().Be(3, because: "the DAL runs the rollback file in its three blocks");

        // Act: migrate-up runs the seed through the tool on MainDB and on the DAL on SecondDB
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var migration = new RecordingProgressObserver();
        ctx.UseProgressObserver(migration);
        var again = await ctx.MigrateUpAsync(Release2);

        // Assert
        again.Success.Should().BeTrue(because: $"release 2.0 migrates again: {again.ErrorMessage}");
        migration.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB && f.TargetAlias == "MainDB")
            .Which.BlocksTotal.Should().Be(1, because: "a file that the tool runs reports one block on the way up as well");
    }

    /// <summary>
    /// (p) A rollback through a CLI tool that exits without reading the rollback file it gets on its standard input ends
    /// like a failed rollback block: the tool closes its input before the engine has written the file, and its exit code
    /// decides. With RollbackErrorAction=Terminate the record is Failed with the rollback blocks that ran before the tool
    /// (none) and the chain aborts; with Ignore the record is Failed with its rollback counted, and the chain goes on. The
    /// rollback file is larger than a pipe holds, and the tool is a shell that fails for it without reading it and reads
    /// every other file (not on Windows).
    /// </summary>
    public static async Task VerifyRollbackThroughACliToolThatClosesItsInputAsync(ScenarioBuilder builder, EngineConfig config, RollbackErrorAction action)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: release 2.0 migrated through the tool
        var rollbackOfSeedB = new StringBuilder();
        for (int i = 0; i < 20000; i++)
        {
            rollbackOfSeedB.Append("-- a line that makes the rollback file larger than the pipe buffer of the tool ").Append(i).Append('\n');
        }

        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithFileBytes($"{Release2}/Backend/{SeedBRollback}", Encoding.UTF8.GetBytes(rollbackOfSeedB.ToString()))
            .WithCliTool("shell", "/bin/sh", $"-c \"case $1 in *{SeedBRollback}) exit 3;; esac; cat > /dev/null\" shell {{FilePath}}",
                inputMode: "Stdin", timeoutInSeconds: 60)
            .WithUseCliToolAlias("shell")
            .WithRollbackErrorAction(action)
            .BuildAsync();
        var up = await ctx.MigrateUpAsync(Release2);
        up.Success.Should().BeTrue(because: $"the tool reads every migration file and succeeds: {up.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);

        // Act
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        if (action == RollbackErrorAction.Ignore)
        {
            down.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the chain goes on after the failed rollback and rolls the other files back");
            ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Failed,
                FileDownBlocksMigrated = 1,
                FileDownBlocksTotal = 1
            });
            ctx.AssertFileStatuses((CreateTableD, MigrationStatus.NotMigrated), (CreateTableC, MigrationStatus.NotMigrated));
        }
        else
        {
            down.Result.Should().Be(MigrationRunResult.Error, because: "the tool fails for the rollback of the seed and Terminate aborts the chain");
            ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
            {
                MigrationStatusId = (int)MigrationStatus.Failed,
                FileDownBlocksMigrated = 0,
                FileDownBlocksTotal = 1
            });
            ctx.AssertFileStatuses((CreateTableD, MigrationStatus.Migrated), (CreateTableC, MigrationStatus.Migrated));
            down.ErrorMessage.Should().Contain("ExitCode=3", because: "the exit code of the tool decides, not the input it did not read");
        }
    }

    /// <summary>
    /// Makes the repository refuse the progress write of a rollback that has run <paramref name="rollbackBlocks"/> rollback
    /// blocks (Executing with that count), as a repository that fails at that moment, and take every other write: a check
    /// constraint on the record table, on SQLite a trigger. <see cref="AllowRollbackProgressWrite"/> takes it back.
    /// </summary>
    private static void RefuseRollbackProgressWrite(ScenarioContext ctx, EngineConfig config, int rollbackBlocks)
    {
        var naming = new RepositoryNaming(config.DatabaseType, ctx.RepositoryOptions.SchemaName, ctx.RepositoryOptions.TableBaseName);
        string table = naming.QualifiedTableName(RepositoryTable.MigrationRecord);
        string status = naming.QualifiedColumnName("MigrationStatusId");
        string rollbackBlocksDone = naming.QualifiedColumnName("FileDownBlocksMigrated");
        string sql = config.DatabaseType == "Sqlite"
            ? $"CREATE TRIGGER {Refusal} BEFORE UPDATE ON {table} WHEN NEW.{status} = 20 AND NEW.{rollbackBlocksDone} = {rollbackBlocks} " +
              "BEGIN SELECT RAISE(ABORT, 'refused by the test'); END"
            : $"ALTER TABLE {table} ADD CONSTRAINT {Refusal} CHECK (NOT ({status} = 20 AND {rollbackBlocksDone} = {rollbackBlocks}))";
        ctx.ExecuteOnConnection(ctx.RepositoryOptions.ConnectionString!, sql);
    }

    /// <summary>Takes back what <see cref="RefuseRollbackProgressWrite"/> did, so that the repository takes every write again.</summary>
    private static void AllowRollbackProgressWrite(ScenarioContext ctx, EngineConfig config)
    {
        var naming = new RepositoryNaming(config.DatabaseType, ctx.RepositoryOptions.SchemaName, ctx.RepositoryOptions.TableBaseName);
        string sql = config.DatabaseType == "Sqlite"
            ? $"DROP TRIGGER {Refusal}"
            : $"ALTER TABLE {naming.QualifiedTableName(RepositoryTable.MigrationRecord)} DROP CONSTRAINT {Refusal}";
        ctx.ExecuteOnConnection(ctx.RepositoryOptions.ConnectionString!, sql);
    }

    /// <summary>
    /// Gives the target <paramref name="targetAlias"/> the CLI tool <paramref name="alias"/> from the next host on, while the
    /// other targets of its group keep the DAL: UseCliToolAlias on that target in the configuration file of the scenario,
    /// which <see cref="ScenarioContext.RebuildForAsync"/> reads again.
    /// </summary>
    private static void UseCliToolOnTarget(ScenarioContext ctx, string targetAlias, string alias)
    {
        string configPath = Path.Combine(ctx.WorkDirectory, "appsettings.json");
        var configuration = JsonNode.Parse(File.ReadAllText(configPath))!;
        var target = configuration["RayMigrator"]!["Products"]!.AsArray()
            .SelectMany(product => product!["TargetGroups"]!.AsArray())
            .SelectMany(group => group!["Targets"]!.AsArray())
            .Single(t => (string?)t!["Alias"] == targetAlias)!;
        target["UseCliToolAlias"] = alias;
        File.WriteAllText(configPath, configuration.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Gives every target of the scenario the CLI tool <paramref name="alias"/> from the next host on, as an operator who
    /// configures a tool after a run on the DAL: ProductDefaults.UseCliToolAlias in the configuration file of the scenario,
    /// which <see cref="ScenarioContext.RebuildForAsync"/> reads again.
    /// </summary>
    internal static void UseCliToolOnEveryTarget(ScenarioContext ctx, string alias)
    {
        string configPath = Path.Combine(ctx.WorkDirectory, "appsettings.json");
        var configuration = JsonNode.Parse(File.ReadAllText(configPath))!;
        configuration["RayMigrator"]!["ProductDefaults"]!["UseCliToolAlias"] = alias;
        File.WriteAllText(configPath, configuration.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
