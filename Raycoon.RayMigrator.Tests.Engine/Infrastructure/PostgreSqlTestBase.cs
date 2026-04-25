using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Base class for PostgreSQL engine tests.
/// Provides access to the fixture and a convenience method to create scenarios.
/// </summary>
public abstract class PostgreSqlTestBase
{
    protected readonly PostgreSqlFixture Fixture;

    protected PostgreSqlTestBase(PostgreSqlFixture fixture) => Fixture = fixture;

    protected ScenarioBuilder CreateScenario() => new(Fixture.EngineConfig);
}
