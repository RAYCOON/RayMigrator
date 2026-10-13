using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.SqlServer;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A template parameter value with every character that could break the SQL text reaches the database and
/// comes back unchanged through the SqlServer DAL (see <see cref="ParameterRoundTripValues"/>).
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerParameterRoundTripTests : SqlServerTestBase
{
    public SqlServerParameterRoundTripTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalar_HostileStringValue_RoundTrips()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalSqlServer(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT @Value", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters());

        result.Should().Be(ParameterRoundTripValues.HostileString);
    }

    [Fact]
    public async Task ExecuteScalar_NullValue_ReturnsNull()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalSqlServer(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT @Null", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters());

        (result == null || result is DBNull).Should().BeTrue();
    }
}
