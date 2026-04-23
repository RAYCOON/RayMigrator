// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Enum for RayMigratorOptions
/// </summary>
public enum MigrationErrorAction : byte
{
    /// <summary>
    /// Invalid value.
    /// </summary>
    Undefined = 0,
    
    /// <summary>
    /// Instantly terminate all migrations and do not perform any further actions.
    /// </summary>
    Terminate = 10,
    
    /// <summary>
    /// Migrate down all migrations performed by the current MigrationRun.
    /// </summary>
    Rollback = 20,
    
    /// <summary>
    /// Migrate down only using the .down-file associated with the file that caused the error(s).
    /// </summary>
    RollbackErrorOnly = 21,

    /// <summary>
    /// Migrate down all migrations from the release that caused the error.
    /// Migrations from earlier releases remain intact.
    /// </summary>
    RollbackRelease = 22,

    /// <summary>
    /// Ignore the error and continue execution.
    /// Failed SQL blocks are skipped, and the migration file is marked as Failed.
    /// The migration run continues with the next file.
    /// </summary>
    Ignore = 30
}