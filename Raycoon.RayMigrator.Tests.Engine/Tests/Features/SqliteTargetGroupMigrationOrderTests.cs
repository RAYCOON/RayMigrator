
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Database", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteTargetGroupMigrationOrderTests : SqliteTestBase
{
    public SqliteTargetGroupMigrationOrderTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>
    /// Copies Sqlite_Frontend migration files into the work directory so the engine
    /// discovers Frontend files alongside the Backend files already copied by ScenarioBuilder.
    /// </summary>
    private static void CopyFrontendFiles(string workDir)
    {
        string frontendBase = Path.Combine(
            AppContext.BaseDirectory, "MigrationFiles", "Sqlite_Frontend");
        foreach (string srcFile in Directory.GetFiles(frontendBase, "*.*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(frontendBase, srcFile);
            string dstFile = Path.Combine(workDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
            File.Copy(srcFile, dstFile, true);
        }
    }

    /// <summary>
    /// TGO1: Without TargetGroupMigrationOrder, config array order is used (Backend first, then Frontend).
    /// Repository records for Backend should have lower Ids than Frontend records.
    /// </summary>
    [Fact]
    public async Task MigrateUp_DefaultOrder_BackendBeforeFrontend()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeTrue($"MigrateUp should succeed: {result.ErrorMessage}");
        ctx.AssertTargetGroupMigrationOrder("Backend", "Frontend");
    }

    /// <summary>
    /// TGO2: With TargetGroupMigrationOrder = ["Frontend", "Backend"] via CLI (request parameter),
    /// Frontend records should have lower Ids than Backend records.
    /// </summary>
    [Fact]
    public async Task MigrateUp_CliOrder_FrontendBeforeBackend()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Frontend", "Backend"]);

        result.Success.Should().BeTrue($"MigrateUp with Frontend-first order should succeed: {result.ErrorMessage}");
        ctx.AssertTargetGroupMigrationOrder("Frontend", "Backend");
    }

    /// <summary>
    /// TGO3: With TargetGroupMigrationOrder = "Frontend,Backend" in appsettings,
    /// Frontend records should have lower Ids than Backend records.
    /// </summary>
    [Fact]
    public async Task MigrateUp_AppSettingsOrder_FrontendBeforeBackend()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .WithTargetGroupMigrationOrder("Frontend,Backend")
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeTrue($"MigrateUp with appsettings Frontend-first order should succeed: {result.ErrorMessage}");
        ctx.AssertTargetGroupMigrationOrder("Frontend", "Backend");
    }

    /// <summary>
    /// TGO4: CLI order overrides appsettings order.
    /// appsettings has "Frontend,Backend" but CLI specifies ["Backend","Frontend"].
    /// Backend should execute first.
    /// </summary>
    [Fact]
    public async Task MigrateUp_CliOverridesAppSettings()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .WithTargetGroupMigrationOrder("Frontend,Backend")
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Backend", "Frontend"]);

        result.Success.Should().BeTrue($"MigrateUp CLI override should succeed: {result.ErrorMessage}");
        ctx.AssertTargetGroupMigrationOrder("Backend", "Frontend");
    }

    /// <summary>
    /// TGO5: migsettings TargetGroupMigrationOrder overrides appsettings order.
    /// appsettings has "Backend,Frontend" (default), migsettings for Release_1.0 sets Frontend first.
    /// Frontend records for Release_1.0 should appear before Backend records in the repository.
    /// </summary>
    [Fact]
    public async Task MigrateUp_MigSettingsOrder_FrontendBeforeBackend()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .SetMigSettings("Release_1.0/migsettings.txt", new Dictionary<string, string>
            {
                ["TargetGroupMigrationOrder"] = "[\"Frontend\", \"Backend\"]"
            })
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(toRelease: "Release_1.0");

        result.Success.Should().BeTrue($"MigrateUp with migsettings order should succeed: {result.ErrorMessage}");
        ctx.AssertTargetGroupMigrationOrder("Frontend", "Backend");
    }

    /// <summary>
    /// TGO6: Baseline respects TargetGroupMigrationOrder from CLI.
    /// Frontend should be baselined before Backend.
    /// </summary>
    [Fact]
    public async Task Baseline_CliOrder_FrontendBeforeBackend()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync(MigrationCommand.Baseline);

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.BaselineAsync(
            targetGroupMigrationOrder: ["Frontend", "Backend"]);

        result.Success.Should().BeTrue($"Baseline with Frontend-first order should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files");
        ctx.AssertTargetGroupMigrationOrder("Frontend", "Backend");
    }

    /// <summary>
    /// TGO7: TargetGroupMigrationOrder with wrong case produces an error with a hint.
    /// Specifying "backend" instead of "Backend" should fail with a case-sensitive error message.
    /// </summary>
    [Fact]
    public async Task MigrateUp_WrongCaseOrder_ShouldFailWithHint()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["backend", "Frontend"]);

        result.Success.Should().BeFalse("Wrong-case TargetGroupMigrationOrder should fail");
        result.ErrorMessage.Should().Contain("case-insensitively but not case-sensitively",
            "Error should hint at the correct case for the alias");
    }

    /// <summary>
    /// TGO8: TargetGroupMigrationOrder on a single-TargetGroup product should fail.
    /// The feature is only allowed when the product has more than one TargetGroup.
    /// </summary>
    [Fact]
    public async Task MigrateUp_SingleTargetGroup_ShouldFailWhenOrderSpecified()
    {
        await using var ctx = await CreateScenario()
            .BuildAsync();

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Backend"]);

        result.Success.Should().BeFalse("TargetGroupMigrationOrder on single-TG product should fail");
        result.ErrorMessage.Should().Contain("not allowed when product has only",
            "Error should explain that only multi-TargetGroup products support execution order");
    }

    /// <summary>
    /// TGO9: Incomplete TargetGroupMigrationOrder (only one alias for a two-TG product) should fail.
    /// </summary>
    [Fact]
    public async Task MigrateUp_IncompleteOrder_ShouldFail()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Backend"]);

        result.Success.Should().BeFalse("Incomplete TargetGroupMigrationOrder should fail");
        result.ErrorMessage.Should().Contain("All TargetGroup aliases must be specified",
            "Error should state that all aliases are required");
    }

    /// <summary>
    /// TGO10: After MigrateUp with Frontend-first order, actual user tables from Frontend
    /// should exist on ConnectionString2 and Backend tables on the primary connection.
    /// Verifies that execution order does not affect which database gets which migrations.
    /// </summary>
    [Fact]
    public async Task MigrateUp_CliOrder_FrontendFirst_TablesOnCorrectDatabases()
    {
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Frontend", "Backend"]);

        result.Success.Should().BeTrue($"MigrateUp should succeed: {result.ErrorMessage}");

        // Backend tables on primary connection
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);

        // Frontend tables on secondary connection
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);

        // Execution order in repository is Frontend first
        ctx.AssertTargetGroupMigrationOrder("Frontend", "Backend");
    }
}
