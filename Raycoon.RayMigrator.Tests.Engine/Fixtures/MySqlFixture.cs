using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Fixtures;

public class MySqlFixture : IAsyncLifetime
{
    public bool IsDatabaseAvailable { get; private set; }
    public EngineConfig EngineConfig { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        EngineConfig = new EngineConfig
        {
            DatabaseType = "MySql",
            ConnectionString = "Server=127.0.0.1;Port=3307;Database=raydb;User Id=rayuser;Password=raypass123;Connection Timeout=5",
            ConnectionString2 = "Server=127.0.0.1;Port=3307;Database=raydb_frontend;User Id=rayuser;Password=raypass123;Connection Timeout=5",
            SchemaName = "raydb",
            BaseFilesPath = Path.Combine(AppContext.BaseDirectory, "MigrationFiles", "MySql")
        };

        IsDatabaseAvailable = DockerHealthCheck.IsDatabaseAvailable(
            EngineConfig.DatabaseType, EngineConfig.ConnectionString);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
