// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Single validation rule contract. Implementations are <c>internal sealed</c> and
/// registered by hand in <see cref="RuleCatalog"/> (no reflection discovery).
/// </summary>
internal interface IValidationRule
{
    void Execute(ValidationInput input, ValidationReport report);
}
