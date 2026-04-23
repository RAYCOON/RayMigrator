// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Severity level of a validation issue.
/// Order matches <see cref="Raycoon.RayMigrator.ConfigWizard.Core.Models.ValidationSeverity"/> for backwards compatibility.
/// </summary>
public enum ValidationSeverity
{
    Warning,
    Error
}
