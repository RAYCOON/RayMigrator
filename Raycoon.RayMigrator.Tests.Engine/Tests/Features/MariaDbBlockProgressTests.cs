using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The block counters of a migration record on MariaDB: they count the leading blocks that committed at every moment of a
/// run (<see cref="BlockProgressContract"/>).
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbBlockProgressTests : MariaDbTestBase
{
    public MariaDbBlockProgressTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_FileOfACliTool_CountsNoCommittedBlockWhileTheToolRuns()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyCliToolFileCountsNoCommittedBlockAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_IgnoredBlockFailure_EndsTheCount()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyIgnoredBlockFailureEndsTheCountAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledAfterAnIgnoredBlockFailure_ResumesAtTheFailedBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyCancelAfterAnIgnoredBlockFailureKeepsTheLeadingBlocksAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_FileEndedWithAnIgnoredBlockFailure_ResumesAtTheFailedBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyIgnoredFailureResumesAtTheFailedBlockAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_FileEndedWithAnIgnoredFailureOfTheFirstBlock_RunsTheWholeFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyIgnoredFailureOfTheFirstBlockRunsTheWholeFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_IgnoredRollbackBlockFailure_EndsTheCount()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyIgnoredRollbackBlockFailureEndsTheCountAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_ResumedFile_CountsItsBlocksFromTheStart()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyResumedFileCountsItsBlocksFromTheStartAsync(CreateScenario(), Fixture.EngineConfig, TargetMigrationOrder.FileByFile);
    }

    [Fact]
    public async Task MigrateUp_ResumedFileInTargetByTargetOrder_CountsItsBlocksFromTheStart()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyResumedFileCountsItsBlocksFromTheStartAsync(CreateScenario(), Fixture.EngineConfig, TargetMigrationOrder.TargetByTarget);
    }

    [Fact]
    public async Task MigrateDown_ContinuedRollback_CountsItsBlocksFromTheStart()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyContinuedRollbackCountsItsBlocksFromTheStartAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_CancelledAfterAnIgnoredRollbackBlockFailure_ContinuesAtTheFailedBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyCancelAfterAnIgnoredRollbackBlockFailureKeepsTheLeadingRollbackBlocksAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FailedAtomicRollback_KeepsTheRollbackBlocksOfTheEarlierAttempt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyFailedAtomicRollbackKeepsTheRollbackBlocksOfTheEarlierAttemptAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FailedRollbackThroughACliTool_EndsFailed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyFailedRollbackThroughACliToolEndsFailedAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_FileOfACliTool_DoesNotResumeAnEarlierAttempt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyCliToolFileDoesNotResumeAnEarlierAttemptAsync(CreateScenario(), Fixture.EngineConfig, TargetMigrationOrder.FileByFile);
    }

    [Fact]
    public async Task MigrateUp_FileOfACliToolInTargetByTargetOrder_DoesNotResumeAnEarlierAttempt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyCliToolFileDoesNotResumeAnEarlierAttemptAsync(CreateScenario(), Fixture.EngineConfig, TargetMigrationOrder.TargetByTarget);
    }

    [Fact]
    public async Task MigrateDown_RollbackThroughACliTool_DoesNotContinueAnEarlierAttempt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyRollbackThroughACliToolDoesNotContinueAnEarlierAttemptAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FailedProgressWriteAfterARollbackBlock_CountsTheBlockThatRan()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyFailedRollbackProgressWriteCountsTheBlockThatRanAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_RollbackThroughACliToolOnOneTarget_ReportsOneBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyRollbackThroughACliToolReportsOneBlockAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_RollbackThroughACliToolThatClosesItsInput_EndsFailed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyRollbackThroughACliToolThatClosesItsInputAsync(CreateScenario(), Fixture.EngineConfig, RollbackErrorAction.Terminate);
    }

    [Fact]
    public async Task MigrateDown_RollbackThroughACliToolThatClosesItsInputWithIgnore_GoesOnWithTheNextFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await BlockProgressContract.VerifyRollbackThroughACliToolThatClosesItsInputAsync(CreateScenario(), Fixture.EngineConfig, RollbackErrorAction.Ignore);
    }
}
