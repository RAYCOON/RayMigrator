// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

public sealed class TargetGroupInput
{
    public string? Alias { get; init; }
    public string? DatabaseType { get; init; }
    public string? UseCliToolAlias { get; init; }

    /// <summary>Merged effective value after ProductDefaults.TargetGroupDefaults cascade.</summary>
    public string? EffectiveTargetMigrationOrder { get; init; }
    public string? EffectiveHashValidationScope { get; init; }

    public IReadOnlyList<TargetInput> Targets { get; init; } = Array.Empty<TargetInput>();
}
