// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Defines the hosting mode for RayMigrator's DI registration.
/// </summary>
public enum RayMigratorHostMode
{
    /// <summary>
    /// CLI mode: short-lived process, singleton MigrationContext.
    /// </summary>
    Cli,

    /// <summary>
    /// API mode: long-lived server, per-request MigrationContext via AsyncLocal.
    /// </summary>
    Api
}
