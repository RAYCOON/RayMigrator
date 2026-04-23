// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>Tracks wizard completion status for a product+environment combination.</summary>
public class ProductEnvironmentEntry
{
    /// <summary>Whether the Detailed Configuration wizard has been fully completed for this combination.</summary>
    public bool WizardCompleted { get; set; }
}
