// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum MigrationOperation : byte
{
    /// <summary>
    /// Invalid value. RunOperation has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Performing Rollback.
    /// </summary>
    Rollback = 5,

    /// <summary>
    /// Performing Down-Migration.
    /// </summary>
    MigrateDown = 50,

    /// <summary>
    /// Performing Up-Migration.
    /// </summary>
    MigrateUp = 100,
}