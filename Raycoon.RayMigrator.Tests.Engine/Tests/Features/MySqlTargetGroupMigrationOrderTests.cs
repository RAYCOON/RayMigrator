using System.Collections.Concurrent;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Database", "MySql")]
[Trait("Category", "Features")]
public class MySqlTargetGroupMigrationOrderTests : MySqlTestBase
{
    public MySqlTargetGroupMigrationOrderTests(MySqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// Copies MySql_Frontend migration files into the work directory so the engine
    /// discovers Frontend files alongside the Backend files already copied by ScenarioBuilder.
    /// </summary>
    private static void CopyFrontendFiles(string workDir)
    {
        string frontendBase = Path.Combine(
            AppContext.BaseDirectory, "MigrationFiles", "MySql_Frontend");
        foreach (string srcFile in Directory.GetFiles(frontendBase, "*.*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(frontendBase, srcFile);
            string dstFile = Path.Combine(workDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
            File.Copy(srcFile, dstFile, true);
        }
    }

    /// <summary>Collects the rendered warnings of the host, from every thread that logs.</summary>
    private static ConcurrentQueue<string> CollectWarnings(ScenarioContext ctx)
    {
        var warnings = new ConcurrentQueue<string>();
        ctx.UseLogObserver(logEvent =>
        {
            if (logEvent.Level == LogEventLevel.Warning)
            {
                warnings.Enqueue(logEvent.RenderMessage());
            }
        });
        return warnings;
    }

    /// <summary>
    /// TGO1: Without TargetGroupMigrationOrder, config array order is used (Backend first, then Frontend).
    /// Repository records for Backend should have lower Ids than Frontend records.
    /// </summary>
    [Fact]
    public async Task MigrateUp_DefaultOrder_BackendBeforeFrontend()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
    /// TGO8: TargetGroupMigrationOrder on a single-TargetGroup product that names exactly that TargetGroup is ignored:
    /// migrate-up runs every file and warns once, although it resolves the order for each of the four releases.
    /// </summary>
    [Fact]
    public async Task MigrateUp_SingleTargetGroupOrderWithItsAlias_IsIgnoredWithWarning()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Arrange: no Frontend target group, only Backend exists
        await using var ctx = await CreateScenario()
            .BuildAsync();
        var warnings = CollectWarnings(ctx);

        // Act
        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["Backend"]);

        // Assert
        result.Success.Should().BeTrue($"an order that names the only TargetGroup is ignored: {result.ErrorMessage}");
        result.SuccessfulMigrations.Should().Be(12, "every file of the four releases migrates");
        warnings.Should().ContainSingle(w => w.Contains("is ignored", StringComparison.Ordinal),
                "migrate-up warns once per run that it ignores the order")
            .Which.Should().Be("TargetGroupMigrationOrder (source: --target-group-migration-order) is ignored: the product has only one TargetGroup [Backend].",
                "the warning names where the order comes from and the TargetGroup, as a plain message");
    }

    /// <summary>
    /// TGO8b: On a single-TargetGroup product an order that names the TargetGroup only case-insensitively is still
    /// refused, like any order other than its exact alias.
    /// </summary>
    [Fact]
    public async Task MigrateUp_SingleTargetGroupWrongCaseOrder_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Arrange: no Frontend target group, only Backend exists
        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Act
        var result = await ctx.MigrateUpAsync(
            targetGroupMigrationOrder: ["backend"]);

        // Assert
        result.Success.Should().BeFalse("an order that does not name the only TargetGroup exactly is refused");
        result.ErrorMessage.Should().Contain("not allowed when product has only",
            "only the exact alias of the only TargetGroup is ignored");
    }

    /// <summary>
    /// TGO8c: Baseline ignores an order that names exactly the only TargetGroup, as migrate-up does: it baselines every
    /// file and warns once.
    /// </summary>
    [Fact]
    public async Task Baseline_SingleTargetGroupOrderWithItsAlias_IsIgnoredWithWarning()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Arrange: no Frontend target group, only Backend exists
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);
        var warnings = CollectWarnings(ctx);

        // Act
        var result = await ctx.BaselineAsync(
            targetGroupMigrationOrder: ["Backend"]);

        // Assert
        result.Success.Should().BeTrue($"an order that names the only TargetGroup is ignored: {result.ErrorMessage}");
        result.BaselinedFiles.Should().Be(12, "every file of the four releases is baselined");
        warnings.Should().ContainSingle(w => w.Contains("is ignored", StringComparison.Ordinal),
                "baseline warns once per run that it ignores the order")
            .Which.Should().Be("TargetGroupMigrationOrder (source: --target-group-migration-order) is ignored: the product has only one TargetGroup [Backend].",
                "the warning names where the order comes from and the TargetGroup, as a plain message");
    }

    /// <summary>
    /// TGO8d: An order of the migsettings of a release that names exactly the only TargetGroup is ignored as well, with a
    /// warning that names the release whose migsettings set it, once for each such release.
    /// </summary>
    [Fact]
    public async Task MigrateUp_SingleTargetGroupMigSettingsOrderWithItsAlias_IsIgnoredWithAWarningPerRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Arrange: the migsettings of release 1.0 and of release 2.0 name the only TargetGroup
        await using var ctx = await CreateScenario()
            .SetMigSettings("Release_1.0/migsettings.txt", new Dictionary<string, string> { ["TargetGroupMigrationOrder"] = "[\"Backend\"]" })
            .SetMigSettings("Release_2.0/migsettings.txt", new Dictionary<string, string> { ["TargetGroupMigrationOrder"] = "[\"Backend\"]" })
            .BuildAsync();
        var warnings = CollectWarnings(ctx);

        // Act
        var result = await ctx.MigrateUpAsync();

        // Assert
        result.Success.Should().BeTrue($"an order that names the only TargetGroup is ignored: {result.ErrorMessage}");
        warnings.Where(w => w.Contains("is ignored", StringComparison.Ordinal)).Should().Equal(
            ["TargetGroupMigrationOrder (source: migsettings of release Release_1.0) is ignored: the product has only one TargetGroup [Backend].",
             "TargetGroupMigrationOrder (source: migsettings of release Release_2.0) is ignored: the product has only one TargetGroup [Backend]."],
            "the warning names each release whose migsettings set the order, once");
    }

    /// <summary>
    /// TGO8e: A product setting that names exactly the only TargetGroup is ignored without a warning of migrate-up: the
    /// validation at startup reports it for every command (RULE_1_13).
    /// </summary>
    [Fact]
    public async Task MigrateUp_SingleTargetGroupProductOrderWithItsAlias_IsIgnoredWithoutAWarningOfTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Arrange
        await using var ctx = await CreateScenario()
            .WithTargetGroupMigrationOrder("Backend")
            .BuildAsync();
        var warnings = CollectWarnings(ctx);

        // Act
        var result = await ctx.MigrateUpAsync();

        // Assert
        result.Success.Should().BeTrue($"an order that names the only TargetGroup is ignored: {result.ErrorMessage}");
        result.SuccessfulMigrations.Should().Be(12, "every file of the four releases migrates");
        warnings.Should().NotContain(w => w.Contains("TargetGroupMigrationOrder", StringComparison.Ordinal),
            "the validation at startup reports the product setting, so migrate-up adds no warning of its own");
    }

    /// <summary>
    /// TGO9: Incomplete TargetGroupMigrationOrder (only one alias for a two-TG product) should fail.
    /// </summary>
    [Fact]
    public async Task MigrateUp_IncompleteOrder_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
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
