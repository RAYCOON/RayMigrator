using System.Collections.Concurrent;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services.Abstractions;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The pending migrations every engine reports: <see cref="MigrationStatusInfo.PendingFiles"/> lists the (file, target)
/// pairs the next migrate-up runs, in the order it runs them, each with its <see cref="PendingMigrationReason"/> and, for
/// a resumed file, the committed blocks it skips. <see cref="MigrationStatusInfo.PendingMigrations"/> is the length of the
/// list, every target group counts its own entries and names the end of its Migrated record that finished last. Every
/// scenario then runs the next migrate-up with a recorder and compares the files it started with the list.
/// </summary>
internal static class PendingMigrationsContract
{
    private const string Release1 = "Release_1.0";
    private const string Release2 = "Release_2.0";
    private const string Release3 = "Release_3.0";
    private const string Release4 = "Release_4.0";
    private const string Backend = "Backend";
    private const string Frontend = "Frontend";
    private const string MainDb = "MainDB";
    private const string SecondDb = "SecondDB";
    private const string CreateTableD = "02_CreateTableD.sql";
    private const string Seed = "03_SeedDataA.sql";
    private const string SeedB = "03_SeedDataB.sql";
    private const string SeedD = "03_SeedDataD.sql";

    /// <summary>The files of the standard set per release, in discovery order.</summary>
    private static readonly (string Release, string[] Files)[] StandardFiles =
    [
        (Release1, ["01_CreateTableA.sql", "02_CreateTableB.sql", Seed]),
        (Release2, ["01_CreateTableC.sql", CreateTableD, SeedB]),
        (Release3, ["01_CreateTableE.sql", "02_CreateTableF.sql", "03_SeedDataC.sql"]),
        (Release4, ["01_CreateTableG.sql", "02_CreateTableH.sql", SeedD])
    ];

