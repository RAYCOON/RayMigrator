// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Represents a value that can optionally override a default from the parent level.
/// </summary>
public class OverridableValue<T>
{
    public bool IsOverridden { get; set; }
    public T? Value { get; set; }

    public T GetEffectiveValue(T defaultValue) =>
        IsOverridden && Value is not null ? Value : defaultValue;
}
