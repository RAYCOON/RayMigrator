
using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Base class for MySQL engine tests.
/// Provides access to the fixture and a convenience method to create scenarios.
/// </summary>
public abstract class MySqlTestBase
{
    protected readonly MySqlFixture Fixture;

    protected MySqlTestBase(MySqlFixture fixture) => Fixture = fixture;

    protected ScenarioBuilder CreateScenario() => new(Fixture.EngineConfig);
}
