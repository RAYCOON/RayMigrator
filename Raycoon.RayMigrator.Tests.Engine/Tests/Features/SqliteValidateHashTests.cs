
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteValidateHashTests : SqliteTestBase
{
    public SqliteValidateHashTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ValidateHash_SqlBlocksScope_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        var migrateResult = await ctx.MigrateUpAsync("Release_2.0");
        migrateResult.Success.Should().BeTrue($"Migration should succeed: {migrateResult.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.SqlBlocks);
        result.Success.Should().BeTrue($"Hash validation with SqlBlocks scope failed: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes with SqlBlocks scope");
    }

    [Fact]
    public async Task ValidateHash_AfterBaseline_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var baselineResult = await ctx.BaselineAsync("Release_2.0");
        baselineResult.Success.Should().BeTrue($"Baseline should succeed: {baselineResult.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();
        result.Success.Should().BeTrue($"Hash validation after baseline failed: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ValidateHash_NoInvalidOrMissing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();
        result.Success.Should().BeTrue($"Hash validation should succeed: {result.ErrorMessage}");
        result.TotalFiles.Should().BeGreaterThan(0, "TotalFiles should be > 0 after migration");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes");
        result.MissingFiles.Should().Be(0, "No files should be missing");
    }

    [Fact]
    public async Task ValidateHash_NewIssuesOnlyFromUnmigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();
        var nonNewIssues = result.Issues.Where(i => i.IssueType != "New").ToList();
        nonNewIssues.Should().BeEmpty(
            "Only 'New' issues (unmigrated R3+R4 files) are expected, not invalid/missing ones");
    }

    [Fact]
    public async Task ValidateHash_AfterFileModification_ShouldDetect()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + Environment.NewLine + "-- hash-breaking modification");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();
        result.InvalidFiles.Should().BeGreaterThan(0,
            "At least one file should be detected as invalid after modification");
        var modifiedIssues = result.Issues.Where(i => i.IssueType == "Modified").ToList();
        modifiedIssues.Should().NotBeEmpty("The modified file should appear as a 'Modified' issue");
    }

    [Fact]
    public async Task ValidateHash_FileScope_ShouldAlsoPass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.File);
        result.Success.Should().BeTrue($"Hash validation with File scope failed: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes with File scope");
    }

    [Fact]
    public async Task ValidateHash_DisabledScope_IgnoresModification()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + Environment.NewLine + "-- hash-breaking modification");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.Disabled);
        result.Success.Should().BeTrue(
            $"Disabled scope should ignore modifications: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ValidateHash_DisabledScope_AfterFullMigration_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.Disabled);
        result.Success.Should().BeTrue(
            $"Disabled scope after full migration should pass: {result.ErrorMessage}");
    }
}
