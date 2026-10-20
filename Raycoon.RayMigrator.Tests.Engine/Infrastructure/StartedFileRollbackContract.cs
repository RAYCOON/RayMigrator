using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The rollback of a file that stopped after committed blocks, which every engine performs on migrate-down: a record
/// Failed, or Executing in a run that has ended, with some or every block committed and no rollback attempt, as a failed,
/// cancelled or abruptly ended migrate-up leaves it, is rolled back like a Migrated record when its release is after the
/// target release. Its rollback file runs from its first rollback block, in the order of the chain, so before the files it
/// came after, and the record ends NotMigrated, so that the next migrate-up runs the file from its first block instead of
/// resuming it after blocks whose effect the rollback of the release took away. A record without a committed block is
/// not rolled back, and migrate-down names it in a warning. A rollback that fails on such a file stops in the middle like
/// any other, and a missing rollback file leaves its record with its committed blocks. The seed of release 1.0 runs in
/// three blocks of one row each on the second database, where every block commits on its own; migrate-down rolls back to
/// before every release.
/// </summary>
internal static class StartedFileRollbackContract
{
    private const string Release1 = "Release_1.0";
    private const string MainDb = "MainDB";
    private const string CreateTableA = "01_CreateTableA.sql";
    private const string CreateTableB = "02_CreateTableB.sql";
    private const string Seed = "03_SeedDataA.sql";
    private const string SeedRollback = "03_SeedDataA.rollback.sql";
    private const string Reason = "stopped by the test";

    /// <summary>The release migrate-down rolls back to: before every release, so that it rolls back all of release 1.0.</summary>
    private const string BeforeEveryRelease = "";

    /// <summary>The table a block of the seed inserts into before a test creates it, on the second database.</summary>
    private const string Probe = "rollbackprobe";

    /// <summary>The three-block seed of release 1.0 on the second database; <paramref name="secondBlock"/> replaces block 2.</summary>
    private static ScenarioBuilder SeedOnTheSecondDatabase(ScenarioBuilder builder, EngineConfig config, string? secondBlock = null)
        => CancellationContract.WithThreeBlockSeed(builder, config, secondBlock).WithTargetConnectionString(config.ConnectionString2!);

    /// <summary>The rollback of the seed in three blocks, one row each; <paramref name="secondBlock"/> replaces block 2.</summary>
    private static string ThreeBlockRollbackOfTheSeed(EngineConfig config, string? secondBlock = null)
        => SqlDialect.JoinBlocks(config.DatabaseType,
            SqlDialect.GetDeleteFromTableASql(config.DatabaseType, "alpha"),
            secondBlock ?? SqlDialect.GetDeleteFromTableASql(config.DatabaseType, "beta"),
            SqlDialect.GetDeleteFromTableASql(config.DatabaseType, "gamma"));

    /// <summary>
    /// Runs release 1.0 and cancels it with Terminate after block 1 of the seed: the seed is Failed with block 1 and without
    /// a rollback attempt, and the row of block 1 is on the target.
    /// </summary>
    private static async Task StopTheSeedAfterItsFirstBlockAsync(ScenarioContext ctx, EngineConfig config)
    {
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        AssertStoppedAfterOneBlock(ctx);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
    }

    /// <summary>Asserts that the seed is Failed with block 1 of 3 committed and without a rollback attempt.</summary>
    private static void AssertStoppedAfterOneBlock(ScenarioContext ctx)
    {
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.GetMigrationRecord(Seed)!.FileDownBlocksTotal.Should().BeNull(because: "no rollback of the seed ran");
    }

    /// <summary>The files a migrate-down rolled back, skipped or failed to roll back, in the order of its chain.</summary>
    private static List<string> RollbackChain(RecordingProgressObserver observer)
        => observer.Of<FileCompletedEvent>().Where(f => f.Operation == MigrationOperation.MigrateDown).Select(f => f.FileName).ToList();

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

