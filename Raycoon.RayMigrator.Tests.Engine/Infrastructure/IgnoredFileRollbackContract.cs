using System.Collections.Concurrent;
using System.Text;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The rollback of a file that a migrate-up left Failed under MigrationErrorAction=Ignore, which every engine performs when
/// the same run stops later: the chain of an error action (Rollback, RollbackRelease) and that of a cancel action
/// (RollbackRun, RollbackRelease) roll back every file the run ran, newest first, the stopped file first, each from its
/// first rollback block, the files the run left Failed under Ignore included, so that the next migrate-up runs such a file
/// from its first block instead of resuming it after blocks whose effect the rollback of the files before it took away.
/// The scope stays the run: RollbackRelease takes the files of the stopped file's release only. A file without a committed
/// block is rolled back as well, because blocks after its failed first block may have run, and a missing rollback file of
/// such a file stops or skips the chain as that of any file does. The seed of release 1.0 runs in three blocks of one row
/// each on the second database, where every block commits on its own.
/// </summary>
internal static class IgnoredFileRollbackContract
{
    private const string Release1 = "Release_1.0";
    private const string Release2 = "Release_2.0";
    private const string MainDb = "MainDB";
    private const string SecondDb = "SecondDB";
    private const string CreateTableA = "01_CreateTableA.sql";
    private const string CreateTableB = "02_CreateTableB.sql";
    private const string Seed = "03_SeedDataA.sql";
    private const string SeedRollback = "03_SeedDataA.rollback.sql";
    private const string CreateTableC = "01_CreateTableC.sql";
    private const string CreateTableD = "02_CreateTableD.sql";
    private const string SeedB = "03_SeedDataB.sql";
    private const string Reason = "stopped by the test";

    /// <summary>
    /// The three-block seed of release 1.0 on the second database with a block that fails: block 2, or block 1 with
    /// <paramref name="firstBlockFails"/>.
    /// </summary>
    private static ScenarioBuilder FailingSeedOnTheSecondDatabase(ScenarioBuilder builder, EngineConfig config, bool firstBlockFails = false)
    {
        string error = SqlDialect.GetErrorSql(config.DatabaseType);
        return CancellationContract.WithThreeBlockSeed(builder, config,
                secondBlock: firstBlockFails ? null : error,
                firstBlock: firstBlockFails ? error : null)
            .WithTargetConnectionString(config.ConnectionString2!);
    }

    /// <summary>MigrationErrorAction=Ignore in the header of the seed of release 1.0, which overrides the action of the product.</summary>
    private static ScenarioBuilder IgnoreInTheSeed(ScenarioBuilder builder)
        => builder.SetFileToml(Release1, Seed, "MigrationErrorAction", "\"Ignore\"");

    /// <summary>
    /// 02 of release 2.0 in three blocks: the CREATE TABLE of the fixture, a block that fails, and an INSERT into the new
    /// table, which commits after the gap.
    /// </summary>
    private static byte[] ThreeBlockCreateTableD(EngineConfig config)
    {
        string original = File.ReadAllText(Path.Combine(config.BaseFilesPath, Release2, "Backend", CreateTableD));
        int headerEnd = original.IndexOf("*/", StringComparison.Ordinal) + 2;
        string body = SqlDialect.JoinBlocks(config.DatabaseType,
            original[headerEnd..].Trim().TrimEnd(';'),
            SqlDialect.GetErrorSql(config.DatabaseType),
            SqlDialect.GetInsertIntoTableDSql(config.DatabaseType, "delta"));
        return Encoding.UTF8.GetBytes(original[..headerEnd] + "\n\n" + body + "\n");
    }

    /// <summary>The files the chain of the stopped migrate-up rolled back, skipped or failed to roll back, in the order of the chain.</summary>
    private static List<string> RollbackChain(IReadOnlyList<FileCompletedEvent> completed)
        => completed.Where(f => f.Operation == MigrationOperation.Rollback).Select(f => f.FileName).ToList();

