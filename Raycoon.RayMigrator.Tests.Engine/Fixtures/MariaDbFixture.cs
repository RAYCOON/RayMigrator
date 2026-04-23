// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Fixtures;

public class MariaDbFixture : IAsyncLifetime
{
    public bool IsDatabaseAvailable { get; private set; }
    public EngineConfig EngineConfig { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        EngineConfig = new EngineConfig
        {
            DatabaseType = "MariaDb",
            ConnectionString = "Server=127.0.0.1;Port=3306;Database=raydb;User Id=rayuser;Password=raypass123;Connection Timeout=5",
            ConnectionString2 = "Server=127.0.0.1;Port=3306;Database=raydb_frontend;User Id=rayuser;Password=raypass123;Connection Timeout=5",
            SchemaName = "raydb",
            BaseFilesPath = Path.Combine(AppContext.BaseDirectory, "MigrationFiles", "MariaDb")
        };

        IsDatabaseAvailable = DockerHealthCheck.IsDatabaseAvailable(
            EngineConfig.DatabaseType, EngineConfig.ConnectionString);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
