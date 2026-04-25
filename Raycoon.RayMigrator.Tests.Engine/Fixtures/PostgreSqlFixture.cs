using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Fixtures;

public class PostgreSqlFixture : IAsyncLifetime
{
    public bool IsDatabaseAvailable { get; private set; }
    public EngineConfig EngineConfig { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        EngineConfig = new EngineConfig
        {
            DatabaseType = "PostgreSQL",
            ConnectionString = "Host=localhost;Port=5432;Database=raydb;Username=postgres;Password=postgres123;Timeout=5",
            ConnectionString2 = "Host=localhost;Port=5432;Database=raydb_frontend;Username=postgres;Password=postgres123;Timeout=5",
            SchemaName = "ray",
            BaseFilesPath = Path.Combine(AppContext.BaseDirectory, "MigrationFiles", "PostgreSQL")
        };

        IsDatabaseAvailable = DockerHealthCheck.IsDatabaseAvailable(
            EngineConfig.DatabaseType, EngineConfig.ConnectionString);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
