using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.MySql;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A template parameter value with every character that could break the SQL text reaches the database and
/// comes back unchanged through the MySql DAL (see <see cref="ParameterRoundTripValues"/>).
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlParameterRoundTripTests : MySqlTestBase
{
    public MySqlParameterRoundTripTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalar_HostileStringValue_RoundTrips()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalMySql(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT @Value", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters(), cancellationToken: TestContext.Current.CancellationToken);

        result.Should().Be(ParameterRoundTripValues.HostileString);
    }

    [Fact]
    public async Task ExecuteScalar_NullValue_ReturnsNull()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalMySql(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT @Null", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters(), cancellationToken: TestContext.Current.CancellationToken);

        (result == null || result is DBNull).Should().BeTrue();
    }
}
