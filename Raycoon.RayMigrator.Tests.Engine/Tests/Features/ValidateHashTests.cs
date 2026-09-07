using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class ValidateHashTests : PostgreSqlTestBase
{
    public ValidateHashTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// V1: MigrateUp R1+R2, then ValidateHash with SqlBlocks scope should pass with no invalid files.
    /// </summary>
    [Fact]
    public async Task ValidateHash_SqlBlocksScope_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        var migrateResult = await ctx.MigrateUpAsync("Release_2.0");
        migrateResult.Success.Should().BeTrue($"Migration should succeed: {migrateResult.ErrorMessage}");

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.SqlBlocks);

        result.Success.Should().BeTrue($"Hash validation with SqlBlocks scope failed: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes with SqlBlocks scope");
    }

    /// <summary>
    /// V2: Baseline R1+R2 then ValidateHash should pass.
    /// </summary>
    [Fact]
    public async Task ValidateHash_AfterBaseline_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var baselineResult = await ctx.BaselineAsync("Release_2.0");
        baselineResult.Success.Should().BeTrue($"Baseline should succeed: {baselineResult.ErrorMessage}");

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();

        result.Success.Should().BeTrue($"Hash validation after baseline failed: {result.ErrorMessage}");
    }

    /// <summary>
    /// V3: MigrateUp all then ValidateHash should have TotalFiles > 0, InvalidFiles == 0, MissingFiles == 0.
    /// </summary>
    [Fact]
    public async Task ValidateHash_NoInvalidOrMissing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();

        result.Success.Should().BeTrue($"Hash validation should succeed: {result.ErrorMessage}");
        result.TotalFiles.Should().BeGreaterThan(0, "TotalFiles should be > 0 after migration");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes");
        result.MissingFiles.Should().Be(0, "No files should be missing");
    }

    /// <summary>
    /// V4: MigrateUp R1+R2 then ValidateHash all should only show "New" issues for unmigrated R3+R4 files.
    /// </summary>
    [Fact]
    public async Task ValidateHash_NewIssuesOnlyFromUnmigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();

        var nonNewIssues = result.Issues.Where(i => i.IssueType != "New").ToList();
        nonNewIssues.Should().BeEmpty(
            "Only 'New' issues (unmigrated R3+R4 files) are expected, not invalid/missing ones");
    }

    /// <summary>
    /// V5: MigrateUp then modify a migration file on disk, ValidateHash should detect the modification.
    /// </summary>
    [Fact]
    public async Task ValidateHash_AfterFileModification_ShouldDetect()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Modify a migrated file on disk to break its hash
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

    /// <summary>
    /// V6: MigrateUp then ValidateHash with File scope should also pass (different scope than SqlBlocks).
    /// </summary>
    [Fact]
    public async Task ValidateHash_FileScope_ShouldAlsoPass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.File);

        result.Success.Should().BeTrue($"Hash validation with File scope failed: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0, "No files should have invalid hashes with File scope");
    }

    /// <summary>
    /// V7: Disabled scope should ignore file modifications and return success.
    /// </summary>
    [Fact]
    public async Task ValidateHash_DisabledScope_IgnoresModification()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Modify a migrated file on disk to break its hash
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        string content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content + Environment.NewLine + "-- hash-breaking modification");

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.Disabled);

        result.Success.Should().BeTrue(
            $"Disabled scope should ignore modifications: {result.ErrorMessage}");
    }

    /// <summary>
    /// V8: Disabled scope after full migration should pass without checking hashes.
    /// </summary>
    [Fact]
    public async Task ValidateHash_DisabledScope_AfterFullMigration_Pass()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(HashValidationScope.Disabled);

        result.Success.Should().BeTrue(
            $"Disabled scope after full migration should pass: {result.ErrorMessage}");
    }

    #region validate-hash in the run mode the CLI uses (#5)

    /// <summary>
    /// #5: The CLI runs validate-hash in RunMode Validate. The repository query used that run mode
    /// as filter and never found a record, so every file was "New" and modifications or missing
    /// files went undetected. These tests build the host like the CLI does and fail on pre-#5 code.
    /// </summary>
    [Fact]
    public async Task ValidateHash_InValidateRunMode_UnchangedFiles_AreValid()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var result = await ctx.ValidateHashAsync();
        result.Success.Should().BeTrue($"Hash validation should succeed: {result.ErrorMessage}");
        result.TotalFiles.Should().BeGreaterThan(0, "the migrated files must be found on disk");
        result.ValidFiles.Should().Be(result.TotalFiles,
            "every migrated, unchanged file must be reported as valid, not as 'New' (#5)");
        result.InvalidFiles.Should().Be(0);
        result.MissingFiles.Should().Be(0);
        result.Issues.Should().BeEmpty("an unchanged repository has no hash issues");
    }

    [Fact]
    public async Task ValidateHash_InValidateRunMode_ModifiedFile_IsReportedAsModified()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        File.WriteAllText(filePath, File.ReadAllText(filePath) + Environment.NewLine + "-- hash-breaking modification");
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var result = await ctx.ValidateHashAsync();
        result.ErrorMessage.Should().BeNull("the command itself must not fail");
        result.Success.Should().BeFalse("Success means 'no hash issues', and one file was modified");
        result.InvalidFiles.Should().Be(1, "exactly one file was modified after migration");
        result.Issues.Should().ContainSingle(i => i.IssueType == "Modified" && i.FileName == "01_CreateTableA.sql");
        result.Issues.Should().NotContain(i => i.IssueType == "New",
            "migrated files must not be mistaken for new files (#5)");
    }

    [Fact]
    public async Task ValidateHash_InValidateRunMode_DeletedFile_IsReportedAsMissing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        File.Delete(Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"));
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var result = await ctx.ValidateHashAsync();
        result.ErrorMessage.Should().BeNull("the command itself must not fail");
        result.Success.Should().BeFalse("Success means 'no hash issues', and one migrated file is missing");
        result.MissingFiles.Should().Be(1, "exactly one migrated file was deleted from disk");
        result.Issues.Should().ContainSingle(i => i.IssueType == "Missing" && i.FileName == "01_CreateTableA.sql");
    }

    [Fact]
    public async Task ValidateHash_InValidateRunMode_UnmigratedFiles_AreReportedAsNewOnly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var result = await ctx.ValidateHashAsync();
        result.Success.Should().BeTrue($"Hash validation should succeed: {result.ErrorMessage}");
        result.ValidFiles.Should().BeGreaterThan(0, "the Release_1.0 files are migrated and unchanged");
        result.Issues.Should().NotBeEmpty("files of later releases are not migrated yet");
        result.Issues.Should().OnlyContain(i => i.IssueType == "New",
            "only unmigrated files may be reported, and only as 'New'");
        result.InvalidFiles.Should().Be(0);
        result.MissingFiles.Should().Be(0);
    }

    #endregion
}
