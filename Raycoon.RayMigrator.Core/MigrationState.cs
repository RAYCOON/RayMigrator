// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Class containing dynamic data of the entire migration process.
/// </summary>
public class MigrationState
{
    // Migration Process
    public MigrationEvent? MigrationEvent { get; set; }
    
    // Migration Process: RunId's
    public int MigratorMetaId { get; set; }
    public int ProductId { get; set; }
    public int EnvironmentId { get; set; }
    public int MigrationRunId { get; set; }
    public int MigrationId { get; set; }

    // Migration Process: File metadata
    public string ReleaseVersionFromFileNameWithPath { get; set; } = string.Empty;
    public string FilenameWithRelativePath { get; set; } = string.Empty;
    public int FileOrderId { get; set; }
    public int FileBlockId { get; set; }

    // Migration Process: Step / Result
    public MigrationRunResult MigrationRunResult { get; set; }
    public MigrationOperation MigrationOperation { get; set; }
    public MigrationStatus MigrationStatus { get; set; }
    
    // Migration Process: TargetGroup- / Target-settings
    public string TargetGroupAlias { get; set; } = string.Empty;
    public HashValidationScope? HashValidationScope { get; set; } // In TargetGroup-Optionen enthalten
    public string TargetAlias { get; set; } = string.Empty;
}