// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Describes a single defaults promotion applied by DefaultsPromoter.
/// </summary>
public class PromotionResult
{
    public string PropertyName { get; set; } = "";
    public string PromotedValue { get; set; } = "";
    public int AffectedProducts { get; set; }

    /// <summary>"ProductDefaults" or "TargetGroupDefaults"</summary>
    public string Level { get; set; } = "";
}
