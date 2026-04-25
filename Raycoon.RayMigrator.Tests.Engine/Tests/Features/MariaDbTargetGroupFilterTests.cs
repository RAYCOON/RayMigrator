using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbTargetGroupFilterTests : MariaDbTestBase
{
    public MariaDbTargetGroupFilterTests(MariaDbFixture fixture) : base(fixture) { }

    /// <summary>
    /// Copies MariaDb_Frontend migration files into the work directory so the engine
    /// discovers Frontend files alongside the Backend files already copied by ScenarioBuilder.
    /// </summary>
    private static void CopyFrontendFiles(string workDir)
    {
        string frontendBase = Path.Combine(
            AppContext.BaseDirectory, "MigrationFiles", "MariaDb_Frontend");
        foreach (string srcFile in Directory.GetFiles(frontendBase, "*.*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(frontendBase, srcFile);
            string dstFile = Path.Combine(workDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
            File.Copy(srcFile, dstFile, true);
        }
    }

    /// <summary>
    /// T1: MigrateUp with Backend-only filter should succeed.
    /// Frontend files exist on disk but should be ignored.
    /// </summary>
    [Fact]
    public async Task MigrateUp_BackendOnly_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend"]);

        result.Success.Should().BeTrue($"MigrateUp Backend-only should succeed: {result.ErrorMessage}");
        result.Result.Should().Be(MigrationRunResult.Ok);
    }

    /// <summary>
    /// T2: After Backend-only migration, Frontend tables should NOT exist on ConnectionString2.
    /// </summary>
    [Fact]
    public async Task MigrateUp_BackendOnly_NoFrontendTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        await ctx.MigrateUpAsync(targetGroupAliases: ["Backend"]);
        ctx.AssertSuccess(true);

        // Frontend tables should not exist on the Frontend database
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", false);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", false);
    }

    /// <summary>
    /// T3: After Backend-only migration, only Backend repo records should exist. Frontend count should be 0.
    /// </summary>
    [Fact]
    public async Task MigrateUp_BackendOnly_OnlyBackendRepoRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        await ctx.MigrateUpAsync(targetGroupAliases: ["Backend"]);
        ctx.AssertSuccess(true);

        ctx.CountMigrationsForTargetGroup("Backend").Should().BeGreaterThan(0,
            "Backend migrations should exist in repository");
        ctx.CountMigrationsForTargetGroup("Frontend").Should().Be(0,
            "Frontend migrations should NOT exist in repository");
    }

    /// <summary>
    /// T4: MigrateUp all, then MigrateDown Backend-only to R2. Frontend tables should still exist.
    /// </summary>
    [Fact]
    public async Task MigrateDown_BackendOnly_PreservesFrontend()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        // MigrateUp all target groups
        var upResult = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend", "Frontend"]);
        upResult.Success.Should().BeTrue($"MigrateUp all failed: {upResult.ErrorMessage}");

        // Frontend tables should exist after full migration
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);

        // MigrateDown Backend only to Release_2.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var downResult = await ctx.MigrateDownAsync("Release_2.0", targetGroupAliases: ["Backend"]);
        downResult.Success.Should().BeTrue($"MigrateDown Backend to R2 failed: {downResult.ErrorMessage}");

        // Frontend tables should still exist (untouched)
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex2", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley2", true);
    }

    /// <summary>
    /// T5: Baseline with Backend-only filter should only baseline Backend files.
    /// </summary>
    [Fact]
    public async Task Baseline_BackendOnly_ShouldOnlyBaselineBackend()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync(MigrationCommand.Baseline);

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.BaselineAsync(targetGroupAliases: ["Backend"]);

        result.Success.Should().BeTrue($"Baseline Backend should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark Backend files");

        ctx.CountMigrationsForTargetGroup("Backend").Should().BeGreaterThan(0,
            "Backend migrations should exist in repository after baseline");
        ctx.CountMigrationsForTargetGroup("Frontend").Should().Be(0,
            "Frontend migrations should NOT exist in repository after baseline");
    }

    /// <summary>
    /// T6: MigrateUp all, then ValidateHash Backend-only should succeed.
    /// </summary>
    [Fact]
    public async Task ValidateHash_BackendOnly_AfterFullMigration_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        // MigrateUp all
        var upResult = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend", "Frontend"]);
        upResult.Success.Should().BeTrue($"MigrateUp all failed: {upResult.ErrorMessage}");

        // ValidateHash Backend only
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync(targetGroupAliases: ["Backend"]);

        result.Success.Should().BeTrue($"ValidateHash Backend failed: {result.ErrorMessage}");
        result.InvalidFiles.Should().Be(0, "No Backend files should have invalid hashes");
    }

    /// <summary>
    /// T7: MigrateUp with a non-existent target group should fail.
    /// </summary>
    [Fact]
    public async Task MigrateUp_InvalidTargetGroup_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(targetGroupAliases: ["NonExistent"]);

        result.Success.Should().BeFalse("MigrateUp with invalid target group should fail");
        result.ErrorMessage.Should().Contain("not found in product configuration");
    }

    /// <summary>
    /// T8: MigrateUp all, then UpdateHash Backend-only should succeed with 0 updated files.
    /// </summary>
    [Fact]
    public async Task UpdateHash_BackendOnly_AfterFullMigration_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        // MigrateUp all
        var upResult = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend", "Frontend"]);
        upResult.Success.Should().BeTrue($"MigrateUp all failed: {upResult.ErrorMessage}");

        // UpdateHash Backend only
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync(targetGroupAliases: ["Backend"]);

        result.Success.Should().BeTrue($"UpdateHash Backend failed: {result.ErrorMessage}");
        result.UpdatedFiles.Should().Be(0, "No files should need hash updates after fresh migration");
    }

    /// <summary>
    /// T9: MigrateUp Backend first, then Frontend incrementally. Both groups should have data.
    /// </summary>
    [Fact]
    public async Task MigrateUp_BackendFirst_ThenFrontend_Incremental()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        // Phase 1: Backend only
        var backendResult = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend"]);
        backendResult.Success.Should().BeTrue($"MigrateUp Backend failed: {backendResult.ErrorMessage}");

        ctx.AssertTableExists("tablea", true);
        ctx.CountMigrationsForTargetGroup("Backend").Should().BeGreaterThan(0);

        // Phase 2: Frontend only (needs allowOutOfOrder since Backend is ahead)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var frontendResult = await ctx.MigrateUpAsync(allowOutOfOrder: true, targetGroupAliases: ["Frontend"]);
        frontendResult.Success.Should().BeTrue($"MigrateUp Frontend failed: {frontendResult.ErrorMessage}");

        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);
        ctx.CountMigrationsForTargetGroup("Frontend").Should().BeGreaterThan(0);
    }

    /// <summary>
    /// T10: MigrateUp Frontend-only should NOT create Backend tables.
    /// </summary>
    [Fact]
    public async Task MigrateUp_FrontendOnly_NoBackendTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(targetGroupAliases: ["Frontend"]);
        result.Success.Should().BeTrue($"MigrateUp Frontend failed: {result.ErrorMessage}");

        // Frontend tables should exist
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);

        // Backend tables should NOT exist
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
    }

    /// <summary>
    /// T11: MigrateUp with both Backend and Frontend should migrate all target groups.
    /// </summary>
    [Fact]
    public async Task MigrateUp_MultipleTargetGroups_ShouldMigrateAll()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        var result = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend", "Frontend"]);

        result.Success.Should().BeTrue($"MigrateUp both groups failed: {result.ErrorMessage}");

        // Backend tables
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);

        // Frontend tables
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tablex1", true);
        ctx.AssertTableExistsOnConnection(Fixture.EngineConfig.ConnectionString2!, "tabley1", true);

        // Repository records for both groups
        ctx.CountMigrationsForTargetGroup("Backend").Should().BeGreaterThan(0,
            "Backend migrations should exist in repository");
        ctx.CountMigrationsForTargetGroup("Frontend").Should().BeGreaterThan(0,
            "Frontend migrations should exist in repository");
    }

    /// <summary>
    /// T12: MigrateUp all, then break a Backend rollback file, MigrateDown Backend-only should fail.
    /// </summary>
    [Fact]
    public async Task MigrateDown_BackendOnly_WithBrokenRollback_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithTargetGroup("Frontend", Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.ConnectionString2!)
            .BreakRollback("Release_4.0", "01_CreateTableG.sql")
            .BuildAsync();

        CopyFrontendFiles(ctx.WorkDirectory);

        // MigrateUp all
        var upResult = await ctx.MigrateUpAsync(targetGroupAliases: ["Backend", "Frontend"]);
        upResult.Success.Should().BeTrue($"MigrateUp all failed: {upResult.ErrorMessage}");

        // MigrateDown Backend to Release_2.0 with broken rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var downResult = await ctx.MigrateDownAsync("Release_2.0", targetGroupAliases: ["Backend"]);

        downResult.Success.Should().BeFalse("MigrateDown with broken rollback should fail");
    }
}
