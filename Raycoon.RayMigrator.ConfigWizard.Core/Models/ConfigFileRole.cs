// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Role of a configuration file in the appsettings hierarchy.
/// </summary>
public enum ConfigFileRole
{
    /// <summary>appsettings.json - base configuration</summary>
    Base = 1,

    /// <summary>appsettings.{Environment}.json - environment-specific overrides</summary>
    Environment = 2,

    /// <summary>appsettings.{Product}.json - product-specific overrides</summary>
    Product = 3,

    /// <summary>appsettings.{Product}.{Environment}.json - product+environment overrides</summary>
    ProductEnvironment = 4
}
