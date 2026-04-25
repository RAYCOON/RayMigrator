using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlRepositoryIntegrityTests : MySqlTestBase
{
    public MySqlRepositoryIntegrityTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AllRepoTables_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        string[] repoTables = ["Product", "Environment", "MigrationRun", "MigrationRecord", "MigratorMeta"];
        foreach (string table in repoTables)
        {
            ctx.AssertRepositoryTableExists(table, true);
        }
    }

    [Fact]
    public async Task Product_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.AssertProductExists(true);
    }

    [Fact]
    public async Task Environment_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.AssertEnvironmentExists("Docker", true);
    }

    [Fact]
    public async Task MigrationRecords_ShouldBeMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.CountMigrations().Should().BeGreaterThan(0, "Migration records should exist in repository");
        ctx.CountMigrationRuns().Should().BeGreaterThan(0, "At least one MigrationRun should exist");
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Ok,
            "Latest MigrationRun should have Result=Ok");
    }
}
