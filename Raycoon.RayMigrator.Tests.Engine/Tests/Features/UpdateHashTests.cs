using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class UpdateHashTests : PostgreSqlTestBase
{
    public UpdateHashTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// U1: After a fresh migration, UpdateHash should find no files to update (all hashes match).
    /// </summary>
    [Fact]
    public async Task UpdateHash_AfterMigration_NoUpdatesNeeded()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}");
        result.UpdatedFiles.Should().Be(0, "Fresh migration, all hashes should match");
    }

    /// <summary>
    /// U2: After modifying a migrated file on disk, UpdateHash should update at least one hash.
    /// </summary>
    [Fact]
    public async Task UpdateHash_AfterFileModification_ShouldUpdateHash()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Modify a migrated file on disk to break its hash
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + "\n-- modified for hash test");

        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue($"UpdateHash should succeed: {result.ErrorMessage}");
        result.UpdatedFiles.Should().BeGreaterThanOrEqualTo(1,
            "At least one file hash should have been updated after modification");
    }

    /// <summary>
    /// U3: After modifying a file and running UpdateHash, a subsequent ValidateHash should pass
    /// (proving that UpdateHash actually corrected the stored hash).
    /// </summary>
    [Fact]
    public async Task UpdateHash_AfterModification_ThenValidateHash_ShouldPass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Modify a migrated file on disk to break its hash
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + "\n-- modified for hash test");

        // UpdateHash should fix the stored hash
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var updateResult = await ctx.UpdateHashAsync();
        updateResult.Success.Should().BeTrue($"UpdateHash should succeed: {updateResult.ErrorMessage}");

        // ValidateHash should now pass (no invalid files)
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var validateResult = await ctx.ValidateHashAsync();

        validateResult.Success.Should().BeTrue(
            $"ValidateHash after UpdateHash should pass: {validateResult.ErrorMessage}");
        validateResult.InvalidFiles.Should().Be(0,
            "No files should have invalid hashes after UpdateHash fixed them");
    }

    /// <summary>
    /// U4: UpdateHash is idempotent — running it a second time after a modification should find
    /// zero files to update because the first run already corrected the hashes.
    /// </summary>
    [Fact]
    public async Task UpdateHash_Idempotent_SecondRunNoUpdates()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Modify a migrated file on disk to break its hash
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + "\n-- modified for hash test");

        // First UpdateHash — should update at least one file
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var firstResult = await ctx.UpdateHashAsync();
        firstResult.Success.Should().BeTrue($"First UpdateHash should succeed: {firstResult.ErrorMessage}");
        firstResult.UpdatedFiles.Should().BeGreaterThanOrEqualTo(1,
            "First UpdateHash should update at least one file");

        // Second UpdateHash — should find zero files to update (idempotent)
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var secondResult = await ctx.UpdateHashAsync();
        secondResult.Success.Should().BeTrue($"Second UpdateHash should succeed: {secondResult.ErrorMessage}");
        secondResult.UpdatedFiles.Should().Be(0,
            "Second UpdateHash should find zero files to update (idempotent)");
    }

    /// <summary>
    /// U5: UpdateHash on an empty repository (no migrations executed) should succeed gracefully.
    /// </summary>
    [Fact]
    public async Task UpdateHash_OnEmptyRepository_ShouldSucceedGracefully()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.UpdateHash);

        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue(
            $"UpdateHash should handle empty repository gracefully: {result.ErrorMessage}");
        result.UpdatedFiles.Should().Be(0,
            "No files should be updated when the repository is empty");
    }
}
