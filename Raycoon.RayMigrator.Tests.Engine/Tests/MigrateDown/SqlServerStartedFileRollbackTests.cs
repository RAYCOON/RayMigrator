using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateDown;

/// <summary>
/// migrate-down on SQL Server rolls back a file that stopped after committed blocks, like a Migrated file, and names a
/// file that stopped without one (<see cref="StartedFileRollbackContract"/>).
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateDown")]
public class SqlServerStartedFileRollbackTests : SqlServerTestBase
{
    public SqlServerStartedFileRollbackTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateDown_FileCancelledAfterACommittedBlock_IsRolledBackFirst()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyCancelledFileIsRolledBackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FileFailedAfterACommittedBlock_IsRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyFailedFileIsRolledBackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FileEndedWithAnIgnoredBlockFailure_IsRolledBackWhole()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyFileWithAnIgnoredFailureIsRolledBackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FileKeptByTheOrphanRepair_IsRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyFileKeptByTheOrphanRepairIsRolledBackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FileWithoutACommittedBlock_IsNamedInAWarning()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyFileWithoutACommittedBlockIsNamedAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_FailedRollbackOfAStoppedFile_StopsInTheMiddle()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyFailedRollbackOfTheFileStopsInTheMiddleAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateDown_StoppedFileWithoutItsRequiredRollbackFile_AbortsAndKeepsItsBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyMissingRollbackFileKeepsTheCommittedBlocksAsync(CreateScenario(), Fixture.EngineConfig, requireRollbackFile: true);
    }

    [Fact]
    public async Task MigrateDown_StoppedFileWithoutItsRollbackFile_GoesOnAndKeepsItsBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifyMissingRollbackFileKeepsTheCommittedBlocksAsync(CreateScenario(), Fixture.EngineConfig, requireRollbackFile: false);
    }

    [Fact]
    public async Task MigrateDown_SimulateWithAStoppedFile_ListsItsRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await StartedFileRollbackContract.VerifySimulateListsTheRollbackOfTheFileAsync(CreateScenario(), Fixture.EngineConfig);
    }
}
