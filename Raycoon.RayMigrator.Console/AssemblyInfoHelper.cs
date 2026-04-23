// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator;

/// <summary>
/// Provides assembly information to be displayed at program startup.
/// Delegates to the shared helper for logo and version.
/// </summary>
public static class AssemblyInfoHelper
{
    /// <summary>
    /// Returns the ASCII-art startup header with version, description, and copyright.
    /// </summary>
    public static string GetAssemblyInfo()
    {
        return Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetAsciiHeader();
    }

    /// <summary>
    /// Gets the RayMigrator version. Delegates to the shared helper.
    /// </summary>
    public static string GetRayMigratorVersion()
    {
        return Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetRayMigratorVersion();
    }
}
