using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlUpdateHashTests : MySqlTestBase
{
    public MySqlUpdateHashTests(MySqlFixture fixture) : base(fixture) { }

    [Fact] public async Task UpdateHash_AfterMigration_NoUpdatesNeeded() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var result = await ctx.UpdateHashAsync(); result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}"); result.UpdatedFiles.Should().Be(0, "Fresh migration, all hashes should match"); }

    [Fact] public async Task UpdateHash_AfterFileModification_ShouldUpdateHash() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"); string content = File.ReadAllText(filePath); File.WriteAllText(filePath, content + "\n-- modified for hash test"); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var result = await ctx.UpdateHashAsync(); result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}"); result.UpdatedFiles.Should().BeGreaterThanOrEqualTo(1, "At least one file hash should have been updated after modification"); }

    [Fact] public async Task UpdateHash_AfterModification_ThenValidateHash_ShouldPass() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"); string content = File.ReadAllText(filePath); File.WriteAllText(filePath, content + "\n-- modified for hash test"); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var updateResult = await ctx.UpdateHashAsync(); updateResult.Success.Should().BeTrue($"UpdateHash should succeed: {updateResult.ErrorMessage}"); await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate); var validateResult = await ctx.ValidateHashAsync(); validateResult.Success.Should().BeTrue($"ValidateHash after UpdateHash should pass: {validateResult.ErrorMessage}"); validateResult.InvalidFiles.Should().Be(0, "No files should have invalid hashes after UpdateHash fixed them"); }

    [Fact] public async Task UpdateHash_Idempotent_SecondRunNoUpdates() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"); string content = File.ReadAllText(filePath); File.WriteAllText(filePath, content + "\n-- modified for hash test"); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var firstResult = await ctx.UpdateHashAsync(); firstResult.Success.Should().BeTrue($"First UpdateHash should succeed: {firstResult.ErrorMessage}"); firstResult.UpdatedFiles.Should().BeGreaterThanOrEqualTo(1, "First UpdateHash should update at least one file"); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var secondResult = await ctx.UpdateHashAsync(); secondResult.Success.Should().BeTrue($"Second UpdateHash should succeed: {secondResult.ErrorMessage}"); secondResult.UpdatedFiles.Should().Be(0, "Second UpdateHash should find zero files to update (idempotent)"); }

    [Fact] public async Task UpdateHash_OnEmptyRepository_ShouldSucceedGracefully() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.UpdateHash); var result = await ctx.UpdateHashAsync(); result.Success.Should().BeTrue($"UpdateHash should handle empty repository gracefully: {result.ErrorMessage}"); result.UpdatedFiles.Should().Be(0, "No files should be updated when the repository is empty"); }

    #region update-hash converges for files without a TOML block and updates every target's record (#9)

    /// <summary>
    /// #9: a file without a TOML block has a null config hash while the repository stores "", so update-hash
    /// reported and wrote an update for every such file on every run. All engine fixtures carry a TOML block,
    /// which is why the existing idempotency tests never saw it; this variant strips the headers of Release_1.0.
    /// </summary>
    [Fact]
    public async Task UpdateHash_FilesWithoutTomlHeader_AfterMigration_NoUpdatesNeeded()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().WithoutTomlHeaders("Release_1.0").BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}");
        result.UpdatedFiles.Should().Be(0, "nothing changed on disk; a missing TOML block must not count as a changed config hash (#9)");
        result.UpdatedRecords.Should().Be(0);
        result.NewFiles.Should().Be(9, "the files of Release_2.0 to Release_4.0 are not migrated yet; the three Release_1.0 files are unchanged, not new");
        result.UpdatedFileNames.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateHash_FilesWithoutTomlHeader_Idempotent_SecondRunNoUpdates()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().WithoutTomlHeaders("Release_1.0").BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        File.WriteAllText(filePath, File.ReadAllText(filePath) + Environment.NewLine + "-- modified for hash test");

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var first = await ctx.UpdateHashAsync();
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var second = await ctx.UpdateHashAsync();

        first.Success.Should().BeTrue($"First UpdateHash should succeed: {first.ErrorMessage}");
        first.UpdatedFiles.Should().Be(1, "exactly the modified file was updated");
        second.UpdatedFiles.Should().Be(0, "the second run must find nothing to update (#9)");
        second.UpdatedRecords.Should().Be(0);
    }

    /// <summary>
    /// #9: with two targets there is one Migrated record per target; update-hash used to update only the
    /// first one, leaving the other target's record stale so that the next migrate-up depended on row order.
    /// </summary>
    [Fact]
    public async Task UpdateHash_TwoTargets_UpdatesTheRecordOfEveryTarget()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.CountMigrations().Should().Be(6, "3 files x 2 targets");
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        File.WriteAllText(filePath, File.ReadAllText(filePath) + Environment.NewLine + "-- approved change");

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}");
        result.UpdatedFiles.Should().Be(1);
        result.UpdatedRecords.Should().Be(2, "one record per target must be updated (#9)");
        result.UpdatedFileNames.Should().ContainSingle("a file with two stale records is listed once")
            .Which.Should().Be("01_CreateTableA.sql");

        // Both targets are now in sync with the file: nothing pending, hashes valid, second update-hash is a no-op.
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        (await ctx.InfoAsync()).PendingMigrations.Should().Be(18,
            "only the 9 files of Release_2.0 to Release_4.0 are pending on both targets; the updated Release_1.0 file is not pending on either target");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var validation = await ctx.ValidateHashAsync();
        validation.InvalidFiles.Should().Be(0);
        validation.ValidFiles.Should().Be(3);
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        (await ctx.UpdateHashAsync()).UpdatedRecords.Should().Be(0);
    }

    [Fact]
    public async Task UpdateHash_TwoTargets_DeletedFile_IsCountedRemovedOnce()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        File.Delete(Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"));

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}");
        result.RemovedFiles.Should().Be(1, "one file was deleted although it has two records (#9)");
        result.UpdatedFiles.Should().Be(0);
        result.UpdatedRecords.Should().Be(0);
    }

    #endregion
}
