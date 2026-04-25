using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    public bool IsDatabaseAvailable { get; private set; }
    public EngineConfig EngineConfig { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        EngineConfig = new EngineConfig
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=127.0.0.1;Initial Catalog=Backend_1;TrustServerCertificate=true;Connect Timeout=5;User Id=sa;Password=P@ssw0rd!",
            ConnectionString2 = "Server=127.0.0.1;Initial Catalog=Backend_2;TrustServerCertificate=true;Connect Timeout=5;User Id=sa;Password=P@ssw0rd!",
            SchemaName = "ray",
            BaseFilesPath = Path.Combine(AppContext.BaseDirectory, "MigrationFiles", "SqlServer")
        };

        IsDatabaseAvailable = DockerHealthCheck.IsDatabaseAvailable(
            EngineConfig.DatabaseType, EngineConfig.ConnectionString);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
