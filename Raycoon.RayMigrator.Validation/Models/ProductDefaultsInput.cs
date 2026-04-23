// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Defaults snapshot that cascade rules (RULE_8_x) compare products/target-groups against.
/// </summary>
public sealed class ProductDefaultsInput
{
    public string? MigrationErrorAction { get; init; }
    public string? RollbackErrorAction { get; init; }
    public string? MigrationFilesExtension { get; init; }
    public string? MigrationRollbackFilesPreExtension { get; init; }
    public string? UseCliToolAlias { get; init; }
    public string? TargetMigrationOrder { get; init; }
    public string? HashValidationScope { get; init; }
}
