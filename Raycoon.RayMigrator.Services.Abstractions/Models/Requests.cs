// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Request for forward migration
/// </summary>
public class MigrateUpRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public bool AllowOutOfOrder { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
}

/// <summary>
/// Request for rollback migration
/// </summary>
public class MigrateDownRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string TargetReleaseVersion { get; set; } = string.Empty;
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}

/// <summary>
/// Request for hash validation
/// </summary>
public class ValidateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public HashValidationScope? HashValidationScope { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}

/// <summary>
/// Request for hash update
/// </summary>
public class UpdateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}

/// <summary>
/// Request for baseline operation (mark migrations as executed without running SQL)
/// </summary>
public class BaselineRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
}

/// <summary>
/// Request for fixing repository inconsistencies (orphaned runs, unclear migrations)
/// </summary>
public class FixIssuesRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public FixIssues Scope { get; set; } = FixIssues.OrphanedRuns;
    public int OlderThanMinutes { get; set; } = 60;
    public bool DryRun { get; set; } = false;
    public MigrationStatus AssumedMigrationStatus { get; set; } = MigrationStatus.NotMigrated;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
}