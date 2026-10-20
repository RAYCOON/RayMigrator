using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The orphan repair on PostgreSQL: the open records of a run that ended abruptly keep the progress of their file when a
/// later start or fix closes the run (<see cref="OrphanRepairContract"/>).
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class PostgreSqlOrphanRepairTests : PostgreSqlTestBase
{
    public PostgreSqlOrphanRepairTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// The scenarios of this class run the repository sessions of the engine and of the test in a time zone other than UTC
    /// (Asia/Kathmandu, +05:45), so that the message of the repair, which names the last heartbeat in UTC, depends on the
    /// conversion of its template in every environment, also where the database server runs in UTC.
    /// </summary>
    private new ScenarioBuilder CreateScenario()
        => new(Fixture.EngineConfig with { ConnectionString = $"{Fixture.EngineConfig.ConnectionString};Timezone=Asia/Kathmandu" });

    [Fact]
    public async Task MigrateUp_AfterAnAbruptEndInAFile_ResumesAfterTheCommittedBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyAutoFixResumesAfterTheCommittedBlocksAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_WithoutStatus_KeepsTheCommittedBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyFixKeepsTheCommittedBlocksAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_WithAnExplicitStatus_SetsItOnTheOpenRecord()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyFixWithAnExplicitStatusSetsItAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_AfterAnAbruptEndWithoutProgress_RunsTheWholeFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyAutoFixWithoutProgressRunsTheWholeFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_AfterAnAbruptEndBehindTheLastBlock_FinishesTheFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyAutoFixFinishesAFileWithEveryBlockCommittedAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_FinishedRecord_CountsForTheLastMigration()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyRepairedRecordCountsForTheLastMigrationAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_AfterAnAbruptEndInARollback_RefusesTheFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyAutoFixKeepsAnIncompleteRollbackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_AfterAnAbruptEndBehindTheLastRollbackBlock_CompletesTheRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyFixCompletesAFinishedRollbackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_AfterAnAbruptEndInTheFirstRollbackBlock_FailsTheRecord()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyFixFailsARollbackWithoutProgressAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_AfterAnEndBetweenTheStepsOfTheRepairInARollback_RefusesTheFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyAnOpenRollbackOfAnEndedRunIsAnIncompleteRollbackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Info_RecordFinishedByTheRepair_CountsForTheCurrentRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyRecordFinishedByTheRepairCountsForTheCurrentReleaseAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task Fix_OpenRecordsInEveryState_SettlesThemAsTheProjectionOfTheStatus()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await OrphanRepairContract.VerifyTheRepairRuleMatchesTheProjectionAsync(CreateScenario(), Fixture.EngineConfig);
    }
}
