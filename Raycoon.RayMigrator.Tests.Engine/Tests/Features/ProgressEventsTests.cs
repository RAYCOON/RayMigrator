using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The typed progress events a host observes through <see cref="MigrationContext.ProgressObserver"/>: one run
/// event pair per operation, one file event pair per file and target, one block event per executed block.
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class ProgressEventsTests : PostgreSqlTestBase
{
    public ProgressEventsTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_ReportsTheRunAndEveryFileOnEveryTarget()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        var result = await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        recorder.Events.First().Should().BeOfType<RunStartedEvent>();
        recorder.Events.Last().Should().BeOfType<RunCompletedEvent>();

        var started = recorder.Of<RunStartedEvent>().Should().ContainSingle().Which;
        started.Command.Should().Be(MigrationCommand.MigrateUp);
        started.RunMode.Should().Be(MigrationRunMode.Migrate);
        started.MigrationRunId.Should().Be(result.MigrationRunId, "the run row exists when the run is reported as started");
        started.MigrationRunId.Should().BePositive();
        started.FromRelease.Should().BeNull("nothing was migrated before the first run");
        started.TargetRelease.Should().Be("Release_1.0");

        var completed = recorder.Of<RunCompletedEvent>().Should().ContainSingle().Which;
        completed.Success.Should().BeTrue();
        completed.Result.Should().Be(MigrationRunResult.Ok);
        completed.MigrationRunId.Should().Be(result.MigrationRunId);
        completed.TotalFiles.Should().Be(result.TotalMigrations);

        var fileStarts = recorder.Of<FileStartedEvent>();
        var fileEnds = recorder.Of<FileCompletedEvent>();
        fileStarts.Should().HaveCount(result.TotalMigrations, "one start per file and target");
        fileEnds.Should().HaveCount(result.TotalMigrations, "one completion per file and target");
        fileEnds.Should().OnlyContain(e => e.Outcome == FileOutcome.Migrated && e.Operation == MigrationOperation.MigrateUp);
        fileStarts.Should().OnlyContain(e => e.MigrationRecordId > 0 && e.ReleaseVersion == "Release_1.0" && e.BlocksTotal >= 1);
        fileEnds.Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId))
            .Should().BeEquivalentTo(fileStarts.Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId)),
                "a completion carries the same identity as its start");

        foreach (var end in fileEnds)
        {
            int startIndex = recorder.Events.IndexOf(fileStarts.Single(s => s.FileName == end.FileName && s.TargetAlias == end.TargetAlias));
            recorder.Events.IndexOf(end).Should().BeGreaterThan(startIndex, "a file completes after it started");
        }

        var blocks = recorder.Of<BlockCompletedEvent>();
        blocks.Should().NotBeEmpty();
        blocks.Should().OnlyContain(b => b.Success && b.BlockNumber >= 1 && b.BlockNumber <= b.BlocksTotal);
        blocks.Select(b => b.FileName).Distinct().Should().BeEquivalentTo(fileStarts.Select(s => s.FileName).Distinct(),
            "every executed file reports its blocks");
    }

    [Fact]
    public async Task Simulate_ReportsSimulatedOutcomesWithoutARunRow()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);
        ctx.AssertSuccess(true);

        recorder.Of<RunStartedEvent>().Should().ContainSingle().Which.MigrationRunId.Should().Be(0, "Simulate writes no run row");
        recorder.Of<FileCompletedEvent>().Should().NotBeEmpty().And.OnlyContain(e => e.Outcome == FileOutcome.Simulated);
        recorder.Of<RunCompletedEvent>().Should().ContainSingle().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Simulate_ReportsTheReleaseTheRunStartsFrom()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);
        await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);
        ctx.AssertSuccess(true);

        var started = recorder.Of<RunStartedEvent>().Should().ContainSingle().Which;
        started.MigrationRunId.Should().Be(0, "Simulate writes no run row");
        started.FromRelease.Should().Be("Release_1.0", "a run without a run row still reports where the product stands");
    }

    [Fact]
    public async Task MigrateDown_WithoutRollbackFile_ReportsTheRecordAsSkipped()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .RemoveRollback("Release_4.0", "02_CreateTableH.sql")
            .WithRequireRollbackFile(false)
            .BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_3.0");
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        await ctx.MigrateDownAsync("Release_3.0");
        ctx.AssertSuccess(true);

        var ends = recorder.Of<FileCompletedEvent>();
        var skipped = ends.Where(e => e.Outcome == FileOutcome.Skipped).Should().ContainSingle("the record without a rollback file is skipped, not rolled back").Which;
        skipped.FileName.Should().Be("02_CreateTableH.sql");
        skipped.MigrationRecordId.Should().BePositive();
        skipped.ErrorMessage.Should().Contain("not found");
        ends.Where(e => e.Outcome == FileOutcome.RolledBack).Select(e => e.FileName).Should().BeEquivalentTo(["01_CreateTableG.sql", "03_SeedDataD.sql"]);
        recorder.Of<FileStartedEvent>().Select(s => (s.FileName, s.TargetAlias, s.MigrationRecordId))
            .Should().BeEquivalentTo(ends.Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId)), "every completion has a start, the skipped record included");
    }

    [Fact]
    public async Task MigrateDown_ReportsRollbacksOfTheRecordsAboveTheTargetRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        var result = await ctx.MigrateDownAsync("Release_2.0");
        ctx.AssertSuccess(true);

        recorder.Of<RunStartedEvent>().Should().ContainSingle().Which.Command.Should().Be(MigrationCommand.MigrateDown);
        var ends = recorder.Of<FileCompletedEvent>();
        ends.Should().HaveCount(result.TotalMigrations);
        ends.Should().OnlyContain(e => e.Operation == MigrationOperation.MigrateDown && e.Outcome == FileOutcome.RolledBack && e.MigrationRecordId > 0);
        ends.Select(e => e.ReleaseVersion).Distinct().Should().BeEquivalentTo(["Release_3.0", "Release_4.0"]);
        var starts = recorder.Of<FileStartedEvent>();
        starts.Select(s => (s.FileName, s.TargetAlias, s.MigrationRecordId))
            .Should().BeEquivalentTo(ends.Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId)), "every rollback completion has a start");
        starts.Should().OnlyContain(s => s.Operation == MigrationOperation.MigrateDown && s.BlocksTotal >= 1 && s.ResumeFromBlock == 0);
        var blocks = recorder.Of<BlockCompletedEvent>();
        blocks.Should().NotBeEmpty("the rollback files have SQL blocks");
        blocks.Select(b => b.FileName).Distinct().Should().BeEquivalentTo(starts.Select(s => s.FileName).Distinct());
        recorder.Of<RunCompletedEvent>().Should().ContainSingle().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Baseline_ReportsBaselinedFilesWithoutBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        var result = await ctx.BaselineAsync();
        ctx.AssertSuccess(true);

        recorder.Of<RunStartedEvent>().Should().ContainSingle().Which.Command.Should().Be(MigrationCommand.Baseline);
        var ends = recorder.Of<FileCompletedEvent>();
        ends.Should().HaveCount(result.BaselinedRecords, "one completion per record written")
            .And.OnlyContain(e => e.Operation == MigrationOperation.Baseline && e.Outcome == FileOutcome.Baselined && e.MigrationRecordId > 0);
        recorder.Of<FileStartedEvent>().Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId))
            .Should().BeEquivalentTo(ends.Select(e => (e.FileName, e.TargetAlias, e.MigrationRecordId)), "a start carries the record id as well");
        recorder.Of<BlockCompletedEvent>().Should().BeEmpty("baseline executes no SQL");
        recorder.Of<RunCompletedEvent>().Should().ContainSingle().Which.TotalFiles.Should().Be(result.BaselinedRecords);
    }

    [Fact]
    public async Task FailingFile_ReportsTheFailedFileAndTheFailedRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();
        var recorder = new RecordingProgressObserver();
        ctx.UseProgressObserver(recorder);

        var result = await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);

        var failed = recorder.Of<FileCompletedEvent>().Where(e => e.Outcome == FileOutcome.Failed).Should().ContainSingle().Which;
        failed.FileName.Should().Be("02_CreateTableD.sql");
        failed.ReleaseVersion.Should().Be("Release_2.0");
        failed.ErrorMessage.Should().NotBeNullOrEmpty();
        failed.MigrationRecordId.Should().BePositive();
        recorder.Of<FileCompletedEvent>().Where(e => e.Outcome == FileOutcome.Migrated).Should().NotBeEmpty("the files before the broken one ran");

        var completed = recorder.Of<RunCompletedEvent>().Should().ContainSingle().Which;
        completed.Success.Should().BeFalse();
        completed.Result.Should().Be(result.Result);
        completed.FailedFiles.Should().Be(result.FailedMigrations);
        recorder.Events.Last().Should().BeSameAs(completed, "the run completion is the last event");
    }

    [Fact]
    public async Task ObserverThatThrows_DoesNotAbortTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        ctx.UseProgressObserver(new ThrowingProgressObserver());

        await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
    }

    private sealed class ThrowingProgressObserver : IMigrationProgressObserver
    {
        public void OnRunStarted(RunStartedEvent e) => throw new InvalidOperationException("observer failure");
        public void OnFileStarted(FileStartedEvent e) => throw new InvalidOperationException("observer failure");
        public void OnBlockCompleted(BlockCompletedEvent e) => throw new InvalidOperationException("observer failure");
        public void OnFileCompleted(FileCompletedEvent e) => throw new InvalidOperationException("observer failure");
        public void OnRunCompleted(RunCompletedEvent e) => throw new InvalidOperationException("observer failure");
    }
}
