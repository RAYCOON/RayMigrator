// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Fixtures;

public class SqliteFixture : IAsyncLifetime
{
    // SQLite is file-based and does not require Docker.
    // DalSqlite uses explicit ExecuteReader + NextResult for multi-statement SQL batching.
    public bool IsDatabaseAvailable => true;
    public EngineConfig EngineConfig { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "RayMigrator_SqliteTests");
        Directory.CreateDirectory(tempDir);

        EngineConfig = new EngineConfig
        {
            DatabaseType = "Sqlite",
            ConnectionString = $"Data Source={Path.Combine(tempDir, $"raytest_{Guid.NewGuid()}.sqlite")}",
            ConnectionString2 = $"Data Source={Path.Combine(tempDir, $"raytest2_{Guid.NewGuid()}.sqlite")}",
            SchemaName = "",
            BaseFilesPath = Path.Combine(AppContext.BaseDirectory, "MigrationFiles", "Sqlite")
        };
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        TryDeleteFile(EngineConfig.ConnectionString);
        TryDeleteFile(EngineConfig.ConnectionString2!);
        return ValueTask.CompletedTask;
    }

    private static void TryDeleteFile(string connectionString)
    {
        try
        {
            // Extract file path from "Data Source=path"
            var parts = connectionString.Split('=', 2);
            if (parts.Length == 2)
            {
                string path = parts[1].Trim().TrimEnd(';');
                if (File.Exists(path)) File.Delete(path);
            }
        }
        catch { /* ignore cleanup errors */ }
    }
}
