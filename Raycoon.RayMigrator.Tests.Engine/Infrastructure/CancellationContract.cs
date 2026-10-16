using System.Diagnostics;
using System.Text;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The cancellation contract every engine keeps: a run the host cancels stops at the next block boundary and then
/// takes the <see cref="MigrationCancelledAction"/> the host chose; migrate-down, baseline and a running error recovery
/// stop and ignore the action; the run is recorded as <see cref="MigrationRunResult.Cancelled"/> with schema step 3,
/// a migrate-up whose error recovery the cancel stopped as <see cref="MigrationRunResult.Error"/> with its failure.
/// The cancel comes from a <see cref="CancellingProgressObserver"/> at an exact event, so the scenarios need no timing;
/// only the test of a block interrupted in flight waits for real time.
/// </summary>
internal static class CancellationContract
{
    private const string Release1 = "Release_1.0";
    private const string Release2 = "Release_2.0";
    private const string CreateTableA = "01_CreateTableA.sql";
    private const string CreateTableB = "02_CreateTableB.sql";
    private const string Seed = "03_SeedDataA.sql";
    private const string CreateTableC = "01_CreateTableC.sql";
    private const string CreateTableCRollback = "01_CreateTableC.rollback.sql";
    private const string CreateTableD = "02_CreateTableD.sql";
    private const string SeedB = "03_SeedDataB.sql";
    private const string Reason = "stopped by the test";

    private const string SeedHeader = "/*\n[RayMigrator]\nUseTransaction = true\nEnvironments = [\"*\"]\nRunAlways = false\n*/\n\n";

    /// <summary>The LogLevelId of a warning in the database log (<c>LogLevel.Warning</c>).</summary>
    private const int WarningLogLevelId = 3;

    /// <summary>
    /// The seed file of release 1.0 as three blocks of one row each (alpha, beta, gamma) in the block syntax of the
    /// database; a block can be replaced, for example by an error or a sleep.
    /// </summary>
    public static ScenarioBuilder WithThreeBlockSeed(
        ScenarioBuilder builder, EngineConfig config, string? secondBlock = null, string? thirdBlock = null, string? firstBlock = null)
    {
        string body = SqlDialect.JoinBlocks(config.DatabaseType,
            firstBlock ?? SqlDialect.GetInsertIntoTableASql(config.DatabaseType, "alpha", 10),
            secondBlock ?? SqlDialect.GetInsertIntoTableASql(config.DatabaseType, "beta", 20),
            thirdBlock ?? SqlDialect.GetInsertIntoTableASql(config.DatabaseType, "gamma", 30));
        return builder.WithFileBytes($"{Release1}/Backend/{Seed}", Encoding.UTF8.GetBytes(SeedHeader + body + "\n"));
    }

    /// <summary>The three-block seed with the target on the second database: every block commits on its own.</summary>
    private static ScenarioBuilder BlockByBlock(ScenarioBuilder builder, EngineConfig config)
        => WithThreeBlockSeed(builder, config).WithTargetConnectionString(config.ConnectionString2!);

    private static void CancelWith(RunCancellation cancellation, MigrationCancelledAction action)
        => cancellation.Cancel(action, Reason).Should().BeTrue(because: "the first cancel of a run decides its action");

    /// <summary>
    /// A cancel is expected: the warnings it logs say where the run stopped and why, without the exception, whose stack
    /// trace goes to Debug. The database sink appends the exception of an event to its message.
    /// </summary>
    private static void AssertCancelWarningsCarryNoException(ScenarioContext ctx)
    {
        ctx.FlushDatabaseLog();
        var warnings = ctx.GetLogMessages(WarningLogLevelId);
        warnings.Should().Contain(m => m.Contains("cancel", StringComparison.OrdinalIgnoreCase), because: "the database log records the cancel");
        warnings.Should().NotContain(m => m.Contains("\nException:\n", StringComparison.Ordinal),
            because: "a cancel is no failure, so its warnings carry no stack trace");
    }