    /// <summary>
    /// (a) A fresh repository lists every file of the set as NotMigrated on the target, in release and file order, and
    /// the next migrate-up runs exactly these files.
    /// </summary>
    public static async Task VerifyFreshRepositoryAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.SelectMany(r => r.Files.Select(f => (r.Release, f))),
            because: "a fresh repository has every file pending, in the order of the releases and files");
        status.PendingFiles.Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated && p.TargetAlias == MainDb
            && p.TargetGroupAlias == Backend && p.ResumeFromBlock == null);
        status.PendingFiles.Select(p => p.FileOrderId).Should().Equal(Enumerable.Range(1, 12));
        status.TargetGroups[Backend].PendingMigrations.Should().Be(12);
        status.TargetGroups[Backend].LastMigrationDate.Should().BeNull(because: "nothing is migrated yet");

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>(b) After a migrate-up to Release_2.0 the list holds the six files of Release_3.0 and Release_4.0.</summary>
    public static async Task VerifyPartialMigrationAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.BuildAsync();
        var first = await ctx.MigrateUpAsync(Release2);
        first.Success.Should().BeTrue(because: $"the first run migrates: {first.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(2).SelectMany(r => r.Files.Select(f => (r.Release, f))));
        status.PendingFiles.Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated && p.ResumeFromBlock == null);
        status.TargetGroups[Backend].PendingMigrations.Should().Be(6);
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (c) A file that failed in its first block is Failed: nothing committed, so the next migrate-up runs it again from
    /// the start; the files after it are NotMigrated. The file is repaired before the next run, which changes its hash.
    /// </summary>
    public static async Task VerifyFailedFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .InjectError(Release2, CreateTableD)
            .BuildAsync();
        var failed = await ctx.MigrateUpAsync();
        failed.Success.Should().BeFalse(because: "the injected error stops the run");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.Filename, p.Reason)).Should().Equal(
            new[] { (Release2, CreateTableD, PendingMigrationReason.Failed), (Release2, SeedB, PendingMigrationReason.NotMigrated) }
                .Concat(StandardFiles.Skip(2).SelectMany(r => r.Files.Select(f => (r.Release, f, PendingMigrationReason.NotMigrated)))));
        status.PendingFiles.Should().OnlyContain(p => p.ResumeFromBlock == null);
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        File.Copy(Path.Combine(config.BaseFilesPath, Release2, Backend, CreateTableD),
            Path.Combine(ctx.WorkDirectory, Release2, Backend, CreateTableD), overwrite: true);
        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (d) A cancel with Terminate after block 1 of a three-block seed on a target of its own (every block commits on its
    /// own) leaves the seed Resumable after block 1; the next migrate-up starts it with ResumeFromBlock 1 and the files
    /// of the later releases from the start.
    /// </summary>
    public static async Task VerifyResumableFileAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, "stopped by the test")));
        var cancelled = await ctx.MigrateUpAsync(cancellation: cancellation);
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the cancel stops the run: {cancelled.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles[0].Should().Be(new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.Resumable, 1),
            because: "block 1 of the seed committed before the cancel");
        status.PendingFiles.Skip(1).Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(1).SelectMany(r => r.Files.Select(f => (r.Release, f))));
        status.PendingFiles.Skip(1).Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated);
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (e) A RunAlways file is the only pending file after a full migrate-up, and it stays pending after the next one,
    /// which runs nothing else.
    /// </summary>
    public static async Task VerifyRunAlwaysFileAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.SetFileToml(Release1, Seed, "RunAlways", "true").BuildAsync();
        var first = await ctx.MigrateUpAsync();
        first.Success.Should().BeTrue(because: $"the first run migrates: {first.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Should().Equal(
            [new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.RunAlways, null)],
            because: "a RunAlways file runs on every migrate-up, every other file is migrated");
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        (await ctx.InfoAsync()).PendingFiles.Should().ContainSingle()
            .Which.Reason.Should().Be(PendingMigrationReason.RunAlways, because: "the file stays pending after it ran");
    }

    /// <summary>
    /// (f) A migrated file of the newest release changed on disk is a HashMismatch: migrate-up runs it again. The change
    /// keeps the SQL valid, so the run succeeds and nothing is pending afterwards.
    /// </summary>
    public static async Task VerifyHashMismatchAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.BuildAsync();
        var first = await ctx.MigrateUpAsync();
        first.Success.Should().BeTrue(because: $"the first run migrates: {first.ErrorMessage}");
        ChangeFile(ctx, Release4, SeedD, "'typeC'", "'typeC2'");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Should().Equal(
            [new PendingMigrationInfo(Release4, Backend, MainDb, SeedD, 12, PendingMigrationReason.HashMismatch, null)],
            because: "the migrated file no longer matches its hash under the scope File");
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (g) A changed file of a release older than the target has migrated is listed as a HashMismatch as well, but
    /// migrate-up refuses it as out of order and starts no file; with allow-out-of-order it runs exactly the list.
    /// </summary>
    public static async Task VerifyOutOfOrderHashMismatchAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder.BuildAsync();
        var first = await ctx.MigrateUpAsync();
        first.Success.Should().BeTrue(because: $"the first run migrates: {first.ErrorMessage}");
        ChangeFile(ctx, Release1, Seed, "'gamma'", "'gamma2'");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Should().Equal(
            [new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.HashMismatch, null)],
            because: "the list names every pair migrate-up would run, out of order or not");

        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        var refused = await ctx.MigrateUpAsync();
        refused.Success.Should().BeFalse(because: "Release_1.0 is older than the release the target has migrated");
        refused.ErrorMessage.Should().Contain("Out-of-order migrations detected");
        recorder.Of<FileStartedEvent>().Should().BeEmpty(because: "the out-of-order check stops the run before the first file");

        var next = await RunNextMigrateUpAsync(ctx, status, allowOutOfOrder: true);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (h) Two targets, the second fails at Release_2.0/02_CreateTableD.sql (a conflicting table there only): the list
    /// holds the failed pair of the second target and what is left on both targets, in the target order of
    /// <paramref name="order"/> (#8). The next migrate-up after the cause is removed runs exactly these pairs.
    /// </summary>
    public static async Task VerifyMultiTargetWithFailedTargetAsync(ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order)
    {
        // Arrange
        string secondDb = config.ConnectionString2!;
        await using var ctx = await builder
            .WithMultiTarget(secondDb)
            .WithTargetMigrationOrder(order)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetCreateSimpleTableSql(config.DatabaseType, "tabled"));
        var failed = await ctx.MigrateUpAsync();
        failed.Success.Should().BeFalse(because: "the conflicting table fails the file on the second target");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        bool fileByFile = order == TargetMigrationOrder.FileByFile;
        var release2 = fileByFile
            ? new[]
            {
                (Release2, SecondDb, CreateTableD, PendingMigrationReason.Failed),
                (Release2, MainDb, SeedB, PendingMigrationReason.NotMigrated),
                (Release2, SecondDb, SeedB, PendingMigrationReason.NotMigrated)
            }
            : new[]
            {
                (Release2, SecondDb, CreateTableD, PendingMigrationReason.Failed),
                (Release2, SecondDb, SeedB, PendingMigrationReason.NotMigrated)
            };
        var later = StandardFiles.Skip(2).SelectMany(r => fileByFile
            ? r.Files.SelectMany(f => new[] { (r.Release, MainDb, f, PendingMigrationReason.NotMigrated), (r.Release, SecondDb, f, PendingMigrationReason.NotMigrated) })
            : new[] { MainDb, SecondDb }.SelectMany(t => r.Files.Select(f => (r.Release, t, f, PendingMigrationReason.NotMigrated))));
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.TargetAlias!, p.Filename, p.Reason)).Should().Equal(release2.Concat(later),
            because: $"the pairs are listed per (file, target) in {order} order");
        status.PendingMigrations.Should().Be(fileByFile ? 15 : 14);
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetDropSimpleTableSql(config.DatabaseType, "tabled"));
        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the retry migrates the lagging target: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (i) Two target groups with TargetGroupMigrationOrder "Frontend,Backend": every release lists Frontend before
    /// Backend, although discovery numbers the Backend files first, and the next migrate-up runs them in that order.
    /// </summary>
    public static async Task VerifyTargetGroupMigrationOrderAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await builder
            .WithTargetGroup(Frontend, config.DatabaseType, config.ConnectionString2!)
            .WithTargetGroupMigrationOrder("Frontend,Backend")
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        CopyFrontendFiles(ctx.WorkDirectory, config);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.TargetGroupAlias)).Distinct().Should().Equal(
            StandardFiles.SelectMany(r => new[] { (r.Release, Frontend), (r.Release, Backend) }),
            because: "the TargetGroupMigrationOrder of the product puts Frontend first in every release");
        status.PendingFiles.Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated
            && p.TargetAlias == (p.TargetGroupAlias == Frontend ? "FrontendDB" : MainDb));
        status.TargetGroups[Frontend].PendingMigrations.Should().Be(8);
        status.TargetGroups[Backend].PendingMigrations.Should().Be(12);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (j) A record of an open run whose heartbeat went stale is listed as the orphan repair of the next migrate-up leaves
    /// it: the seed committed block 1 in a run that ended abruptly while block 2 ran (Executing with block 1), so the list
    /// names it Resumable after block 1, and the next migrate-up, which closes the run first, starts it with ResumeFromBlock 1.
    /// </summary>
    public static async Task VerifyOpenRecordWithCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: block 1 committed, then the footprint of a run that ended abruptly while block 2 ran
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, "stopped by the test")));
        (await ctx.MigrateUpAsync(cancellation: cancellation)).Result.Should().Be(MigrationRunResult.Cancelled);
        ctx.InsertOrphanedMigrationRun(minutesOld: 0, heartbeatMinutesOld: 3);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1, migrationRunId: ctx.GetMigrationRun(2)!.Id);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles[0].Should().Be(new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.Resumable, 1),
            because: "the orphan repair of the next migrate-up keeps block 1 of the seed");
        status.PendingFiles.Skip(1).Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(1).SelectMany(r => r.Files.Select(f => (r.Release, f))));

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (k) A record of an open run with every block committed is not listed: the run ended abruptly after the last block of
    /// the seed, before its final status (Executing with 3 of 3), and the orphan repair of the next migrate-up finishes the
    /// record as Migrated, so that migrate-up runs only the files of the later releases.
    /// </summary>
    public static async Task VerifyOpenRecordWithEveryBlockCommittedAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange: release 1.0 migrated, then the footprint of a run that ended before the final status of the seed
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        (await ctx.MigrateUpAsync(Release1)).Success.Should().BeTrue();
        ctx.InsertOrphanedMigrationRun(minutesOld: 0, heartbeatMinutesOld: 3);
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 3, migrationRunId: ctx.GetMigrationRun(2)!.Id);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(1).SelectMany(r => r.Files.Select(f => (r.Release, f))),
            because: "the orphan repair finishes the seed, which committed every block");
        status.PendingFiles.Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 3);
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (l) A RunAlways file with committed blocks of an earlier attempt is RunAlways, not Resumable: the cancel of (d) after
    /// block 1 of the seed leaves its record Failed with block 1, which any other file resumes after, but migrate-up runs a
    /// RunAlways file in full. The list names the seed RunAlways without a resume point, the next migrate-up starts it with
    /// ResumeFromBlock 0, so that block 1 runs a second time, and the seed stays pending afterwards.
    /// </summary>
    public static async Task VerifyRunAlwaysFileAfterCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config)
    {
        // Arrange
        await using var ctx = await CancellationContract.WithThreeBlockSeed(builder, config)
            .SetFileToml(Release1, Seed, "RunAlways", "true")
            .WithTargetConnectionString(config.ConnectionString2!)
            .BuildAsync();
        using var cancellation = new RunCancellation();
        ctx.UseProgressObserver(new CancellingProgressObserver(
            CancellingProgressObserver.AfterBlock(Seed, 1, MigrationOperation.MigrateUp),
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, "stopped by the test")));
        var cancelled = await ctx.MigrateUpAsync(cancellation: cancellation);
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the cancel stops the run: {cancelled.ErrorMessage}");
        ctx.AssertMigrationRecord(Seed, new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles[0].Should().Be(new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.RunAlways, null),
            because: "migrate-up runs a RunAlways file in full, also after committed blocks");
        status.PendingFiles.Skip(1).Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(1).SelectMany(r => r.Files.Select(f => (r.Release, f))));
        status.PendingFiles.Skip(1).Should().OnlyContain(p => p.Reason == PendingMigrationReason.NotMigrated);
        await AssertLastMigrationDateAsync(ctx, status, Backend);

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");
        ctx.AssertRowCountOnConnection(config.ConnectionString2!, "tablea", 4);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        (await ctx.InfoAsync()).PendingFiles.Should().Equal(
            [new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.RunAlways, null)],
            because: "the RunAlways file stays pending after it ran, every other file is migrated");
    }

    /// <summary>
    /// (m) A file that a CLI tool runs is never Resumable: the tool runs the whole file. The cancel of (d) after block 1 of
    /// the seed leaves its record Failed with block 1 of 3 on the DAL, then every target gets the tool. The list names the
    /// seed Failed without a resume point, and the next migrate-up starts it with ResumeFromBlock 0. A record with every
    /// block of an attempt that never got its final status (Executing, as an engine before this version left the record of
    /// a tool it started) is Failed as well, because migrate-up does not finalize it but runs the file again through the
    /// tool. The tool is a shell that exits at once (not on Windows). <paramref name="order"/> TargetByTarget covers the
    /// other file loop of migrate-up.
    /// </summary>
    public static async Task VerifyCliToolFileAfterCommittedBlocksAsync(ScenarioBuilder builder, EngineConfig config, TargetMigrationOrder order)
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
            () => cancellation.Cancel(MigrationCancelledAction.Terminate, "stopped by the test")));
        var cancelled = await ctx.MigrateUpAsync(cancellation: cancellation);
        cancelled.Result.Should().Be(MigrationRunResult.Cancelled, because: $"the cancel stops the run: {cancelled.ErrorMessage}");
        BlockProgressContract.UseCliToolOnEveryTarget(ctx, "shell");
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var status = await ctx.InfoAsync();

        // Assert
        AssertConsistent(status);
        status.PendingFiles[0].Should().Be(new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.Failed, null),
            because: "the tool runs the whole file, also after committed blocks");
        status.PendingFiles.Skip(1).Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.Skip(1).SelectMany(r => r.Files.Select(f => (r.Release, f))));
        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"the files of the list migrate: {next.ErrorMessage}");

        // Arrange: the record of the seed shows every block of an attempt that never got its final status
        ctx.SetMigrationRecordState(Seed, MainDb, MigrationStatus.Executing, fileUpBlocksMigrated: 1);
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        // Act
        var afterEveryBlock = await ctx.InfoAsync();

        // Assert
        afterEveryBlock.PendingFiles.Should().Equal(
            [new PendingMigrationInfo(Release1, Backend, MainDb, Seed, 3, PendingMigrationReason.Failed, null)],
            because: "migrate-up does not finalize the record of a file that the tool runs, it runs the file again");
        var again = await RunNextMigrateUpAsync(ctx, afterEveryBlock, allowOutOfOrder: true);
        again.Success.Should().BeTrue(because: $"the tool runs the seed again: {again.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// (n) A TargetGroupMigrationOrder of the product that names exactly its only target group has nothing to sort: the
    /// status ignores it instead of refusing it and lists every file in release and file order, without a warning of its
    /// own, because the validation at startup reports the ignored product setting (RULE_1_13). The next migrate-up ignores
    /// the order as well and runs exactly these files.
    /// </summary>
    public static async Task VerifySingleTargetGroupMigrationOrderAsync(ScenarioBuilder builder)
    {
        // Arrange
        await using var ctx = await builder
            .WithTargetGroupMigrationOrder(Backend)
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var warnings = new ConcurrentQueue<string>();
        ctx.UseLogObserver(logEvent =>
        {
            if (logEvent.Level == LogEventLevel.Warning)
            {
                warnings.Enqueue(logEvent.RenderMessage());
            }
        });

        // Act
        var status = await ctx.InfoAsync();
        string[] statusWarnings = [.. warnings];

        // Assert
        AssertConsistent(status);
        status.PendingFiles.Select(p => (p.ReleaseVersion, p.Filename)).Should().Equal(
            StandardFiles.SelectMany(r => r.Files.Select(f => (r.Release, f))),
            because: "an order of the only target group leaves the list in release and file order");
        statusWarnings.Should().NotContain(w => w.Contains("cannot be applied", StringComparison.Ordinal),
            because: "the status does not refuse an order that migrate-up ignores");
        statusWarnings.Should().NotContain(w => w.Contains("is ignored", StringComparison.Ordinal),
            because: "the validation at startup reports the product setting, so the status adds no warning of its own");

        var next = await RunNextMigrateUpAsync(ctx, status);
        next.Success.Should().BeTrue(because: $"migrate-up ignores the order as well: {next.ErrorMessage}");
        await AssertNothingPendingAsync(ctx);
    }

    /// <summary>
    /// Runs the next migrate-up with a recorder and asserts that it starts exactly the listed pairs, in the order of the
    /// list, each with the committed blocks the list names (0 for a file that starts from its first block).
    /// </summary>
    private static async Task<MigrationOperationResult> RunNextMigrateUpAsync(
        ScenarioContext ctx, MigrationStatusInfo status, bool allowOutOfOrder = false)
    {
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        var result = await ctx.MigrateUpAsync(allowOutOfOrder: allowOutOfOrder);

        recorder.Of<FileStartedEvent>()
            .Where(e => e.Operation == MigrationOperation.MigrateUp)
            .Select(e => (e.ReleaseVersion, e.TargetGroupAlias, (string?)e.TargetAlias, e.FileName, e.FileOrderId, e.ResumeFromBlock))
            .Should().Equal(
                status.PendingFiles.Select(p => (p.ReleaseVersion, p.TargetGroupAlias, p.TargetAlias, p.Filename, p.FileOrderId, p.ResumeFromBlock ?? 0)),
                because: "the next migrate-up runs exactly the listed pairs, in the order of the list");
        return result;
    }

    /// <summary>The invariants of every status: the count is the length of the list, and the target groups split it.</summary>
    private static void AssertConsistent(MigrationStatusInfo status)
    {
        status.PendingMigrations.Should().Be(status.PendingFiles.Count, because: "the count is the length of the list");
        foreach (var (alias, targetGroup) in status.TargetGroups)
        {
            targetGroup.PendingMigrations.Should().Be(status.PendingFiles.Count(p => p.TargetGroupAlias == alias),
                because: $"target group {alias} counts its own entries");
        }
        status.TargetGroups.Values.Sum(tg => tg.PendingMigrations).Should().Be(status.PendingMigrations,
            because: "every pending file belongs to a configured target group");
        status.PendingFiles.Should().OnlyContain(p => (p.Reason == PendingMigrationReason.Resumable) == (p.ResumeFromBlock != null),
            because: "only a resumed file names the blocks it skips");
    }

    /// <summary>
    /// The target group names the end of its Migrated record that finished last: it lies within the newest run, which
    /// finished after it (<see cref="MigrationStatusInfo.LastMigrationDate"/>). Both times come from the clock of the
    /// repository database.
    /// </summary>
    private static async Task AssertLastMigrationDateAsync(ScenarioContext ctx, MigrationStatusInfo status, string targetGroup)
    {
        var newestRun = (await ctx.GetHistoryAsync()).Runs[0];
        var lastMigration = status.TargetGroups[targetGroup].LastMigrationDate;

        lastMigration.Should().NotBeNull(because: "the target group has Migrated records with an end");
        lastMigration!.Value.Should().BeOnOrAfter(newestRun.StartedAt, because: "the newest run migrated files of the target group");
        lastMigration.Value.Should().BeOnOrBefore(status.LastMigrationDate!.Value, because: "the run finished after its records");
    }

    /// <summary>Asserts that a status taken now lists nothing.</summary>
    private static async Task AssertNothingPendingAsync(ScenarioContext ctx)
    {
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();
        status.PendingFiles.Should().BeEmpty(because: "the run migrated every listed pair");
        status.PendingMigrations.Should().Be(0);
    }

    /// <summary>Replaces <paramref name="oldValue"/>, which occurs once, in a migration file of the scenario.</summary>
    private static void ChangeFile(ScenarioContext ctx, string release, string filename, string oldValue, string newValue)
    {
        string path = Path.Combine(ctx.WorkDirectory, release, Backend, filename);
        string content = File.ReadAllText(path);
        content.Should().Contain(oldValue, because: "the change must alter the hash of the file");
        File.WriteAllText(path, content.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    /// <summary>Copies the Frontend set of the database next to the Backend files of the scenario.</summary>
    private static void CopyFrontendFiles(string workDir, EngineConfig config)
    {
        string frontendBase = config.BaseFilesPath + "_Frontend";
        foreach (string source in Directory.GetFiles(frontendBase, "*.*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(workDir, Path.GetRelativePath(frontendBase, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }
}
