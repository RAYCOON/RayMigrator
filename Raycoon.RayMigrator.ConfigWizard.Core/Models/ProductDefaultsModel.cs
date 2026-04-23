// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class ProductDefaultsModel
{
    public string MigrationErrorAction { get; set; } = "Terminate";
    public string RollbackErrorAction { get; set; } = "Terminate";
    public string MigrationFilesExtension { get; set; } = "sql";
    public string MigrationRollbackFilesPreExtension { get; set; } = "rollback";
    public string MigrationFilesEncoding { get; set; } = "UTF-8";
    public bool RequireRollbackFile { get; set; } = true;
    public bool StopRollbackOnMissingRollbackFile { get; set; } = true;

    /// <summary>
    /// Default CLI tool alias for all products. Products/TargetGroups/Targets can override.
    /// Corresponds to Core's ProductDefaultOptions.UseCliToolAlias.
    /// </summary>
    public string? UseCliToolAlias { get; set; }

    public TargetGroupDefaultsModel TargetGroupDefaults { get; set; } = new();
}
