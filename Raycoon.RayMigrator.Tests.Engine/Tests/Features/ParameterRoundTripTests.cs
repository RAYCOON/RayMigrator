using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A template parameter value with every character that could break the SQL text reaches the database and
/// comes back unchanged through the PostgreSQL DAL (see <see cref="ParameterRoundTripValues"/>), in a plain
/// statement and inside a DO block, whose $$ delimiter the value contains.
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class ParameterRoundTripTests : PostgreSqlTestBase
{
    public ParameterRoundTripTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalar_HostileStringValue_RoundTrips()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalPostgreSql(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT CAST(@Value AS TEXT)", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters());

        result.Should().Be(ParameterRoundTripValues.HostileString);
    }

    [Fact]
    public async Task ExecuteScalar_HostileStringValueInDoBlock_RoundTripsThroughNotice()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalPostgreSql(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("DO $$ BEGIN RAISE NOTICE '%', @Value; END $$;", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters());

        result.Should().Be(ParameterRoundTripValues.HostileString);
    }

    [Fact]
    public async Task ExecuteScalar_NullValue_ReturnsNull()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var dal = new DalPostgreSql(Fixture.EngineConfig.ConnectionString);

        var result = await dal.ExecuteScalarAsync("SELECT CAST(@Null AS TEXT)", ParameterRoundTripValues.Settings, ParameterRoundTripValues.Parameters());

        (result == null || result is DBNull).Should().BeTrue();
    }
}
