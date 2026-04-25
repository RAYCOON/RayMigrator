using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Base class for SQLite engine tests.
/// Provides access to the fixture and a convenience method to create scenarios.
/// </summary>
public abstract class SqliteTestBase
{
    protected readonly SqliteFixture Fixture;

    protected SqliteTestBase(SqliteFixture fixture) => Fixture = fixture;

    protected ScenarioBuilder CreateScenario() => new(Fixture.EngineConfig);
}
