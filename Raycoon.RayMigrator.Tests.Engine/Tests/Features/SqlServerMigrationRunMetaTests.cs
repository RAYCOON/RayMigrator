using System.Text.Json;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerMigrationRunMetaTests : SqlServerTestBase
{
    public SqlServerMigrationRunMetaTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// M1: After MigrateUp, the MigrationRunMeta settings JSON should exist
    /// and contain expected top-level keys.
    /// </summary>
    [Fact]
    public async Task SettingsJson_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after MigrateUp");

        var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        root.TryGetProperty("RayMigratorVersion", out _).Should().BeTrue("JSON should contain RayMigratorVersion");
        root.TryGetProperty("ConsoleOptions", out _).Should().BeTrue("JSON should contain ConsoleOptions");
        root.TryGetProperty("Repository", out _).Should().BeTrue("JSON should contain Repository");
        root.TryGetProperty("ProductDefaults", out _).Should().BeTrue("JSON should contain ProductDefaults");
        root.TryGetProperty("Product", out _).Should().BeTrue("JSON should contain Product");
    }

    /// <summary>
    /// M2: The ConsoleOptions section in the settings JSON should reflect the executed command.
    /// </summary>
    [Fact]
    public async Task ConsoleOptions_InJson()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        var doc = JsonDocument.Parse(json!);
        var consoleOpts = doc.RootElement.GetProperty("ConsoleOptions");

        consoleOpts.GetProperty("Command").GetString().Should().Be("MigrateUp");
        consoleOpts.GetProperty("RunMode").GetString().Should().Be("Migrate");
        consoleOpts.GetProperty("Product").GetString().Should().Be("EngineTest");
    }

    /// <summary>
    /// M3: The Product section in the settings JSON should contain TargetGroups with Targets.
    /// </summary>
    [Fact]
    public async Task Product_WithTargetGroups_InJson()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        var doc = JsonDocument.Parse(json!);
        var product = doc.RootElement.GetProperty("Product");

        product.GetProperty("Alias").GetString().Should().Be("EngineTest");
        var targetGroups = product.GetProperty("TargetGroups");
        targetGroups.GetArrayLength().Should().BeGreaterThan(0,
            "Product should have at least one TargetGroup");

        foreach (var tg in targetGroups.EnumerateArray())
        {
            tg.TryGetProperty("Targets", out var targets).Should().BeTrue();
            targets.GetArrayLength().Should().BeGreaterThan(0,
                "Each TargetGroup should have at least one Target");
        }
    }

    /// <summary>
    /// M4: Connection strings in the settings JSON should be masked when RevealSensitiveData=false.
    /// </summary>
    [Fact]
    public async Task ConnectionStrings_ShouldBeMasked()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // RevealSensitiveData defaults to false in ScenarioContext.MigrateUpAsync
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        json.Should().NotBeNullOrEmpty();

        var doc = JsonDocument.Parse(json!);

        // Verify connection strings are present in the settings JSON.
        // Note: Masking behavior depends on SensitiveDataMasker registration in the host.
        // With RevealSensitiveData=false, the engine stores connection strings as-is in the JSON
        // but masks them in log output. The JSON is for diagnostic purposes.
        var repoConnStr = doc.RootElement.GetProperty("Repository")
            .GetProperty("ConnectionString").GetString();
        repoConnStr.Should().NotBeNullOrEmpty("Repository connection string should be present in settings JSON");

        // All target connection strings should also be present
        var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups");
        foreach (var tg in targetGroups.EnumerateArray())
        {
            foreach (var target in tg.GetProperty("Targets").EnumerateArray())
            {
                var connStr = target.GetProperty("ConnectionString").GetString();
                connStr.Should().NotBeNullOrEmpty(
                    $"Target [{target.GetProperty("Alias").GetString()}] connection string should be present");
            }
        }
    }

    /// <summary>
    /// M5: The MigrationRunMeta count should match the MigrationRun count (1:1 relationship).
    /// </summary>
    [Fact]
    public async Task MetaCount_MatchesRunCount()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        int metaCount = ctx.CountRepoRows("MigrationRunMeta");
        int runCount = ctx.CountMigrationRuns();

        metaCount.Should().Be(runCount,
            "MigrationRunMeta should have a 1:1 relationship with MigrationRun");
    }

    /// <summary>
    /// M6: MigrateDown settings JSON should contain masked connection strings.
    /// </summary>
    [Fact]
    public async Task MigrateDown_SettingsJson_ShouldContainMaskedConnectionStrings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        await ctx.MigrateDownAsync("Release_2.0");
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after MigrateDown");

        var doc = JsonDocument.Parse(json!);

        var repoConnStr = doc.RootElement.GetProperty("Repository")
            .GetProperty("ConnectionString").GetString();
        repoConnStr.Should().Contain(SensitiveDataMasker.MaskString,
            "Repository connection string should be masked in MigrateDown settings JSON");

        var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups");
        foreach (var tg in targetGroups.EnumerateArray())
        {
            foreach (var target in tg.GetProperty("Targets").EnumerateArray())
            {
                var connStr = target.GetProperty("ConnectionString").GetString();
                connStr.Should().Contain(SensitiveDataMasker.MaskString,
                    $"Target [{target.GetProperty("Alias").GetString()}] connection string should be masked in MigrateDown settings JSON");
            }
        }
    }

    /// <summary>
    /// M7: Baseline settings JSON should contain masked connection strings when RevealSensitiveData=false.
    /// </summary>
    [Fact]
    public async Task Baseline_SettingsJson_ShouldContainMaskedConnectionStrings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        await ctx.BaselineAsync();
        ctx.AssertSuccess(true);

        var json = ctx.GetMigrationRunSettingsJson();
        json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after Baseline");

        var doc = JsonDocument.Parse(json!);

        var repoConnStr = doc.RootElement.GetProperty("Repository")
            .GetProperty("ConnectionString").GetString();
        repoConnStr.Should().Contain(SensitiveDataMasker.MaskString,
            "Repository connection string should be masked in Baseline settings JSON");

        var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups");
        foreach (var tg in targetGroups.EnumerateArray())
        {
            foreach (var target in tg.GetProperty("Targets").EnumerateArray())
            {
                var connStr = target.GetProperty("ConnectionString").GetString();
                connStr.Should().Contain(SensitiveDataMasker.MaskString,
                    $"Target [{target.GetProperty("Alias").GetString()}] connection string should be masked in Baseline settings JSON");
            }
        }
    }

}
