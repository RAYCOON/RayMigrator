using System.Text.Json;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerMigSettingsInheritanceTests : SqlServerTestBase
{
    public SqlServerMigSettingsInheritanceTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// MS1: Root-level migsettings.txt value should be inherited by all files.
    /// MigrationErrorAction is not set in file TOML, so migsettings should fill it in.
    /// </summary>
    [Fact]
    public async Task RootLevel_ShouldBeInheritedByAllFiles()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty("FileUpConfigJson should be stored for the migration");

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "MigrationErrorAction=Ignore from root migsettings.txt should be inherited");
    }

    /// <summary>
    /// MS2: Environment-specific migsettings.Docker.txt should override base migsettings.txt.
    /// </summary>
    [Fact]
    public async Task EnvironmentSpecific_ShouldOverrideBase()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Terminate"
            })
            .SetMigSettings("migsettings.Docker.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "MigrationErrorAction=Ignore from migsettings.Docker.txt should override base Terminate");
    }

    /// <summary>
    /// MS3: File-level TOML should override migsettings for the same property.
    /// </summary>
    [Fact]
    public async Task TomlMetadata_ShouldOverrideMigSettings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .SetFileToml("Release_1.0", "01_CreateTableA.sql",
                "MigrationErrorAction", "\"Terminate\"")
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Terminate",
            "MigrationErrorAction=Terminate from file TOML should override migsettings Ignore");
    }

    /// <summary>
    /// MS4: Release-level migsettings should override root-level migsettings.
    /// </summary>
    [Fact]
    public async Task ReleaseLevel_ShouldOverrideRoot()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Terminate"
            })
            .SetMigSettings("Release_2.0/migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // R1 file should inherit root-level value
        string? r1Json = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        r1Json.Should().NotBeNullOrEmpty();
        var r1Config = JsonDocument.Parse(r1Json!);
        r1Config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Terminate",
            "R1 file should get root-level MigrationErrorAction=Terminate");

        // R2 file should get release-level override
        string? r2Json = ctx.GetMigrationConfigJson("01_CreateTableC.sql");
        r2Json.Should().NotBeNullOrEmpty();
        var r2Config = JsonDocument.Parse(r2Json!);
        r2Config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "R2 file should get release-level MigrationErrorAction=Ignore override");
    }

    /// <summary>
    /// MS5: TargetGroup-level migsettings should override release-level migsettings.
    /// </summary>
    [Fact]
    public async Task TargetGroupLevel_ShouldOverrideRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("Release_1.0/migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Terminate"
            })
            .SetMigSettings("Release_1.0/Backend/migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "TargetGroup-level migsettings should override release-level");
    }

    /// <summary>
    /// MS6: Three-level cascade (root -> env-specific -> release) should resolve correctly.
    /// Root sets RunAlways (not in TOML but we override via migsettings), env overrides MigrationErrorAction,
    /// release overrides a different property.
    /// </summary>
    [Fact]
    public async Task ThreeLevelCascade_ShouldResolveCorrectly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Terminate",
                ["RequireRollbackFile"] = "false"
            })
            .SetMigSettings("migsettings.Docker.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .SetMigSettings("Release_2.0/migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Rollback"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // R1 file: Docker override wins (Ignore), RequireRollbackFile from root (false)
        string? r1Json = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        r1Json.Should().NotBeNullOrEmpty();
        var r1Config = JsonDocument.Parse(r1Json!);
        r1Config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "R1 file should get Docker-level override Ignore");
        r1Config.RootElement.GetProperty("RequireRollbackFile").GetBoolean().Should().BeFalse(
            "RequireRollbackFile=false from root should cascade to R1");

        // R2 file: Release-level override wins (Rollback)
        string? r2Json = ctx.GetMigrationConfigJson("01_CreateTableC.sql");
        r2Json.Should().NotBeNullOrEmpty();
        var r2Config = JsonDocument.Parse(r2Json!);
        r2Config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Rollback",
            "R2 file should get release-level MigrationErrorAction=Rollback");
    }

    /// <summary>
    /// MS7: Description from TOML should be stored in FileUpConfigJson (not inherited from migsettings).
    /// </summary>
    [Fact]
    public async Task DescriptionFromToml_ShouldBeStoredInConfigJson()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("Description").GetString().Should().Be("Create table TableA",
            "Description from file TOML should be stored in FileUpConfigJson");
    }

    /// <summary>
    /// MS8: UseTransaction=true from file TOML should not be overridden by migsettings UseTransaction=false.
    /// </summary>
    [Fact]
    public async Task TomlUseTransaction_ShouldNotBeOverriddenByMigSettings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["UseTransaction"] = "false"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        // 03_SeedDataA.sql TOML has UseTransaction=true, migsettings has false.
        // File TOML should win.
        string? configJson = ctx.GetMigrationConfigJson("03_SeedDataA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("UseTransaction").GetBoolean().Should().BeTrue(
            "UseTransaction=true from file TOML should not be overridden by migsettings.txt UseTransaction=false");
    }

    /// <summary>
    /// MS9: RunAlways=false from file TOML should not be overridden by migsettings RunAlways=true.
    /// </summary>
    [Fact]
    public async Task TomlRunAlways_ShouldNotBeOverriddenByMigSettings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["RunAlways"] = "true"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        // File TOML has RunAlways=false, migsettings has true.
        // File TOML should win.
        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("RunAlways").GetBoolean().Should().BeFalse(
            "RunAlways=false from file TOML should not be overridden by migsettings.txt RunAlways=true");
    }

    /// <summary>
    /// MS10: Environments=["*"] from file TOML should be preserved when migsettings sets Environments.
    /// </summary>
    [Fact]
    public async Task TomlEnvironments_ShouldNotBeOverriddenByMigSettings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["Environments"] = "[\"Production\"]"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        // File TOML has Environments=["*"], migsettings has ["Production"].
        // File TOML should win, so the file should still be executed (not filtered by Production).
        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty("File with Environments=[\"*\"] should not be filtered out");

        var config = JsonDocument.Parse(configJson!);
        var environments = config.RootElement.GetProperty("Environments");
        environments.ValueKind.Should().Be(JsonValueKind.Array);
        var envList = environments.EnumerateArray().Select(e => e.GetString()).ToList();
        envList.Should().Contain("*",
            "Environments=[\"*\"] from file TOML should be preserved, not overridden by migsettings");
    }

    /// <summary>
    /// MS11: RequireRollbackFile=false from migsettings should be inherited (not in file TOML).
    /// </summary>
    [Fact]
    public async Task RequireRollbackFileFalse_ShouldBeInherited()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRequireRollbackFile(false)
            .SetMigSettings("migsettings.txt", new Dictionary<string, string>
            {
                ["RequireRollbackFile"] = "false"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);

        string? configJson = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        configJson.Should().NotBeNullOrEmpty();

        var config = JsonDocument.Parse(configJson!);
        config.RootElement.GetProperty("RequireRollbackFile").GetBoolean().Should().BeFalse(
            "RequireRollbackFile=false from migsettings should be inherited");
    }

    /// <summary>
    /// MS12: MigsettingsEntry values should not bleed across releases.
    /// Only the release with the migsettings override should get the override.
    /// </summary>
    [Fact]
    public async Task MigSettings_ShouldNotBleedAcrossReleases()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("Release_1.0/Backend/migsettings.txt", new Dictionary<string, string>
            {
                ["MigrationErrorAction"] = "Ignore"
            })
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // R1 Backend file should have the override
        string? r1Json = ctx.GetMigrationConfigJson("01_CreateTableA.sql");
        r1Json.Should().NotBeNullOrEmpty();
        var r1Config = JsonDocument.Parse(r1Json!);
        r1Config.RootElement.GetProperty("MigrationErrorAction").GetString().Should().Be("Ignore",
            "R1 file should have MigrationErrorAction=Ignore from its Backend migsettings");

        // R2 Backend file should NOT have the override (different release directory)
        string? r2Json = ctx.GetMigrationConfigJson("01_CreateTableC.sql");
        r2Json.Should().NotBeNullOrEmpty();
        var r2Config = JsonDocument.Parse(r2Json!);
        // The R2 file should not have MigrationErrorAction=Ignore (no migsettings in R2)
        // It should be null (not set) since there is no migsettings at root or in R2
        var r2ErrorAction = r2Config.RootElement.GetProperty("MigrationErrorAction");
        r2ErrorAction.ValueKind.Should().Be(JsonValueKind.Null,
            "R2 file should not inherit MigrationErrorAction from R1's Backend migsettings");
    }

    /// <summary>
    /// MS13: RunAlways=true from migsettings should cause re-execution on second run
    /// when the file TOML does NOT have RunAlways set.
    /// We override the TOML to remove RunAlways, then set it via migsettings.
    /// </summary>
    [Fact]
    public async Task RunAlwaysFromMigSettings_ShouldCauseReExecution()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetMigSettings("Release_1.0/Backend/migsettings.txt", new Dictionary<string, string>
            {
                ["RunAlways"] = "true"
            })
            // Remove RunAlways from file TOML so migsettings can fill it
            .SetFileToml("Release_1.0", "03_SeedDataA.sql", "RunAlways", "# removed")
            .BuildAsync();

        // Replace the RunAlways line we just set to a comment with actually removing it
        // by writing the file without RunAlways
        string seedPath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "03_SeedDataA.sql");
        string content = File.ReadAllText(seedPath);
        // Remove the "RunAlways = # removed" line
        content = content.Replace("RunAlways = # removed\n", "").Replace("RunAlways = # removed\r\n", "");
        File.WriteAllText(seedPath, content);

        // Phase 1: MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);
        ctx.AssertRowCount("TableA", 3);

        // Phase 2: MigrateUp again -- R1/F3 should re-execute (RunAlways=true from migsettings)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result2 = await ctx.MigrateUpAsync(allowOutOfOrder: true);
        result2.Success.Should().BeTrue($"Second MigrateUp failed: {result2.ErrorMessage}");
        result2.SuccessfulMigrations.Should().BeGreaterThanOrEqualTo(1,
            "RunAlways=true from migsettings should cause at least one re-execution");

        // Seed data re-inserted: TableA should have 6 rows (3 from Run 1 + 3 from Run 2)
        ctx.AssertRowCount("TableA", 6);
    }
}
