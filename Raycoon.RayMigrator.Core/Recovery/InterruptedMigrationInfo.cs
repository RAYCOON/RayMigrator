// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core.Recovery;

/// <summary>
/// Contains information about an interrupted migration that can be resumed.
/// </summary>
public class InterruptedMigrationInfo
{
    /// <summary>
    /// The ID of the interrupted migration record.
    /// </summary>
    public int MigrationId { get; set; }

    /// <summary>
    /// The ID of the migration run that was interrupted.
    /// </summary>
    public int MigrationRunId { get; set; }

    /// <summary>
    /// The release version being migrated.
    /// </summary>
    public string ReleaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// The filename of the migration that was interrupted.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The number of blocks that were successfully migrated before interruption.
    /// </summary>
    public int BlocksMigrated { get; set; }

    /// <summary>
    /// The total number of blocks in the migration file.
    /// </summary>
    public int BlocksTotal { get; set; }

    /// <summary>
    /// The environment where the migration was running.
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// The FK id of the environment where the migration was running.
    /// </summary>
    public int EnvironmentId { get; set; }

    /// <summary>
    /// The target group alias where the migration was executing.
    /// </summary>
    public string TargetGroupAlias { get; set; } = string.Empty;

    /// <summary>
    /// The target alias where the migration was executing.
    /// </summary>
    public string TargetAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets the next block number to resume from (1-based).
    /// </summary>
    public int NextBlockToResume => BlocksMigrated + 1;

    /// <summary>
    /// Gets the progress percentage of the interrupted migration.
    /// </summary>
    public double ProgressPercent => BlocksTotal > 0 ? (double)BlocksMigrated / BlocksTotal * 100 : 0;

    public override string ToString()
    {
        return $"Migration {MigrationId} ({Filename}): Block {BlocksMigrated}/{BlocksTotal} ({ProgressPercent:F1}%)";
    }
}