    /// <summary>
    /// (a) A cancel with Terminate after block 1 leaves the seed Failed with 1 committed block, and the pending list names it
    /// Resumable after block 1. migrate-down to before every release rolls the seed back first, from its first rollback
    /// block, then the two files before it, and the seed ends NotMigrated with every rollback block, so that the pending
    /// list names it NotMigrated and the next migrate-up runs it from its first block: the table has all three rows again.
    /// Without the rollback of the seed, migrate-up would create the table anew and resume the seed with block 2.
    /// </summary>
    public static async Task VerifyCancelledFileIsRolledBackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await StopTheSeedAfterItsFirstBlockAsync(ctx, config);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        (await ctx.InfoAsync()).PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, "Backend", MainDb, Seed, 3, PendingMigrationReason.Resumable, 1),
            because: "the next migrate-up would resume the seed after its committed block");
        var rollback = new RecordingProgressObserver();
        ctx.UseProgressObserver(rollback);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Ok, because: $"every file of the release is rolled back: {down.ErrorMessage}");
        RollbackChain(rollback).Should().Equal([Seed, CreateTableB, CreateTableA],
            because: "the seed stopped after its committed block, and the chain rolls it back before the files it came after");
        rollback.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.MigrateDown)
            .Which.ResumeFromBlock.Should().Be(0, because: "the rollback of the seed starts at its first rollback block");
        rollback.Of<BlockCompletedEvent>().Should().ContainSingle(b => b.FileName == Seed && b.Operation == MigrationOperation.MigrateDown)
            .Which.Success.Should().BeTrue(because: "the rollback file of the seed ran");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 1
        });
        ctx.AssertFileStatuses((CreateTableB, MigrationStatus.NotMigrated), (CreateTableA, MigrationStatus.NotMigrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);

        // Act: the pending list
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, "Backend", MainDb, Seed, 3, PendingMigrationReason.NotMigrated, null),
            because: "the rolled back seed runs from its first block");

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var rerun = new RecordingProgressObserver();
        ctx.UseProgressObserver(rerun);
        var up = await ctx.MigrateUpAsync(Release1);

        // Assert
        up.Success.Should().BeTrue(because: $"the release runs again: {up.ErrorMessage}");
        rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "a rolled back file is never resumed");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 3,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>
    /// (b) A failure with MigrationErrorAction=Terminate after a committed block: block 2 of the seed inserts into a table
    /// that does not exist yet, so the file keeps its hash, and the seed is Failed with block 1. migrate-down rolls it back,
    /// and once the table exists, the next migrate-up runs the whole seed: block 1 runs again on the table that 01 created
    /// anew, and the table has the rows of blocks 1 and 3.
    /// </summary>
    public static async Task VerifyFailedFileIsRolledBackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 2 inserts into a table that the test creates only before the last run
        await using var ctx = await SeedOnTheSecondDatabase(builder, config,
                secondBlock: SqlDialect.GetInsertIntoSimpleTableSql(config.DatabaseType, Probe, "from block 2"))
            .BuildAsync();
        DropProbe(ctx, config);
        try
        {
            var failed = await ctx.MigrateUpAsync(Release1);
            failed.Result.Should().Be(MigrationRunResult.Error, because: "block 2 of the seed fails and Terminate stops the run");
            AssertStoppedAfterOneBlock(ctx);
            var rollback = new RecordingProgressObserver();
            ctx.UseProgressObserver(rollback);

            // Act
            var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

            // Assert
            down.Result.Should().Be(MigrationRunResult.Ok, because: $"every file of the release is rolled back: {down.ErrorMessage}");
            RollbackChain(rollback).Should().Equal([Seed, CreateTableB, CreateTableA]);
            ctx.AssertFileStatuses(
                (Seed, MigrationStatus.NotMigrated),
                (CreateTableB, MigrationStatus.NotMigrated),
                (CreateTableA, MigrationStatus.NotMigrated));

            // Act: the next migrate-up, with the table of block 2 created
            ctx.ExecuteOnConnection(config.ConnectionString2!, SqlDialect.GetCreateSimpleTableSql(config.DatabaseType, Probe));
            await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
            var rerun = new RecordingProgressObserver();
            ctx.UseProgressObserver(rerun);
            var up = await ctx.MigrateUpAsync(Release1);

            // Assert
            up.Success.Should().BeTrue(because: $"block 2 finds its table now: {up.ErrorMessage}");
            rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(0);
            ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, Probe, 1);
        }
        finally
        {
            DropProbe(ctx, config);
        }
    }

    /// <summary>
    /// (c) With MigrationErrorAction=Ignore a file that ran to its end with a failed block 2 is Failed with block 1, although
    /// block 3 committed after the gap. migrate-down rolls the whole file back, the row of block 3 included, and the next
    /// migrate-up runs it from its first block: block 2 fails again and is ignored again.
    /// </summary>
    public static async Task VerifyFileWithAnIgnoredFailureIsRolledBackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        var first = await ctx.MigrateUpAsync(Release1);
        first.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the run went on past the failed block");
        AssertStoppedAfterOneBlock(ctx);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
        int? rowsAfterTheRollbackOfTheSeed = null;
        var rollback = new ProbingProgressObserver(e =>
        {
            if (e is FileCompletedEvent { FileName: Seed, Operation: MigrationOperation.MigrateDown })
            {
                rowsAfterTheRollbackOfTheSeed = ctx.CountRowsOnConnection(config.ConnectionString2!, "tablea");
            }
        });
        ctx.UseProgressObserver(rollback);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Ok, because: $"every file of the release is rolled back: {down.ErrorMessage}");
        rollback.Of<FileCompletedEvent>().Where(f => f.Operation == MigrationOperation.MigrateDown).Select(f => f.FileName)
            .Should().Equal([Seed, CreateTableB, CreateTableA]);
        rowsAfterTheRollbackOfTheSeed.Should().Be(0, because: "the rollback of the seed takes back the rows of block 1 and of block 3");
        ctx.AssertFileStatus(Seed, MigrationStatus.NotMigrated);

        // Act: the next migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var rerun = new RecordingProgressObserver();
        ctx.UseProgressObserver(rerun);
        var up = await ctx.MigrateUpAsync(Release1);

        // Assert
        up.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "block 2 fails again and is ignored again");
        rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "the rolled back seed runs from its first block");
        AssertStoppedAfterOneBlock(ctx);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
    }

    /// <summary>
    /// (d) A run that ended abruptly while block 2 of the seed ran leaves its run open and the seed Executing with block 1.
    /// The auto-fix of migrate-down closes that run as orphaned and keeps the progress of the seed (Failed with block 1),
    /// and migrate-down then rolls the seed back with the files before it.
    /// </summary>
    public static async Task VerifyFileKeptByTheOrphanRepairIsRolledBackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 1 committed, then the footprint of an abrupt end in the middle of the seed
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await StopTheSeedAfterItsFirstBlockAsync(ctx, config);
        int orphanRunId = OrphanRepairContract.InsertOrphanRun(ctx);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1, migrationRunId: orphanRunId);
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, BeforeEveryRelease);
        var rollback = new RecordingProgressObserver();
        ctx.UseProgressObserver(rollback);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Ok, because: $"the repaired seed and the files before it are rolled back: {down.ErrorMessage}");
        var orphan = ctx.GetMigrationRun(2)!;
        orphan.Id.Should().Be(orphanRunId);
        orphan.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error, because: "the auto-fix closes the orphaned run");
        orphan.ErrorCode.Should().Be(TemplateResultCode.OrphanedRunClosed.ToString(CultureInfo.InvariantCulture));
        RollbackChain(rollback).Should().Equal([Seed, CreateTableB, CreateTableA],
            because: "the repair keeps block 1 of the seed, so migrate-down rolls the seed back");
        ctx.AssertFileStatuses(
            (Seed, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (CreateTableA, MigrationStatus.NotMigrated));
        ctx.AssertMigrationRun(3, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
    }

    /// <summary>
    /// (e) A file without a committed block is not rolled back: on the atomic path (the target shares the repository
    /// connection) a cancel when the seed starts commits nothing of it, and the seed is Failed with no block. migrate-down
    /// rolls back the files before it, leaves the seed as it is and names it in a warning with its release, target and
    /// record, because with MigrationErrorAction=Ignore or a CLI tool blocks of such a file may have run; the warning does
    /// not make the run PartialSuccess.
    /// </summary>
    public static async Task VerifyFileWithoutACommittedBlockIsNamedAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(Seed, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, Reason)));
        (await ctx.MigrateUpAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 0 });
        int seedRecordId = ctx.GetMigrationRecord(Seed)!.Id;
        var warnings = CollectWarnings(ctx);
        var rollback = new RecordingProgressObserver();
        ctx.UseProgressObserver(rollback);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Ok, because: $"the files with committed blocks are rolled back, and the warning is no failure: {down.ErrorMessage}");
        ctx.AssertRunResult(MigrationRunResult.Ok);
        RollbackChain(rollback).Should().Equal([CreateTableB, CreateTableA], because: "the record of the seed counts no committed block");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 0 });
        ctx.GetMigrationRecord(Seed)!.FileDownBlocksTotal.Should().BeNull(because: "no rollback of the seed ran");
        warnings.Should().ContainSingle(w => w.StartsWith("File not rolled back", StringComparison.Ordinal) && w.Contains(Seed),
                because: "migrate-down names the file it does not roll back")
            .Which.Should().Contain(Release1).And.Contain(MainDb).And.Contain($"MigrationRecordId: {seedRecordId}")
            .And.Contain("its record counts no committed block; with MigrationErrorAction=Ignore or a CLI tool, blocks of the file may have run");
    }

    /// <summary>
    /// (f) A rollback of the file that stopped after its committed block fails like any other rollback: rollback block 2 of
    /// the seed fails, and with RollbackErrorAction=Terminate the chain aborts there. The seed is Failed with rollback block
    /// 1, the files before it stay Migrated, the pending list names the seed RollbackIncomplete, and migrate-up refuses to
    /// run. Once the rollback file is repaired, migrate-down continues the rollback of the seed with block 2 and rolls the
    /// files before it back.
    /// </summary>
    public static async Task VerifyFailedRollbackOfTheFileStopsInTheMiddleAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the seed rolls back in three blocks, the second of which fails
        await using var ctx = await SeedOnTheSecondDatabase(builder, config)
            .WithFileBytes($"{Release1}/Backend/{SeedRollback}",
                Encoding.UTF8.GetBytes(ThreeBlockRollbackOfTheSeed(config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType)) + "\n"))
            .BuildAsync();
        await StopTheSeedAfterItsFirstBlockAsync(ctx, config);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Error, because: "rollback block 2 of the seed fails and Terminate aborts the chain");
        down.ErrorMessage.Should().Contain($"block 2 of {Seed}");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        ctx.AssertFileStatuses((CreateTableB, MigrationStatus.Migrated), (CreateTableA, MigrationStatus.Migrated));
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 0);

        // Act: the pending list
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, "Backend", MainDb, Seed, 3, PendingMigrationReason.RollbackIncomplete, null, 1, 3));

        // Act: migrate-up
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var refused = await ctx.MigrateUpAsync(Release1);

        // Assert
        refused.Success.Should().BeFalse(because: "running the seed again from block 1 would act on a target that kept part of it");
        refused.ErrorMessage.Should().StartWith("Incomplete rollback: 1 file(s)")
            .And.Contain($"{Release1}/{Seed} on target {MainDb}: 1 of 3 rollback blocks, migrate-down continues at block 2");

        // Act: the rollback file is repaired, and migrate-down continues
        File.WriteAllText(Path.Combine(ctx.WorkDirectory, Release1, "Backend", SeedRollback), ThreeBlockRollbackOfTheSeed(config) + "\n");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, BeforeEveryRelease);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var next = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        next.Result.Should().Be(MigrationRunResult.Ok, because: $"the rollback of the seed continues and the files before it follow: {next.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.MigrateDown)
            .Which.ResumeFromBlock.Should().Be(1, because: "rollback block 1 ran in the first migrate-down");
        ctx.AssertFileStatuses(
            (Seed, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (CreateTableA, MigrationStatus.NotMigrated));
    }

    /// <summary>
    /// (g) A missing rollback file leaves the record of a file that stopped after its committed block with that block. With
    /// RequireRollbackFile=true the chain aborts at the seed, which it reaches first: the run is Error, the message names
    /// the rollback file, and the files before the seed stay Migrated; once the file is there, migrate-down rolls the seed
    /// back. With RequireRollbackFile=false the chain skips the seed with a warning that names its committed block and rolls
    /// the files before it back: the run is PartialSuccess, and the closing line of the chain counts the two files it rolled
    /// back cleanly.
    /// </summary>
    public static async Task VerifyMissingRollbackFileKeepsTheCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config, bool requireRollbackFile)
    {
        // Arrange: the rollback file of the seed goes away after the run that stopped the seed
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).WithRequireRollbackFile(requireRollbackFile).BuildAsync();
        await StopTheSeedAfterItsFirstBlockAsync(ctx, config);
        string rollbackPath = Path.Combine(ctx.WorkDirectory, Release1, "Backend", SeedRollback);
        string rollbackContent = File.ReadAllText(rollbackPath);
        File.Delete(rollbackPath);
        var warnings = CollectWarnings(ctx);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease);

        // Assert
        AssertStoppedAfterOneBlock(ctx);
        if (requireRollbackFile)
        {
            down.Result.Should().Be(MigrationRunResult.Error, because: "the required rollback file of the seed is missing");
            down.ErrorMessage.Should().Contain(SeedRollback)
                .And.Contain($"{Seed} keeps its status with its 1 committed block(s), and migrate-down rolls it back once the file is there");
            ctx.AssertFileStatuses((CreateTableB, MigrationStatus.Migrated), (CreateTableA, MigrationStatus.Migrated));
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);

            // Act: the rollback file is there again
            File.WriteAllText(rollbackPath, rollbackContent);
            await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, BeforeEveryRelease);
            var next = await ctx.MigrateDownAsync(BeforeEveryRelease);

            // Assert
            next.Result.Should().Be(MigrationRunResult.Ok, because: $"migrate-down rolls the seed back now: {next.ErrorMessage}");
            ctx.AssertFileStatuses(
                (Seed, MigrationStatus.NotMigrated),
                (CreateTableB, MigrationStatus.NotMigrated),
                (CreateTableA, MigrationStatus.NotMigrated));
        }
        else
        {
            down.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "the chain skipped the seed for its missing rollback file");
            down.Messages.Should().Contain(
                $"WARNING [{Seed}]: Rollback file not found: {SeedRollback}; the record keeps its 1 committed block(s), and the file stays pending for the next migrate-up");
            down.Messages[^1].Should().Be("Rolled back 2 of 3 migration(s) cleanly; 1 with a warning",
                because: "the closing line of the chain counts the files it rolled back cleanly, not the seed it skipped");
            warnings.Should().ContainSingle(w => w.Contains(Seed) && w.Contains("committed block"),
                because: "the log names the committed block that stays on the target");
            ctx.AssertFileStatuses((CreateTableB, MigrationStatus.NotMigrated), (CreateTableA, MigrationStatus.NotMigrated));
        }
    }

    /// <summary>
    /// (h) Simulate lists the rollback of a file that stopped after its committed block, from its first rollback block, and
    /// changes nothing: the seed stays Failed with its block, and its row stays on the target.
    /// </summary>
    public static async Task VerifySimulateListsTheRollbackOfTheFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await SeedOnTheSecondDatabase(builder, config).BuildAsync();
        await StopTheSeedAfterItsFirstBlockAsync(ctx, config);
        var simulation = new RecordingProgressObserver();
        ctx.UseProgressObserver(simulation);

        // Act
        var down = await ctx.MigrateDownAsync(BeforeEveryRelease, runMode: MigrationRunMode.Simulate);

        // Assert
        down.Success.Should().BeTrue(because: $"a simulation changes nothing: {down.ErrorMessage}");
        simulation.Of<FileStartedEvent>().Where(f => f.Operation == MigrationOperation.MigrateDown).Select(f => (f.FileName, f.ResumeFromBlock))
            .Should().Equal([(Seed, 0), (CreateTableB, 0), (CreateTableA, 0)], because: "the simulation lists the rollback of the seed from its first rollback block");
        AssertStoppedAfterOneBlock(ctx);
        ctx.AssertFileStatuses((CreateTableB, MigrationStatus.Migrated), (CreateTableA, MigrationStatus.Migrated));
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
        ctx.AssertRunCount(1);
    }
}
