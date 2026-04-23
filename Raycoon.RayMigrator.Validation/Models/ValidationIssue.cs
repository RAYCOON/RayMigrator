// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// A single validation finding. Immutable record so assertions compare by value.
/// </summary>
/// <param name="Code">Rule identifier from <see cref="RuleIds"/> (e.g. "RULE_3_8").</param>
/// <param name="Severity">Error blocks, Warning informs.</param>
/// <param name="Path">Configuration path (e.g. "Products > MyApp > TargetGroups > Backend").</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Path,
    string Message);
