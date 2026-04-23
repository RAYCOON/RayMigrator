// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core.Configuration.Sources;

/// <summary>
/// Abstraction for loading RayMigrator configuration from different sources.
/// Each implementation encapsulates how RayMigratorOptions are loaded:
/// <list type="bullet">
///   <item><description>JSON files (appsettings.json hierarchy)</description></item>
///   <item><description>Admin-DB (SQLite/SQL Server/PostgreSQL/MariaDB/MySQL)</description></item>
/// </list>
/// </summary>
public interface IOptionsSource
{
    /// <summary>
    /// Loads configuration for the specified product and environment.
    /// </summary>
    /// <param name="product">The product alias to load configuration for.</param>
    /// <param name="environment">The environment name (e.g. "Docker", "Production").</param>
    /// <returns>An <see cref="OptionsSourceResult"/> containing all data needed by the execution pipeline.</returns>
    Task<OptionsSourceResult> LoadAsync(string product, string environment);
}