    /// <summary>Asserts that the seed of release 1.0 is Failed with block 1 of 3 committed.</summary>
    private static void AssertSeedFailedAfterOneBlock(ScenarioContext ctx)
        => ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });

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
    /// (a) The error recovery rolls back the file the run left Failed under Ignore: the seed, with MigrationErrorAction=Ignore
    /// in its header, fails at block 2 and ends Failed with block 1, while the rows of blocks 1 and 3 stay on the target.
    /// Then 01 of release 2.0 fails, and the Rollback of the product rolls back that file first and then every file the run
    /// ran, newest first: the seed from its first rollback block, then 02 and 01. The run is Recovered, the pending list
    /// names the seed NotMigrated, and the next migrate-up runs the seed from its first block, so that both of its rows are
    /// there again. Without the seed in the chain, the next migrate-up would create the table anew and resume the seed
    /// with block 2, and the row of block 1 would be missing.
    /// </summary>
    public static async Task VerifyErrorRecoveryRollsBackTheIgnoredFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await IgnoreInTheSeed(FailingSeedOnTheSecondDatabase(builder, config))
            .InjectError(Release2, CreateTableC)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();
        var recovery = new RecordingProgressObserver();
        ctx.UseProgressObserver(recovery);

        // Act
        var failed = await ctx.MigrateUpAsync(Release2);

        // Assert
        recovery.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.MigrateUp)
            .Which.Outcome.Should().Be(FileOutcome.FailedIgnored, because: "the seed ran to its end with its failed block ignored");
        failed.Result.Should().Be(MigrationRunResult.Recovered, because: $"the chain rolled back every file of the run cleanly: {failed.ErrorMessage}");
        RollbackChain(recovery.Of<FileCompletedEvent>()).Should().Equal([CreateTableC, Seed, CreateTableB, CreateTableA],
            because: "the chain rolls back the failed file first and then every file the run ran, newest first, the seed it left Failed under Ignore included");
        recovery.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.Rollback)
            .Which.ResumeFromBlock.Should().Be(0, because: "the rollback of the seed starts at its first rollback block");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 1
        });
        ctx.AssertFileStatuses(
            (CreateTableC, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (CreateTableA, MigrationStatus.NotMigrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);

        // Act: the pending list
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == Seed).Which.Should().Be(
            new PendingMigrationInfo(Release1, "Backend", MainDb, Seed, 3, PendingMigrationReason.NotMigrated, null),
            because: "the rolled back seed runs from its first block");

        // Act: the next migrate-up of release 1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var rerun = new RecordingProgressObserver();
        ctx.UseProgressObserver(rerun);
        var up = await ctx.MigrateUpAsync(Release1);

        // Assert
        up.Result.Should().Be(MigrationRunResult.PartialSuccess, because: "block 2 of the seed fails again and is ignored again");
        rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "a rolled back file is never resumed");
        AssertSeedFailedAfterOneBlock(ctx);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
    }

    /// <summary>
    /// (b) The rollback of a cancel action rolls back the file the run left Failed under Ignore: with MigrationErrorAction=Ignore
    /// for the product the seed ends Failed with block 1, and the host cancels the run with RollbackRun when 01 of release
    /// 2.0 starts. The chain rolls back the interrupted file first and then every file the run ran, newest first, the seed
    /// included, and the run is Cancelled with four files rolled back.
    /// </summary>
    public static async Task VerifyCancelActionRollsBackTheIgnoredFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await FailingSeedOnTheSecondDatabase(builder, config)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(CreateTableC, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.RollbackRun, Reason));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        observer.Fired.Should().BeTrue(because: "01 of release 2.0 started");
        result.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the rollback of the action completed cleanly: {result.ErrorMessage}");
        RollbackChain(observer.Of<FileCompletedEvent>()).Should().Equal([CreateTableC, Seed, CreateTableB, CreateTableA],
            because: "the chain rolls back the interrupted file first and then every file the run ran, newest first, the seed it left Failed under Ignore included");
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRun): 4 file(s) rolled back. Reason: {Reason}");
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
        ctx.AssertFileStatuses(
            (CreateTableC, MigrationStatus.NotMigrated),
            (Seed, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (CreateTableA, MigrationStatus.NotMigrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);
    }

    /// <summary>
    /// (c) RollbackRelease keeps the scope of the release: with MigrationErrorAction=Ignore the seed of release 1.0 and 02 of
    /// release 2.0 each end Failed with block 1, and the host cancels the run with RollbackRelease when the seed of release
    /// 2.0 starts. The chain rolls back the interrupted seed of release 2.0 first and then the files of release 2.0 the run
    /// ran, newest first, 02 included; the seed of release 1.0 keeps its block and its rows, and the files of release 1.0
    /// stay Migrated.
    /// </summary>
    public static async Task VerifyRollbackReleaseTakesTheIgnoredFilesOfItsReleaseAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await FailingSeedOnTheSecondDatabase(builder, config)
            .WithFileBytes($"{Release2}/Backend/{CreateTableD}", ThreeBlockCreateTableD(config))
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(SeedB, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.RollbackRelease, Reason));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == CreateTableD && f.Operation == MigrationOperation.MigrateUp)
            .Which.Outcome.Should().Be(FileOutcome.FailedIgnored, because: "02 of release 2.0 ran to its end with its failed block ignored");
        result.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the rollback of the action completed cleanly: {result.ErrorMessage}");
        RollbackChain(observer.Of<FileCompletedEvent>()).Should().Equal([SeedB, CreateTableD, CreateTableC],
            because: "RollbackRelease takes the files of release 2.0 the run ran, the one it left Failed under Ignore included");
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRelease): 3 file(s) rolled back. Reason: {Reason}");
        ctx.AssertFileStatuses(
            (SeedB, MigrationStatus.NotMigrated),
            (CreateTableD, MigrationStatus.NotMigrated),
            (CreateTableC, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.Migrated),
            (CreateTableA, MigrationStatus.Migrated));
        AssertSeedFailedAfterOneBlock(ctx);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tabled", false);
    }

    /// <summary>
    /// (d) A file the run left Failed under Ignore without a committed block is rolled back as well: block 1 of the seed fails
    /// and blocks 2 and 3 commit after it, so its record counts no committed block while two of its rows are on the target.
    /// The host cancels the run with RollbackRun after the seed, before release 2.0 starts: the chain rolls back the seed
    /// first, which takes both rows, and then the files before it.
    /// </summary>
    public static async Task VerifyIgnoredFileWithoutACommittedBlockIsRolledBackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await FailingSeedOnTheSecondDatabase(builder, config, firstBlockFails: true)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        int? rowsAfterTheSeed = null;
        int? rowsAfterTheRollbackOfTheSeed = null;
        var observer = new ProbingProgressObserver(e =>
        {
            if (e is not FileCompletedEvent { FileName: Seed } seed)
            {
                return;
            }

            if (seed.Operation == MigrationOperation.MigrateUp)
            {
                rowsAfterTheSeed = ctx.CountRowsOnConnection(config.ConnectionString2!, "tablea");
                cancellation.Cancel(MigrationCancelledAction.RollbackRun, Reason);
            }
            else if (seed.Operation == MigrationOperation.Rollback)
            {
                rowsAfterTheRollbackOfTheSeed = ctx.CountRowsOnConnection(config.ConnectionString2!, "tablea");
            }
        });
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        rowsAfterTheSeed.Should().Be(2, because: "blocks 2 and 3 of the seed committed after its failed first block");
        result.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the rollback of the action completed cleanly: {result.ErrorMessage}");
        RollbackChain(observer.Of<FileCompletedEvent>()).Should().Equal([Seed, CreateTableB, CreateTableA],
            because: "the run stopped before release 2.0, and the chain rolls back the seed although its record counts no committed block");
        rowsAfterTheRollbackOfTheSeed.Should().Be(0, because: "the rollback of the seed takes the rows of blocks 2 and 3");
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRun): 3 file(s) rolled back. Reason: {Reason}");
        ctx.AssertFileStatuses(
            (Seed, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (CreateTableA, MigrationStatus.NotMigrated));
        ctx.CountMigrationsByFilename(CreateTableC).Should().Be(0, because: "the run stopped before release 2.0 started");
    }

    /// <summary>
    /// (e) In FileByFile order with two targets the chain rolls back the record of the target that ran the file: the seed, with
    /// MigrationErrorAction=Ignore in its header, fails at block 2 on the first target and ends Failed with block 1 there,
    /// so the second target skips it and has no record of it. 01 of release 2.0 fails on the first target, and the Rollback
    /// of the product rolls it back first and then the records the run wrote, newest first: the seed on the first target,
    /// then 02 and 01 on both targets.
    /// </summary>
    public static async Task VerifyChainTakesTheRecordOfTheTargetThatRanTheIgnoredFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        var seed = CancellationContract.WithThreeBlockSeed(builder, config, secondBlock: SqlDialect.GetErrorSql(config.DatabaseType));
        await using var ctx = await IgnoreInTheSeed(seed)
            .WithMultiTarget(config.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.FileByFile)
            .InjectError(Release2, CreateTableC)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();
        var recovery = new RecordingProgressObserver();
        ctx.UseProgressObserver(recovery);

        // Act
        var failed = await ctx.MigrateUpAsync(Release2);

        // Assert
        failed.Result.Should().Be(MigrationRunResult.Recovered, because: $"the chain rolled back every record of the run cleanly: {failed.ErrorMessage}");
        recovery.Of<FileCompletedEvent>().Where(f => f.Operation == MigrationOperation.Rollback).Select(f => (f.FileName, f.TargetAlias))
            .Should().Equal(
                [(CreateTableC, MainDb), (Seed, MainDb), (CreateTableB, SecondDb), (CreateTableB, MainDb), (CreateTableA, SecondDb), (CreateTableA, MainDb)],
                because: "the chain takes the record of the seed on the first target, the only one the run wrote for it");
        ctx.CountMigrationsByFilename(Seed).Should().Be(1, because: "the second target skipped the seed after its failure on the first");
        ctx.AssertFileStatusForTarget(Seed, MainDb, MigrationStatus.NotMigrated);
        foreach (string target in new[] { MainDb, SecondDb })
        {
            ctx.AssertFileStatusForTarget(CreateTableB, target, MigrationStatus.NotMigrated);
            ctx.AssertFileStatusForTarget(CreateTableA, target, MigrationStatus.NotMigrated);
        }

        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);
    }

    /// <summary>
    /// (f) A missing rollback file of a file the run left Failed under Ignore is handled as that of any file of the chain:
    /// RequireRollbackFile=false, and the seed, Failed with block 1, has no rollback file. The run fails at 01 of release 2.0
    /// under the Rollback of the product, with MigrationErrorAction=Ignore in the header of the seed, or, with
    /// <paramref name="cancelled"/>, the host cancels it with RollbackRun when that file starts, under the Ignore of the
    /// product. With StopRollbackOnMissingRollbackFile=true the chain rolls back 01 and stops at the seed, and the files
    /// before it stay Migrated; with false it skips the seed with a warning that names its committed block and rolls back
    /// the files before it. Either way the seed keeps its block, and the rollback is not clean: the run is Error, after a
    /// cancel with CancelledRollbackIncomplete (1005), instead of Recovered or Cancelled. The rollback events name each file
    /// at its position in the run, and that of the seed reports its three blocks.
    /// </summary>
    public static async Task VerifyMissingRollbackFileOfTheIgnoredFileAsync(
        ScenarioBuilder builder, EngineConfig config, bool stopRollbackOnMissingRollbackFile, bool cancelled)
    {
        // Arrange
        var scenario = FailingSeedOnTheSecondDatabase(builder, config)
            .RemoveRollback(Release1, Seed)
            .WithRequireRollbackFile(false)
            .WithStopRollbackOnMissingRollbackFile(stopRollbackOnMissingRollbackFile);
        scenario = cancelled
            ? scenario.WithMigrationErrorAction(MigrationErrorAction.Ignore)
            : IgnoreInTheSeed(scenario).InjectError(Release2, CreateTableC).WithMigrationErrorAction(MigrationErrorAction.Rollback);
        await using var ctx = await scenario.BuildAsync();
        using var cancellation = new RunCancellation();
        var whenCreateTableCStarts = CancellingProgressObserver.WhenFileStarts(CreateTableC, MigrationOperation.MigrateUp);
        var observer = new CancellingProgressObserver(
            e => cancelled && whenCreateTableCStarts(e),
            () => cancellation.Cancel(MigrationCancelledAction.RollbackRun, Reason));
        ctx.UseProgressObserver(observer);
        var warnings = CollectWarnings(ctx);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        List<string> chain = stopRollbackOnMissingRollbackFile
            ? [CreateTableC, Seed]
            : [CreateTableC, Seed, CreateTableB, CreateTableA];
        RollbackChain(observer.Of<FileCompletedEvent>()).Should().Equal(chain,
            because: "the chain reaches the seed right after 01 and stops or goes on there as at any file without its rollback file");
        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.Rollback)
            .Which.Outcome.Should().Be(FileOutcome.Skipped, because: "the seed has no rollback file");
        var started = observer.Of<FileStartedEvent>();
        started.Where(f => f.Operation == MigrationOperation.Rollback).Select(f => (f.FileName, f.FileOrderId)).Should().Equal(
            chain.Select(file => (file, started.Single(f => f.FileName == file && f.Operation == MigrationOperation.MigrateUp).FileOrderId)),
            because: "the rollback events of the chain name each file at its position in the run, as its migrate-up event does");
        started.Should().ContainSingle(f => f.FileName == Seed && f.Operation == MigrationOperation.Rollback)
            .Which.BlocksTotal.Should().Be(3, because: "without its rollback file the rollback event of the seed reports the three blocks of the seed");
        result.Result.Should().Be(MigrationRunResult.Error, because: "the chain did not roll back the seed, so the state is unclear");
        ctx.AssertRunResult(MigrationRunResult.Error);
        string warning = stopRollbackOnMissingRollbackFile
            ? $"Rollback stopped: rollback file not found: {SeedRollback}"
            : $"Rollback file not found: {SeedRollback}";
        if (cancelled)
        {
            result.ErrorCode.Should().Be(TemplateResultCode.CancelledRollbackIncomplete);
            result.ErrorMessage.Should().Be(
                $"Cancelled on request (RollbackRun): the rollback did not complete: {(stopRollbackOnMissingRollbackFile ? 1 : 3)} of 4 file(s) rolled back; " +
                $"{Seed}: {warning}; the record keeps its 1 committed block(s), and the file stays pending for the next migrate-up. Reason: {Reason}");
        }
        else
        {
            result.Messages.Should().ContainSingle().Which.Should().StartWith($"Migration failed at file {CreateTableC}:",
                because: "the failure of 01 decides the message of the run");
        }

        warnings.Should().ContainSingle(w => w.Contains(Seed) && w.StartsWith(
                stopRollbackOnMissingRollbackFile ? "Rollback stopped (StopRollbackOnMissingRollbackFile=true)" : "Rollback file missing, the file keeps its committed blocks",
                StringComparison.Ordinal),
            because: "the log names the seed whose rollback file is missing");
        AssertSeedFailedAfterOneBlock(ctx);
        ctx.AssertFileStatus(CreateTableC, MigrationStatus.NotMigrated);
        var filesBefore = stopRollbackOnMissingRollbackFile ? MigrationStatus.Migrated : MigrationStatus.NotMigrated;
        ctx.AssertFileStatuses((CreateTableB, filesBefore), (CreateTableA, filesBefore));
        if (stopRollbackOnMissingRollbackFile)
        {
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
        }
    }
}
