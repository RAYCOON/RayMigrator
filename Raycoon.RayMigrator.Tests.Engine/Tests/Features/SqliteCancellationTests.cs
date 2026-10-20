using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// Cancellation on Sqlite: a cancelled run stops at the next block boundary and takes the action the host chose
/// (<see cref="CancellationContract"/>); schema step 3 records it. SQLite cannot interrupt a statement, so a block in
/// progress always runs to its end and has no in-flight test.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteCancellationTests : SqliteTestBase
{
    public SqliteCancellationTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_CancelledWithTerminate_LeavesTheFileResumableAtTheNextBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyTerminateInFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithTerminateInTargetByTargetOrder_LeavesTheFileResumable()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyTerminateInFileAsync(CreateScenario(), Fixture.EngineConfig, TargetMigrationOrder.TargetByTarget);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithRollbackFile_RollsBackTheInterruptedFileOnly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyRollbackFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithRollbackRun_RollsBackEveryFileOfTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyRollbackRunAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithRollbackFileAndNoRollbackFile_EndsAsError()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyRollbackFileWithoutRollbackFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledThroughTheTokenAlone_Terminates()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyBareTokenTerminatesAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledOnTheAtomicPath_RunsTheFileWholeNextTime()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyAtomicPathAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledBetweenFiles_StartsNoFurtherFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelBetweenFilesAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_Cancelled_StopsAtTheNextRollbackBlockAndContinuesNextTime()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyMigrateDownAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledDuringTheErrorRecovery_StopsTheRecoveryAndKeepsTheError()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelDuringErrorRecoveryAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledBeforeTheErrorRecoveryReachesItsFirstRecord_RollsNothingBackAndKeepsTheError()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelBeforeTheErrorRecoveryReachesItsFirstRecordAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Baseline_Cancelled_KeepsTheRecordsItWrote()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyBaselineAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task SchemaVersionTwo_IsUpgradedToVersionThreeByTheNextCommand()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifySchemaVersionTwoIsUpgradedToThreeAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task UpdateHash_CancelledBetweenTheRecordsOfAFile_CountsTheFileOfTheUpdatedRecord()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyUpdateHashCancelledBetweenTheRecordsOfAFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_AfterARollbackCancelledInTheMiddle_RefusesUntilMigrateDownCompletesIt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyMigrateUpRefusesAnIncompleteRollbackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_FileByFileCancelledBetweenTheTargetsOfAFile_CountsTheFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyFileByFileCancelBetweenTheTargetsOfAFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithRollbackRelease_RollsBackTheInterruptedReleaseOnly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyRollbackReleaseAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledBeforeTheRunStarts_WritesNothingAndHasNothingToRollBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelBeforeTheRunStartsAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledInSimulateAndValidateMode_PersistsNothing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelInSimulateAndValidateModePersistsNothingAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWhileACliToolRuns_KillsTheToolAndStops()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyCancelWhileACliToolRunsAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task SchemaVersionTwoWithData_IsUpgradedToVersionThreeAndKeepsItsRows()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifySchemaVersionTwoWithDataIsUpgradedToThreeAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_CancelledBetweenTwoOrphanedRuns_KeepsTheRunItClosed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyFixCancelledBetweenTwoRunsAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelledWithARollbackActionAndAMissingRequiredRollbackFile_KeepsTheStatusOfTheFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await CancellationContract.VerifyRollbackActionWithAMissingRequiredRollbackFileAsync(CreateScenario(), Fixture.EngineConfig);
    }
}
