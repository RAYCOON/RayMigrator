// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

public class RayMigratorConsoleOptions
{
    /// <summary>
    /// The command to execute (Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash)
    /// </summary>
    public required MigrationCommand Command { get; init; }
    
    /// <summary>
    /// Product alias from configuration
    /// </summary>
    public required string Product { get; init; }
    
    /// <summary>
    /// Target environment (required for all commands)
    /// </summary>
    public required string Environment { get; init; }
    
    /// <summary>
    /// Execution mode (Migrate or Simulate)
    /// </summary>
    public required MigrationRunMode RunMode { get; init; }
    
    /// <summary>
    /// Target release version for migration
    /// </summary>
    public string? TargetReleaseVersion { get; init; }

    /// <summary>
    /// Target group aliases to filter execution (optional, empty = all target groups)
    /// </summary>
    public string[]? TargetGroupAliases { get; init; }

    /// <summary>
    /// Explicit TargetGroup execution order (optional, parsed from comma-separated CLI input).
    /// When specified, all TargetGroup aliases must be listed exactly once.
    /// Applies to MigrateUp and Baseline commands only.
    /// </summary>
    public string[]? TargetGroupMigrationOrder { get; init; }
    
    /// <summary>
    /// Hash validation scope (for Validate-Hash command)
    /// </summary>
    public HashValidationScope? HashValidationScope { get; init; }
    
    /// <summary>
    /// Show startup information
    /// </summary>
    public required bool ShowStartupInfo { get; set; }
    
    /// <summary>
    /// Include sensitive data in logs (WARNING: includes passwords)
    /// </summary>
    public required bool RevealSensitiveData { get; init; }

    /// <summary>
    /// Fix command: Scope (All, OrphanedRuns)
    /// </summary>
    public FixIssues? FixIssues { get; init; }

    /// <summary>
    /// Allow out-of-order migration execution (Migrate-Up only)
    /// </summary>
    public bool? AllowOutOfOrder { get; init; }

    /// <summary>
    /// Fix command: Only fix runs older than N minutes (default 60, 0 = immediate)
    /// </summary>
    public int? FixOlderThanMinutes { get; init; }

    /// <summary>
    /// Fix command: Only show what would be fixed, don't actually fix
    /// </summary>
    public bool? FixDryRun { get; init; }

    /// <summary>
    /// Fix command: Status to assign to orphaned migrations (Migrated or NotMigrated)
    /// </summary>
    public MigrationStatus? FixAssumedMigrationStatus { get; init; }

    /// <summary>
    /// Stop error-recovery rollback chain when rollback file is missing (default: true).
    /// CLI override for StopRollbackOnMissingRollbackFile configuration.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; init; }

    /// <summary>
    /// Override directory where RayMigrator searches for appsettings.json files.
    /// When null, the current working directory is used.
    /// Always resolved to an absolute path at parse time.
    /// </summary>
    public string? ConfigDir { get; init; }

}