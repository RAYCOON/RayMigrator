using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The read model contract on SqlServer (ADR-025): after a migrate-up every repository table reads through the package's
/// names and maps to its record with the values the engine wrote, timestamps in UTC, the log in the repository.
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerReadModelContractTests : SqlServerTestBase
{
    public SqlServerReadModelContractTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_EveryTableReadsThroughTheReadModel()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var startedUtc = DateTime.UtcNow;
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Information")
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.FlushDatabaseLog();
        ctx.AssertSuccess(true);

        var naming = new RepositoryNaming(Fixture.EngineConfig.DatabaseType, Fixture.EngineConfig.SchemaName, ctx.RepositoryOptions.TableBaseName);
        await ReadModelContract.VerifyAfterMigrateUpAsync(naming, Fixture.EngineConfig.ConnectionString, startedUtc);
    }
}
