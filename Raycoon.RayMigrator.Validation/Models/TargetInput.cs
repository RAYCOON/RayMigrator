// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

public sealed class TargetInput
{
    public string? Alias { get; init; }
    public string? ConnectionString { get; init; }
    public string? UseCliToolAlias { get; init; }

    /// <summary>Resolved via Target -> TargetGroup -> Product -> Defaults cascade.</summary>
    public string? EffectiveUseCliToolAlias { get; init; }

    /// <summary>Parameter map as configured on the target (not merged with parents).</summary>
    public IReadOnlyDictionary<string, string>? CliToolParameters { get; init; }

    /// <summary>Effective parameter map after inheritance resolution (may include parent-contributed keys).</summary>
    public IReadOnlyDictionary<string, string>? EffectiveCliToolParameters { get; init; }
}
