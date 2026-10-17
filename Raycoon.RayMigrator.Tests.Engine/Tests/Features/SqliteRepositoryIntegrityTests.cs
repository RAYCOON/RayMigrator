using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteRepositoryIntegrityTests : SqliteTestBase
{
    public SqliteRepositoryIntegrityTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AllRepoTables_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
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
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertProductExists(true);
    }

    [Fact]
    public async Task Environment_ShouldExist()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertEnvironmentExists("Docker", true);
    }

    [Fact]
    public async Task MigrationRecords_ShouldBeMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.CountMigrations().Should().BeGreaterThan(0, "Migration records should exist in repository");
        ctx.CountMigrationRuns().Should().BeGreaterThan(0, "At least one MigrationRun should exist");
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Ok,
            "Latest MigrationRun should have Result=Ok");
    }

    /// <summary>
    /// R5: Hosts that start the first run of a new product at the same moment register it once: in each of five rounds
    /// eight starts of their own run Repository_Product_CheckInsert at once with a new name, none fails, and all get the
    /// Id of the one new row (<see cref="RegistrationContract"/>).
    /// </summary>
    [Fact]
    public async Task Product_ParallelFirstRegistrations_CreateItOnce()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RegistrationContract.VerifyParallelFirstRegistrationsCreateTheProductOnceAsync(CreateScenario());
    }

    /// <summary>
    /// R6: Hosts that start the first run in a new environment at the same moment register it once, the same way
    /// (<see cref="RegistrationContract"/>).
    /// </summary>
    [Fact]
    public async Task Environment_ParallelFirstRegistrations_CreateItOnce()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RegistrationContract.VerifyParallelFirstRegistrationsCreateTheEnvironmentOnceAsync(CreateScenario());
    }
}