    /// <summary>
    /// (a) Terminate in a file: the file keeps its committed block, is Failed, and the next run resumes it. The
    /// scenarios run in FileByFile order; <paramref name="order"/> TargetByTarget covers the other file loop.
    /// </summary>
    public static async Task VerifyTerminateInFileAsync(ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order = TargetMigrationOrder.FileByFile)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).WithTargetMigrationOrder(order).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.Terminate));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        observer.Fired.Should().BeTrue(because: "the seed file ran its first block");
        result.Success.Should().BeFalse(because: "a cancelled run did not finish");
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.ErrorMessage.Should().StartWith($"Cancelled on request (Terminate): stopped at {Seed} on target MainDB after 1 of 3 block(s)")
            .And.EndWith($"Reason: {Reason}");
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationRunResultId.Should().Be((int)MigrationRunResult.Cancelled, because: "schema step 3 knows the result Cancelled");
        run.ErrorCode.Should().Be(TemplateResultCode.Cancelled.ToString());
        run.ErrorMessage.Should().Be(result.ErrorMessage);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        ctx.AssertFileStatuses((CreateTableA, MigrationStatus.Migrated), (CreateTableB, MigrationStatus.Migrated));
        ctx.GetMigrationHistory(Seed).Should().Equal([(MigrationStatus.Failed, MigrationOperation.MigrateUp)],
            because: "the stop is a terminal transition of the record");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
        observer.Of<BlockCompletedEvent>().Where(b => b.FileName == Seed).Should().ContainSingle(because: "the run stopped before block 2");
        observer.Of<FileCompletedEvent>().Where(f => f.FileName == Seed).Should().ContainSingle()
            .Which.Outcome.Should().Be(FileOutcome.Cancelled);
        var completed = observer.Of<RunCompletedEvent>().Should().ContainSingle().Which;
        completed.Success.Should().BeFalse();
        completed.Result.Should().Be(MigrationRunResult.Cancelled);
        AssertCancelWarningsCarryNoException(ctx);

        // Act: the next run resumes the file with block 2
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var second = await ctx.MigrateUpAsync(Release1);

        // Assert
        second.Success.Should().BeTrue(because: $"the resumed run finishes the file: {second.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(1, because: "block 1 committed before the cancel");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Migrated, FileUpBlocksMigrated = 3 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
    }

    /// <summary>(b) RollbackFile: the interrupted file is rolled back, the files before it stay, the next run starts it anew.</summary>
    public static async Task VerifyRollbackFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackFile));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled, because: "the rollback of the action completed cleanly");
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackFile): 1 file(s) rolled back. Reason: {Reason}");
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
        ctx.AssertFileStatuses(
            (CreateTableA, MigrationStatus.Migrated),
            (CreateTableB, MigrationStatus.Migrated),
            (Seed, MigrationStatus.NotMigrated));
        ctx.GetMigrationHistory(Seed).Should().Equal(
            [(MigrationStatus.Failed, MigrationOperation.MigrateUp), (MigrationStatus.NotMigrated, MigrationOperation.Rollback)],
            because: "the cancel left the file Failed and the action rolled it back");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 0);
        var seedFileEvents = observer.Events
            .Where(e => e is FileStartedEvent { FileName: Seed } or FileCompletedEvent { FileName: Seed })
            .Select(e => e is FileCompletedEvent c ? $"{c.Operation} {c.Outcome}" : $"{((FileStartedEvent)e).Operation} started")
            .ToList();
        seedFileEvents.Should().Equal("MigrateUp started", "MigrateUp Cancelled", "Rollback started", "Rollback RolledBack");

        // Act: the next run executes the rolled back file from its first block
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var rerun = new RecordingProgressObserver();
        ctx.UseProgressObserver(rerun);
        var second = await ctx.MigrateUpAsync(Release1);

        // Assert
        second.Success.Should().BeTrue(because: $"the file runs again: {second.ErrorMessage}");
        rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed)
            .Which.ResumeFromBlock.Should().Be(0, because: "a rolled back file is never resumed");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
    }

    /// <summary>(c) RollbackRun: the interrupted file and every file the run migrated are rolled back.</summary>
    public static async Task VerifyRollbackRunAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRun): 3 file(s) rolled back. Reason: {Reason}");
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
        ctx.AssertFileStatuses(
            (CreateTableA, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.NotMigrated),
            (Seed, MigrationStatus.NotMigrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tableb", false);
        observer.Of<FileCompletedEvent>().Where(f => f.Operation == MigrationOperation.Rollback).Select(f => f.FileName)
            .Should().Equal([Seed, CreateTableB, CreateTableA], because: "the interrupted file goes first, then the run's files newest first");
    }

    /// <summary>
    /// (d) RollbackFile without a rollback file and StopRollbackOnMissingRollbackFile=true: the chain stops at the
    /// missing file, the record stays Failed, and the run is Error, because the state is unclear.
    /// </summary>
    public static async Task VerifyRollbackFileWithoutRollbackFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config)
            .WithRequireRollbackFile(false)
            .WithStopRollbackOnMissingRollbackFile(true)
            .RemoveRollback(Release1, Seed)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackFile));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(MigrationRunResult.Error, because: "the rollback of the action stopped at the missing rollback file");
        result.ErrorCode.Should().Be(TemplateResultCode.CancelledRollbackIncomplete);
        result.ErrorMessage.Should().StartWith("Cancelled on request (RollbackFile): the rollback did not complete:")
            .And.Contain("rollback file not found");
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error);
        run.ErrorCode.Should().Be(TemplateResultCode.CancelledRollbackIncomplete.ToString());
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 1 });
        observer.Of<FileCompletedEvent>().Should().Contain(f => f.FileName == Seed && f.Operation == MigrationOperation.Rollback && f.Outcome == FileOutcome.Skipped,
            because: "the chain reports the file it stopped at");
    }

    /// <summary>(e) A cancel of the token of the call, with no handle in the request, behaves like Terminate.</summary>
    public static async Task VerifyBareTokenTerminatesAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            tokenSource.Cancel);
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpWithTokenAsync(tokenSource.Token, Release1);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorMessage.Should().StartWith("Cancelled on request (Terminate):").And.NotContain("Reason:", because: "a token carries no reason");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 1 });
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
    }

    /// <summary>
    /// (f) The atomic path (target and repository share the connection): the cancel lands before the first block of the
    /// file, nothing of the file commits, the record is Failed with no block, and the next run executes the whole file.
    /// </summary>
    public static async Task VerifyAtomicPathAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await WithThreeBlockSeed(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(Seed, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.Terminate));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });
        ctx.AssertRowCount("tablea", 0);
        observer.Of<BlockCompletedEvent>().Should().NotContain(b => b.FileName == Seed, because: "no block of the file committed");
        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.Outcome.Should().Be(FileOutcome.Cancelled);

        // Act: the next run executes every block
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release1);
        var rerun = new RecordingProgressObserver();
        ctx.UseProgressObserver(rerun);
        var second = await ctx.MigrateUpAsync(Release1);

        // Assert
        second.Success.Should().BeTrue(because: $"the file runs whole: {second.ErrorMessage}");
        rerun.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.ResumeFromBlock.Should().Be(0);
        ctx.AssertRowCount("tablea", 3);
    }

    /// <summary>(g) A cancel between two files: no further file starts, RollbackRun rolls back the files the run migrated.</summary>
    public static async Task VerifyCancelBetweenFilesAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterFile(CreateTableA, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorMessage.Should().StartWith("Cancelled on request (RollbackRun): 1 file(s) rolled back");
        ctx.AssertFileStatus(CreateTableA, MigrationStatus.NotMigrated);
        ctx.CountMigrationsByFilename(CreateTableB).Should().Be(0, because: "the run stopped before the second file started");
        ctx.CountMigrationsByFilename(Seed).Should().Be(0);
        observer.Of<FileStartedEvent>().Should().NotContain(f => f.FileName == CreateTableB);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablea", false);
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
    }

    /// <summary>
    /// (h) A cancelled migrate-down stops before the next rollback block and ignores the action: the record in progress is
    /// Failed with its rollback blocks, the records after it keep their state, and the next migrate-down continues.
    /// </summary>
    public static async Task VerifyMigrateDownAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated, its seed rolls back in three blocks
        string rollbackOfSeedB = SqlDialect.JoinBlocks(config.DatabaseType,
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product1"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product2"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product3"));
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithFileBytes($"{Release2}/Backend/03_SeedDataB.rollback.sql", Encoding.UTF8.GetBytes(rollbackOfSeedB + "\n"))
            .BuildAsync();
        await ctx.MigrateUpAsync(Release2);
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateDownAsync(Release1, cancellation: cancellation);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.ErrorMessage.Should().Be(
            $"Cancelled on request (RollbackRun ignored: a rollback was already running): migrate-down stopped in the rollback of {SeedB} on target MainDB " +
            $"after 1 of 3 rollback block(s); the next migrate-down continues that rollback, and migrate-up refuses {SeedB} until it has completed. Reason: {Reason}",
            because: "the message says where the cancel stopped the chain and what continues it");
        ctx.AssertMigrationRecord(SeedB, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        });
        ctx.AssertFileStatuses((CreateTableD, MigrationStatus.Migrated), (CreateTableC, MigrationStatus.Migrated));
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Cancelled });
        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == SeedB)
            .Which.Outcome.Should().Be(FileOutcome.Cancelled);

        // Act: the next migrate-down continues the rollback with block 2
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var resume = new RecordingProgressObserver();
        ctx.UseProgressObserver(resume);
        var second = await ctx.MigrateDownAsync(Release1);

        // Assert
        second.Success.Should().BeTrue(because: $"the rollback continues: {second.ErrorMessage}");
        resume.Of<FileStartedEvent>().Should().ContainSingle(f => f.FileName == SeedB).Which.ResumeFromBlock.Should().Be(1);
        ctx.AssertFileStatuses(
            (SeedB, MigrationStatus.NotMigrated),
            (CreateTableD, MigrationStatus.NotMigrated),
            (CreateTableC, MigrationStatus.NotMigrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablec", false);
    }

    /// <summary>
    /// (i) A cancel during the error recovery (MigrationErrorAction=Rollback) stops the recovery before the next record
    /// and ignores the cancel action: the failed file is rolled back, the files the recovery did not reach stay Migrated.
    /// The run failed before the cancel, so it stays Error with the code of the failure, and its message names the
    /// failure first and then how far the recovery got and that the files it did not reach keep their state.
    /// </summary>
    public static async Task VerifyCancelDuringErrorRecoveryAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 3 of the seed fails, the error recovery rolls back the seed first
        await using var ctx = await WithThreeBlockSeed(builder, config, thirdBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.Rollback),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        observer.Fired.Should().BeTrue(because: "the error recovery ran the rollback of the seed");
        result.Success.Should().BeFalse();
        result.Result.Should().Be(MigrationRunResult.Error, because: "the run failed at the seed before the cancel stopped its error recovery");
        result.ErrorCode.Should().BeNull(because: "the failure of the seed decides the code, a database error has no engine code, and the cancel does not replace it");
        string failure = result.MigrationResults.Should().ContainSingle(r => !r.Success).Which.ErrorMessage!;
        string expectedMessage = $"{failure.Trim().TrimEnd('.')}. The error recovery was cancelled on request after 1 of 3 rollback(s): " +
                                 $"the files it did not reach keep their state. Reason: {Reason}";
        result.ErrorMessage.Should().Be(expectedMessage, because: "the message names the failure of the seed first");
        result.Messages.Should().Equal($"Migration failed at file {Seed}: {expectedMessage}");
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error);
        run.ErrorCode.Should().BeNull();
        run.ErrorMessage.Should().Be(expectedMessage);
        var completed = observer.Of<RunCompletedEvent>().Should().ContainSingle().Which;
        completed.Success.Should().BeFalse();
        completed.Result.Should().Be(MigrationRunResult.Error);
        completed.ErrorMessage.Should().Be(expectedMessage);
        ctx.FlushDatabaseLog();
        ctx.GetLogMessages(WarningLogLevelId).Should().Contain(m => m.Contains("Cancel action RollbackRun ignored"),
            because: "the cancel action is not taken while a rollback runs, and the log says so");
        ctx.AssertFileStatuses(
            (Seed, MigrationStatus.NotMigrated),
            (CreateTableB, MigrationStatus.Migrated),
            (CreateTableA, MigrationStatus.Migrated));
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tableb", true);
    }

    /// <summary>
    /// (v) A cancel that comes when the failed file completes, before the error recovery reaches its first record: the
    /// recovery rolls nothing back, the failed seed stays Failed with the two blocks it committed, and the files the run
    /// migrated stay Migrated. The run stays Error with the code of the failure, and its message says that nothing was
    /// rolled back.
    /// </summary>
    public static async Task VerifyCancelBeforeTheErrorRecoveryReachesItsFirstRecordAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 3 of the seed fails, and the host cancels when the seed reports its failure
        await using var ctx = await WithThreeBlockSeed(builder, config, thirdBlock: SqlDialect.GetErrorSql(config.DatabaseType))
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterFile(Seed, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        observer.Fired.Should().BeTrue(because: "the seed reported its failure before the error recovery started");
        result.Result.Should().Be(MigrationRunResult.Error, because: "the run failed at the seed before the cancel stopped its error recovery");
        result.ErrorCode.Should().BeNull(because: "the failure of the seed decides the code, and a database error has no engine code");
        string failure = result.MigrationResults.Should().ContainSingle(r => !r.Success).Which.ErrorMessage!;
        string expectedMessage = $"{failure.Trim().TrimEnd('.')}. The error recovery was cancelled on request before its first rollback: " +
                                 $"nothing was rolled back, {Seed} stays Failed with the blocks it committed, and the files this run " +
                                 $"migrated stay Migrated. Reason: {Reason}";
        result.ErrorMessage.Should().Be(expectedMessage, because: "the recovery stopped before the rollback of the seed, its first record");
        ctx.GetMigrationRun(1)!.ErrorMessage.Should().Be(expectedMessage);
        observer.Of<FileStartedEvent>().Should().NotContain(f => f.Operation == MigrationOperation.Rollback,
            because: "the recovery rolled nothing back");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 2,
            FileUpBlocksTotal = 3
        });
        ctx.GetMigrationRecord(Seed)!.FileDownBlocksMigrated.Should().BeNull(because: "no rollback of the seed started");
        ctx.AssertFileStatuses((CreateTableB, MigrationStatus.Migrated), (CreateTableA, MigrationStatus.Migrated));
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 2);
        ctx.FlushDatabaseLog();
        ctx.GetLogMessages(WarningLogLevelId).Should().Contain(m => m.Contains("Cancel action RollbackRun ignored"),
            because: "the run failed before the cancel, so its error recovery and not the cancel action decides what is rolled back");
    }

    /// <summary>(j) A cancelled baseline stops before the next file and keeps the records it wrote.</summary>
    public static async Task VerifyBaselineAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder.BuildAsync(MigrationCommand.Baseline);
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.AfterFile(CreateTableA, MigrationOperation.Baseline),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.BaselineAsync(Release1, cancellation: cancellation);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.BaselinedRecords.Should().Be(1, because: "the first file was baselined before the cancel");
        result.ErrorMessage.Should().StartWith("Cancelled on request (RollbackRun ignored: a baseline executes no SQL to roll back): baseline stopped after 1 record(s)");
        ctx.AssertFileStatus(CreateTableA, MigrationStatus.Migrated);
        ctx.CountMigrationsByFilename(CreateTableB).Should().Be(0);
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
        observer.Of<RunCompletedEvent>().Should().ContainSingle().Which.Result.Should().Be(MigrationRunResult.Cancelled);
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (k) A repository of schema version 2 is upgraded to version 3 by the next command: the lookup row Cancelled and
    /// the four reader indexes exist afterwards, with their columns in key order.
    /// </summary>
    public static async Task VerifySchemaVersionTwoIsUpgradedToThreeAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the pinned host creates schema version 2 only
        await using var ctx = await builder.BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        ctx.PinRepositorySchemaVersion(2);
        await ctx.InfoAsync();
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(2);
        ctx.GetRepositoryLookupName("MigrationRunResult", (int)MigrationRunResult.Cancelled).Should().BeNull(because: "step 3 adds the row");
        var indexes = StepThreeIndexes(config.DatabaseType);
        indexes.Should().AllSatisfy(index => ctx.GetRepositoryIndexColumns(index.Name).Should().BeEmpty(because: $"{index.Name} comes with step 3"));

        // Act
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();

        // Assert
        ctx.AssertSuccess(true);
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(3);
        ctx.CountRepoRows("MigratorMeta").Should().Be(1, because: "the engine version registered at version 2 is kept");
        ctx.GetRepositoryLookupName("MigrationRunResult", (int)MigrationRunResult.Cancelled).Should().Be("Cancelled");
        ctx.CountRepoRows("MigrationRunResult").Should().Be(6);
        indexes.Should().AllSatisfy(index => ctx.GetRepositoryIndexColumns(index.Name).Should().Equal(index.Columns, because: $"{index.Name} lists its columns in key order"));
    }

    /// <summary>
    /// A cancel while a block runs: SQL Server and PostgreSQL abort the sleeping statement, and the <c>KILL QUERY</c> of
    /// MySQL and MariaDB ends the <c>SLEEP()</c> early without an error, so the run stops at the next block boundary. On the
    /// atomic path the transaction of the file is rolled back, and the record is Failed with no block; with a target
    /// connection of its own (<paramref name="ownTargetConnection"/>) the token reaches the command of the target DAL and
    /// every block commits on its own, so the record keeps the first block, and on MySQL and MariaDB the ended sleep as
    /// well. SQLite cannot interrupt a statement and stops at the next block boundary only.
    /// </summary>
    public static async Task VerifyCancelInsideASleepingBlockAsync(ScenarioBuilder builder, EngineConfig config, bool ownTargetConnection = false)
    {
        // Arrange: block 2 sleeps for 20 seconds; the cancel comes one second after the file started
        var scenario = WithThreeBlockSeed(builder, config, secondBlock: SqlDialect.GetSleepSql(config.DatabaseType, 20));
        if (ownTargetConnection)
        {
            scenario = scenario.WithTargetConnectionString(config.ConnectionString2!);
        }

        await using var ctx = await scenario.BuildAsync();
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(Seed, MigrationOperation.MigrateUp),
            () => tokenSource.CancelAfter(TimeSpan.FromSeconds(1)));
        ctx.UseProgressObserver(observer);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await ctx.MigrateUpWithTokenAsync(tokenSource.Token, Release1);
        stopwatch.Stop();

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), because: "the sleeping block was interrupted, not waited out");
        int committedBlocks = !ownTargetConnection ? 0 : config.DatabaseType is "MySql" or "MariaDb" ? 2 : 1;
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = committedBlocks });
        if (ownTargetConnection)
        {
            ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 1);
        }
        else
        {
            ctx.AssertRowCount("tablea", 0);
        }

        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == Seed).Which.Outcome.Should().Be(FileOutcome.Cancelled);
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (l) An update-hash cancelled between the two records of a file that migrated on two targets: the record updated
    /// before the stop counts, and its file with it. The cancel comes from the log line of the first record's update.
    /// </summary>
    public static async Task VerifyUpdateHashCancelledBetweenTheRecordsOfAFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated on two targets, then its first file changed on disk
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString)
            .WithMultiTarget(config.ConnectionString2!)
            .WithSerilogMinimumLevel("Debug")
            .BuildAsync();
        await ctx.MigrateUpAsync(Release1);
        ctx.AssertSuccess(true);
        File.AppendAllText(Path.Combine(ctx.WorkDirectory, Release1, "Backend", CreateTableA), "\n-- changed after the migration\n");
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ctx.UseLogObserver(logEvent =>
        {
            if (logEvent.MessageTemplate.Text.StartsWith("Execution of Template", StringComparison.Ordinal)
                && logEvent.RenderMessage().Contains("Repository_MigrationRecord_UpdateHash", StringComparison.Ordinal))
            {
                tokenSource.Cancel();
            }
        });

        // Act
        var result = await ctx.UpdateHashAsync(cancellationToken: tokenSource.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.UpdatedRecords.Should().Be(1, because: "the cancel came after the update of the first of the two records");
        result.UpdatedFiles.Should().Be(1, because: "a file counts once its first record is updated");
        result.UpdatedFileNames.Should().Equal(CreateTableA);
        result.ErrorMessage.Should().Be("Cancelled on request: 1 file(s) / 1 record(s) updated before the stop");
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (m) A migrate-down cancelled in the middle of a rollback leaves the record Failed with part of its rollback blocks.
    /// The next migrate-up refuses before its first file, because running the file again from block 1 on the half rolled
    /// back target would duplicate what the rollback did not take back yet; the migrate-down after it continues the
    /// rollback, and the migrate-up after that runs the release cleanly.
    /// </summary>
    public static async Task VerifyMigrateUpRefusesAnIncompleteRollbackAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 2.0 migrated, then a migrate-down cancelled after rollback block 1 of 3 of its seed
        string rollbackOfSeedB = SqlDialect.JoinBlocks(config.DatabaseType,
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product1"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product2"),
            SqlDialect.GetDeleteFromTableCSql(config.DatabaseType, "product3"));
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithFileBytes($"{Release2}/Backend/03_SeedDataB.rollback.sql", Encoding.UTF8.GetBytes(rollbackOfSeedB + "\n"))
            .BuildAsync();
        await ctx.MigrateUpAsync(Release2);
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(SeedB, 1, MigrationOperation.MigrateDown),
            () => CancelWith(cancellation, MigrationCancelledAction.Terminate)));
        (await ctx.MigrateDownAsync(Release1, cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        var halfRolledBack = new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileDownBlocksMigrated = 1,
            FileDownBlocksTotal = 3
        };
        ctx.AssertMigrationRecord(SeedB, halfRolledBack);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: info names the half rolled back file
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        // Assert
        status.PendingFiles.Should().ContainSingle(p => p.Filename == SeedB).Which.Should().Be(
            new PendingMigrationInfo(Release2, "Backend", "MainDB", SeedB, 6, PendingMigrationReason.RollbackIncomplete, null, 1, 3),
            because: "the next migrate-up refuses the file until migrate-down completes its rollback");
        status.PendingMigrations.Should().Be(status.PendingFiles.Count);

        // Act
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var refusedRun = new RecordingProgressObserver();
        ctx.UseProgressObserver(refusedRun);
        var refused = await ctx.MigrateUpAsync(Release2);

        // Assert
        refused.Success.Should().BeFalse(because: "running the seed again from block 1 would duplicate the rows the rollback kept");
        refused.Result.Should().Be(MigrationRunResult.Error);
        refused.ErrorMessage.Should().StartWith("Incomplete rollback: 1 file(s)")
            .And.Contain($"{Release2}/{SeedB} on target MainDB: 1 of 3 rollback blocks, migrate-down continues at block 2");
        refusedRun.Of<FileStartedEvent>().Should().BeEmpty(because: "the run refuses before its first file");
        ctx.AssertMigrationRun(3, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });
        ctx.AssertMigrationRecord(SeedB, halfRolledBack);
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 2);

        // Act: the next migrate-down continues the rollback with block 2
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"migrate-down continues the rollback: {down.ErrorMessage}");
        ctx.AssertFileStatuses(
            (SeedB, MigrationStatus.NotMigrated),
            (CreateTableD, MigrationStatus.NotMigrated),
            (CreateTableC, MigrationStatus.NotMigrated));

        // Act: migrate-up runs release 2.0 again
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, Release2);
        var up = await ctx.MigrateUpAsync(Release2);

        // Assert
        up.Success.Should().BeTrue(because: $"nothing is half rolled back any more: {up.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablec", 3);
    }

    /// <summary>
    /// (n) FileByFile on two targets, cancelled after the first file finished on its first target: the run stops before
    /// the second target, and the file counts like a finished file in the result and in the RunCompletedEvent, because
    /// it migrated on the target it ran on. RollbackFile has no interrupted file to roll back.
    /// </summary>
    public static async Task VerifyFileByFileCancelBetweenTheTargetsOfAFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString)
            .WithMultiTarget(config.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.FileByFile)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            e => e is FileCompletedEvent { FileName: CreateTableA, TargetAlias: "MainDB", Operation: MigrationOperation.MigrateUp },
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackFile));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackFile): nothing to roll back. Reason: {Reason}",
            because: "the run stopped between two targets, so no file was interrupted");
        result.TotalMigrations.Should().Be(1);
        result.SuccessfulMigrations.Should().Be(1, because: "the file migrated on the target it ran on");
        result.FailedMigrations.Should().Be(0);
        result.MigrationResults.Should().ContainSingle().Which.Should().Match<MigrationFileResult>(r => r.FileName == CreateTableA && r.Success);
        var completed = observer.Of<RunCompletedEvent>().Should().ContainSingle().Which;
        (completed.TotalFiles, completed.SucceededFiles, completed.FailedFiles).Should().Be((1, 1, 0));
        ctx.AssertFileStatusForTarget(CreateTableA, "MainDB", MigrationStatus.Migrated);
        observer.Of<FileStartedEvent>().Should().NotContain(f => f.TargetAlias == "SecondDB", because: "the run stopped before the second target");
    }

    /// <summary>
    /// (o) RollbackRelease in the second release of the run: the interrupted file and the files of its release that the
    /// run migrated are rolled back, newest first; the files of the first release, which the same run migrated, stay.
    /// </summary>
    public static async Task VerifyRollbackReleaseAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder.WithTargetConnectionString(config.ConnectionString2!).BuildAsync();
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(CreateTableD, MigrationOperation.MigrateUp),
            () => CancelWith(cancellation, MigrationCancelledAction.RollbackRelease));
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled, because: "the rollback of the action completed cleanly");
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRelease): 2 file(s) rolled back. Reason: {Reason}");
        ctx.AssertRunResult(MigrationRunResult.Cancelled);
        ctx.AssertFileStatuses(
            (CreateTableA, MigrationStatus.Migrated),
            (CreateTableB, MigrationStatus.Migrated),
            (Seed, MigrationStatus.Migrated),
            (CreateTableC, MigrationStatus.NotMigrated),
            (CreateTableD, MigrationStatus.NotMigrated));
        ctx.CountMigrationsByFilename(SeedB).Should().Be(0, because: "the run stopped before the last file of release 2.0");
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablec", false);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tableb", true);
        observer.Of<FileCompletedEvent>().Where(f => f.Operation == MigrationOperation.Rollback).Select(f => f.FileName)
            .Should().Equal([CreateTableD, CreateTableC], because: "the interrupted file goes first, then the files of its release, newest first");
    }

    /// <summary>
    /// (p) A cancel before the run starts, on a handle cancelled before the call: the run stops before it writes
    /// anything, also its run row (<c>MigrationRunId</c> 0), and RollbackRun has nothing to roll back. A migrate-down
    /// cancelled the same way stops before its first rollback. Neither logs a stack trace.
    /// </summary>
    public static async Task VerifyCancelBeforeTheRunStartsAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        using var cancellation = new RunCancellation();
        CancelWith(cancellation, MigrationCancelledAction.RollbackRun);
        var observer = new RecordingProgressObserver();
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release1, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.MigrationRunId.Should().Be(0, because: "the run stopped before its run row");
        result.ErrorMessage.Should().Be($"Cancelled on request (RollbackRun): nothing to roll back. Reason: {Reason}");
        ctx.CountMigrationRuns().Should().BeLessThanOrEqualTo(0, because: "no run row is written (-1: the repository was never created)");
        observer.Of<FileStartedEvent>().Should().BeEmpty();
        observer.Of<RunCompletedEvent>().Should().ContainSingle().Which.Result.Should().Be(MigrationRunResult.Cancelled);

        // Act: a migrate-down cancelled the same way
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var down = await ctx.MigrateDownAsync(Release1, cancellation: cancellation);

        // Assert
        down.Result.Should().Be(MigrationRunResult.Cancelled);
        down.MigrationRunId.Should().Be(0);
        down.ErrorMessage.Should().StartWith("Cancelled on request (RollbackRun ignored: a rollback was already running): migrate-down stopped before the first rollback");
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (q) A cancel in Simulate and in Validate mode stops the run as in Migrate mode and persists nothing: no run row, no
    /// record, no SQL on the target, and no rollback of the action.
    /// </summary>
    public static async Task VerifyCancelInSimulateAndValidateModePersistsNothingAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated, so that the repository holds one run and three records
        await using var ctx = await BlockByBlock(builder, config).BuildAsync();
        await ctx.MigrateUpAsync(Release1);
        ctx.AssertSuccess(true);

        foreach (var runMode in new[] { MigrationRunMode.Simulate, MigrationRunMode.Validate })
        {
            await ctx.RebuildForAsync(MigrationCommand.MigrateUp, runMode, Release2);
            using var cancellation = new RunCancellation();
            var observer = new CancellingProgressObserver(
                CancellingProgressObserver.AfterFile(CreateTableC, MigrationOperation.MigrateUp),
                () => CancelWith(cancellation, MigrationCancelledAction.RollbackRun));
            ctx.UseProgressObserver(observer);

            // Act
            var result = await ctx.MigrateUpAsync(Release2, runMode: runMode, cancellation: cancellation);

            // Assert
            observer.Fired.Should().BeTrue(because: $"{runMode} reports {CreateTableC}");
            result.Result.Should().Be(MigrationRunResult.Cancelled, because: $"{runMode} stops like Migrate");
            result.MigrationRunId.Should().Be(0, because: $"{runMode} writes no run row");
            observer.Of<FileStartedEvent>().Should().NotContain(f => f.FileName == CreateTableD, because: "the run stopped before the next file");
            ctx.CountMigrationRuns().Should().Be(1, because: $"{runMode} persists nothing");
            ctx.CountMigrations().Should().Be(3);
            ctx.CountMigrationsByFilename(CreateTableC).Should().Be(0);
            ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablec", false);
        }
    }

    /// <summary>
    /// (r) A cancel while a file runs through a CLI tool: the engine kills the tool, the file is Failed with no block,
    /// and the run is Cancelled. The tool is a shell that sleeps, so the test needs no database client (not on Windows).
    /// </summary>
    public static async Task VerifyCancelWhileACliToolRunsAsync(ScenarioBuilder builder, EngineConfig config)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The CLI tool of the test is /bin/sh");

        // Arrange: every file runs through a tool that sleeps for 20 seconds; the cancel comes one second after the first started
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithCliTool("sleeper", "/bin/sh", "-c \"sleep 20\" sleeper {FilePath}", timeoutInSeconds: 60)
            .WithUseCliToolAlias("sleeper")
            .BuildAsync();
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(CreateTableA, MigrationOperation.MigrateUp),
            () => tokenSource.CancelAfter(TimeSpan.FromSeconds(1)));
        ctx.UseProgressObserver(observer);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await ctx.MigrateUpWithTokenAsync(tokenSource.Token, Release1);
        stopwatch.Stop();

        // Assert
        result.Result.Should().Be(MigrationRunResult.Cancelled);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), because: "the tool was killed, not waited for");
        ctx.AssertMigrationRecord(CreateTableA, new MigrationRecordExpectation { MigrationStatusId = (int)MigrationStatus.Failed, FileUpBlocksMigrated = 0 });
        ctx.CountMigrationsByFilename(CreateTableB).Should().Be(0, because: "the run stopped before the next file");
        observer.Of<FileCompletedEvent>().Should().ContainSingle(f => f.FileName == CreateTableA).Which.Outcome.Should().Be(FileOutcome.Cancelled);
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (s) A repository of schema version 2 that holds data is upgraded to version 3 by the next command, and the data
    /// stays: every MigrationRecord, MigrationRun and MigrationRecordHistory row reads the same before and after step 3.
    /// </summary>
    public static async Task VerifySchemaVersionTwoWithDataIsUpgradedToThreeAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the pinned host creates schema version 2 and migrates every release with it
        await using var ctx = await builder.WithTargetConnectionString(config.ConnectionString2!).BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        ctx.PinRepositorySchemaVersion(2);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(2);
        var before = RepositoryDataSnapshot.Read(ctx);
        before.Records.Should().HaveCount(12, because: "the four releases have three files each");

        // Act: the next command, a read-only info, applies step 3
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        await ctx.InfoAsync();

        // Assert
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(3);
        var after = RepositoryDataSnapshot.Read(ctx);
        after.Counts.Should().Be(before.Counts, because: "step 3 adds no and deletes no row of these tables");
        after.Runs.Should().Equal(before.Runs);
        after.Records.Should().Equal(before.Records);
        after.History.Should().Equal(before.History);
    }

    /// <summary>The rows of the repository tables with data, as the test helpers read them.</summary>
    private sealed record RepositoryDataSnapshot(
        (int Records, int Runs, int History) Counts,
        IReadOnlyList<MigrationRunRecordDto?> Runs,
        IReadOnlyList<MigrationRecordDto?> Records,
        IReadOnlyList<string> History)
    {
        private static readonly string[] Files =
        [
            $"{Release1}:{CreateTableA}", $"{Release1}:{CreateTableB}", $"{Release1}:{Seed}",
            $"{Release2}:{CreateTableC}", $"{Release2}:{CreateTableD}", $"{Release2}:{SeedB}",
            "Release_3.0:01_CreateTableE.sql", "Release_3.0:02_CreateTableF.sql", "Release_3.0:03_SeedDataC.sql",
            "Release_4.0:01_CreateTableG.sql", "Release_4.0:02_CreateTableH.sql", "Release_4.0:03_SeedDataD.sql"
        ];

        public static RepositoryDataSnapshot Read(ScenarioContext ctx)
        {
            var counts = (ctx.CountRepoRows("MigrationRecord"), ctx.CountRepoRows("MigrationRun"), ctx.CountRepoRows("MigrationRecordHistory"));
            var runs = Enumerable.Range(1, counts.Item2).Select(ctx.GetMigrationRun).ToList();
            var filenames = Files.Select(f => f.Split(':')[1]).ToList();
            var records = filenames.Select(ctx.GetMigrationRecord).ToList();
            var history = filenames.SelectMany(f => ctx.GetMigrationHistory(f).Select(h => $"{f}:{h.Status}:{h.Operation}")).ToList();
            return new RepositoryDataSnapshot(counts, runs, records, history);
        }
    }

    /// <summary>
    /// (t) A fix cancelled between two orphaned runs: the run it closed stays closed and counts, the other stays open for
    /// the next fix, and the warning of the cancel carries no exception. The cancel comes from the log line of the first
    /// run's repair.
    /// </summary>
    public static async Task VerifyFixCancelledBetweenTwoRunsAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: two runs that ended without a heartbeat for two hours
        await using var ctx = await builder.WithSerilogMinimumLevel("Debug").BuildAsync();
        (await ctx.MigrateUpAsync(Release1)).Success.Should().BeTrue();
        ctx.InsertOrphanedMigrationRun(120);
        ctx.InsertOrphanedMigrationRun(120);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ctx.UseLogObserver(logEvent =>
        {
            if (logEvent.MessageTemplate.Text.StartsWith("Execution of Template", StringComparison.Ordinal)
                && logEvent.RenderMessage().Contains("Repository_MigrationRun_FixOrphaned", StringComparison.Ordinal))
            {
                tokenSource.Cancel();
            }
        });

        // Act
        var result = await ctx.FixIssuesAsync(cancellationToken: tokenSource.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(TemplateResultCode.Cancelled);
        result.OrphanedRunsFound.Should().Be(2);
        result.OrphanedRunsFixed.Should().Be(1, because: "the cancel came after the repair of the first run, before the second");
        result.ErrorMessage.Should().Be("Cancelled on request: 1 of 2 orphaned run(s) fixed before the stop");
        ctx.CountMigrationRuns().Should().Be(3);
        ctx.GetMigrationRun(2)!.MigrationRunResultId.Should().NotBe(ctx.GetMigrationRun(3)!.MigrationRunResultId,
            because: "one orphaned run was closed and the other stays open for the next fix");
        AssertCancelWarningsCarryNoException(ctx);
    }

    /// <summary>
    /// (u) RollbackRelease whose chain finds a required rollback file missing: the file disappeared during the run, after the
    /// discovery checked it. The chain rolls back the interrupted file and stops at the missing file, whose record keeps
    /// its status Migrated, because nothing of its rollback ran; the run is Error with 1005 and the message names the file.
    /// Once the file is there again, migrate-down rolls the record back.
    /// </summary>
    public static async Task VerifyRollbackActionWithAMissingRequiredRollbackFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: the cancel comes when the second file of release 2.0 starts, and the rollback file of the first goes away
        await using var ctx = await builder
            .WithTargetConnectionString(config.ConnectionString2!)
            .WithRequireRollbackFile(true)
            .BuildAsync();
        string rollbackPath = Path.Combine(ctx.WorkDirectory, Release2, "Backend", CreateTableCRollback);
        string rollbackContent = File.ReadAllText(rollbackPath);
        using var cancellation = new RunCancellation();
        var observer = new CancellingProgressObserver(
            CancellingProgressObserver.WhenFileStarts(CreateTableD, MigrationOperation.MigrateUp),
            () =>
            {
                File.Delete(rollbackPath);
                CancelWith(cancellation, MigrationCancelledAction.RollbackRelease);
            });
        ctx.UseProgressObserver(observer);

        // Act
        var result = await ctx.MigrateUpAsync(Release2, cancellation: cancellation);

        // Assert
        result.Result.Should().Be(MigrationRunResult.Error, because: "the rollback of the action stopped at the missing rollback file");
        result.ErrorCode.Should().Be(TemplateResultCode.CancelledRollbackIncomplete);
        ctx.AssertFileStatuses(
            (CreateTableA, MigrationStatus.Migrated),
            (CreateTableB, MigrationStatus.Migrated),
            (Seed, MigrationStatus.Migrated),
            (CreateTableD, MigrationStatus.NotMigrated),
            (CreateTableC, MigrationStatus.Migrated));
        ctx.GetMigrationRecord(CreateTableC)!.FileDownBlocksMigrated.Should().BeNull(because: "nothing of the rollback ran");
        result.ErrorMessage.Should().StartWith("Cancelled on request (RollbackRelease): the rollback did not complete:")
            .And.Contain(CreateTableCRollback, because: "the message names the missing rollback file");
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablec", true);

        // Act: the rollback file is there again, and migrate-down rolls the record back
        File.WriteAllText(rollbackPath, rollbackContent);
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, Release1);
        var down = await ctx.MigrateDownAsync(Release1);

        // Assert
        down.Success.Should().BeTrue(because: $"migrate-down picks the record that kept its status: {down.ErrorMessage}");
        ctx.AssertFileStatus(CreateTableC, MigrationStatus.NotMigrated);
        ctx.AssertTableExistsOnConnection(config.ConnectionString2!, "tablec", false);
    }

    /// <summary>The indexes of schema step 3 and their columns, as the templates of the database name them (no prefix).</summary>
    private static IReadOnlyList<(string Name, string[] Columns)> StepThreeIndexes(string databaseType) => databaseType switch
    {
        "SqlServer" or "Sqlite" =>
        [
            ("ix_MigrationRun_Product_Environment_StartedAt", ["ProductId", "EnvironmentId", "StartedAt"]),
            ("ix_MigrationRecordHistory_MigrationRunId", ["MigrationRunId"]),
            ("ix_MigrationRecordHistory_HistorizedAt", ["HistorizedAt"]),
            ("ix_MigrationRecord_Product_Environment", ["ProductId", "EnvironmentId"])
        ],
        _ =>
        [
            ("ix_migration_run_product_environment_started_at", ["product_id", "environment_id", "started_at"]),
            ("ix_migration_record_history_migration_run_id", ["migration_run_id"]),
            ("ix_migration_record_history_historized_at", ["historized_at"]),
            ("ix_migration_record_product_environment", ["product_id", "environment_id"])
        ]
    };
}
