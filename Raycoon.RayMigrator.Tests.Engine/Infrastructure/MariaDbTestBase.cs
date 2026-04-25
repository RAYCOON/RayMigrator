
using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Base class for MariaDB engine tests.
/// Provides access to the fixture and a convenience method to create scenarios.
/// </summary>
public abstract class MariaDbTestBase
{
    protected readonly MariaDbFixture Fixture;

    protected MariaDbTestBase(MariaDbFixture fixture) => Fixture = fixture;

    protected ScenarioBuilder CreateScenario() => new(Fixture.EngineConfig);
}
