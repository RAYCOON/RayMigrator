// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetGroupDefaultsModel
{
    public string TargetMigrationOrder { get; set; } = "Successively";
    public string HashValidationScope { get; set; } = "File";
    public bool StopRollbackOnMissingRollbackFile { get; set; } = true;
    public TargetDefaultsModel TargetDefaults { get; set; } = new();
}
