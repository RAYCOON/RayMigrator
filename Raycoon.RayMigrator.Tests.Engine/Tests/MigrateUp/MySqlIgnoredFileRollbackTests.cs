using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

/// <summary>
/// migrate-up on MySQL rolls back the files a run left Failed under MigrationErrorAction=Ignore with the run, when an error
/// action or a cancel action rolls the run back (<see cref="IgnoredFileRollbackContract"/>).
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "MigrateUp")]
public class MySqlIgnoredFileRollbackTests : MySqlTestBase
{
    public MySqlIgnoredFileRollbackTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_ErrorRecovery_RollsBackAFileLeftFailedUnderIgnore()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyErrorRecoveryRollsBackTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelActionRollbackRun_RollsBackAFileLeftFailedUnderIgnore()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyCancelActionRollsBackTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_CancelActionRollbackRelease_RollsBackTheIgnoredFilesOfItsReleaseOnly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyRollbackReleaseTakesTheIgnoredFilesOfItsReleaseAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_IgnoredFileWithoutACommittedBlock_IsRolledBackWithTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyIgnoredFileWithoutACommittedBlockIsRolledBackAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_FileByFileWithTwoTargets_RollsBackTheRecordOfTheTargetThatRanTheIgnoredFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyChainTakesTheRecordOfTheTargetThatRanTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig);
    }

    [Fact]
    public async Task MigrateUp_ErrorRecoveryAtAnIgnoredFileWithoutItsRollbackFile_StopsTheChain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyMissingRollbackFileOfTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig,
            stopRollbackOnMissingRollbackFile: true, cancelled: false);
    }

    [Fact]
    public async Task MigrateUp_ErrorRecoveryAtAnIgnoredFileWithoutItsRollbackFile_SkipsItWithAWarning()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyMissingRollbackFileOfTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig,
            stopRollbackOnMissingRollbackFile: false, cancelled: false);
    }

    [Fact]
    public async Task MigrateUp_CancelActionAtAnIgnoredFileWithoutItsRollbackFile_StopsTheChain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyMissingRollbackFileOfTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig,
            stopRollbackOnMissingRollbackFile: true, cancelled: true);
    }

    [Fact]
    public async Task MigrateUp_CancelActionAtAnIgnoredFileWithoutItsRollbackFile_SkipsItWithAWarning()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await IgnoredFileRollbackContract.VerifyMissingRollbackFileOfTheIgnoredFileAsync(CreateScenario(), Fixture.EngineConfig,
            stopRollbackOnMissingRollbackFile: false, cancelled: true);
    }
}
