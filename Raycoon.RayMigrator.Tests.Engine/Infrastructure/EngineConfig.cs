// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Holds database-specific configuration for engine tests.
/// </summary>
public record EngineConfig
{
    /// <summary>Database type identifier (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite).</summary>
    public required string DatabaseType { get; init; }

    /// <summary>Primary target connection string.</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Secondary target connection string for multi-target tests.</summary>
    public string? ConnectionString2 { get; init; }

    /// <summary>Repository schema name.</summary>
    public required string SchemaName { get; init; }

    /// <summary>Base path to migration files for this database type.</summary>
    public required string BaseFilesPath { get; init; }
}
