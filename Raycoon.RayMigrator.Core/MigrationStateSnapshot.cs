// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Class for providing a snapshot of the current migration context used for logging.
/// </summary>
public class MigrationStateSnapshot
{
    // Migration Process: RunId's
    public int ProductId { get; init; }
    public int MigrationRunId { get; init; }
    public int MigrationId { get; init; }

    // Migration Process: File / Block
    public required string ReleaseVersionFromFileNameWithPath { get; init; }
//    public required string ReleaseDescription { get; set; }
    public required string FilenameWithRelativePath { get; init; }
    public int FileOrderId { get; init; }
    public int FileBlockId { get; init; }

    // Migration Process: Step / Result
    public MigrationRunResult MigrationRunResult { get; init; }
    public MigrationOperation MigrationOperation { get; init; }
    public MigrationStatus MigrationStatus { get; init; }
    
    // Migration Process: TargetGroup- / Target-settings
    public required string TargetGroupAlias { get; init; }
    public HashValidationScope? HashValidationScope { get; init; } // In TargetGroup-Optionen enthalten
    public required string TargetAlias { get; init; }
    

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return string.Empty;
    }
}