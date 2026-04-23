// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Represents the status of a single migration record in the repository.
/// Replaces the former MigrationRunResult + MigrationTargetState dual-field approach on the Migration table.
/// </summary>
public enum MigrationStatus : byte
{
    /// <summary>
    /// Invalid value. Value has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Migration record created, execution has not started yet.
    /// </summary>
    Pending = 10,

    /// <summary>
    /// SQL blocks are currently being executed.
    /// </summary>
    Executing = 20,

    /// <summary>
    /// Execution failed, database state is unclear.
    /// </summary>
    Failed = 30,

    /// <summary>
    /// File is not deployed on target database (rolled back or never executed).
    /// </summary>
    NotMigrated = 50,

    /// <summary>
    /// File is successfully deployed on target database.
    /// </summary>
    Migrated = 100,
}
