using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The retention of history and log rows on SQLite (ADR-027): the repository maintenance service deletes the rows older
/// than a cutoff in batches and spares the rows of open runs (<see cref="RepositoryMaintenanceContract"/>). SQLite cannot interrupt a statement; the token stops the cleanup before the next batch.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteRepositoryMaintenanceTests : SqliteTestBase
{
    public SqliteRepositoryMaintenanceTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CleanUp_CutoffInTheFuture_DeletesHistoryAndLogButKeepsRunsAndRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyFutureCutoffDeletesHistoryAndLogAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_OpenRun_KeepsItsRowsUntilItIsClosed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyOpenRunKeepsItsRowsAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_LogRowsWithoutRun_FallByTheirAge()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyLogRowsWithoutRunFallByAgeAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_CutoffBetweenTheRuns_DeletesTheRowsOfTheOlderRunOnly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyCutoffBetweenTheRunsDeletesTheOlderRowsAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_DryRun_CountsTheRowsTheCleanupDeletesAndDeletesNothing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyDryRunCountsWithoutDeletingAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_BatchSizeOne_TakesOneBatchPerRow()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyBatchSizeOneDeletesRowByRowAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_TimeBudgetZero_StopsAfterOneBatchAndTheNextCallFinishes()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyZeroTimeBudgetStopsAfterOneBatchAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_RepositoryWithoutLogTables_SkipsTheLog()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyRepositoryWithoutLogTablesSkipsTheLogAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_LogInADatabaseOfItsOwn_IsSkippedAndLeftAlone()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyLogInADatabaseOfItsOwnIsLeftAloneAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task CleanUp_LongestTablePrefix_CleansTheTablesOfThePrefix()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyFutureCutoffDeletesHistoryAndLogAsync(
            CreateScenario().WithTableBaseName(DatabaseLogIndexContract.LongestPrefix));
    }

    [Fact]
    public async Task CleanUp_CancelledBetweenTwoBatches_ReturnsTheRowsDeletedSoFar()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyCancelBetweenBatchesAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_FailsAfterADeletedBatch_ReturnsWhatWasDeleted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyFailureAfterADeletedBatchReturnsWhatWasDeletedAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_StoppedEarly_HasDeletedTheOldestRows()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyPartialCleanupDeletesTheOldestRowsFirstAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_CancelledInADryRun_Throws()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyCancelInADryRunThrowsAsync(CreateScenario());
    }

    [Fact]
    public async Task CleanUp_CancelledAtTheCheckOfTheLog_ReturnsTheDeletedHistory()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositoryMaintenanceContract.VerifyCancelAtTheCheckOfTheLogReturnsTheDeletedHistoryAsync(CreateScenario());
    }
}
