// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

public sealed class CliToolInput
{
    public string? Alias { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ArgumentTemplate { get; init; }
    public string? InputMode { get; init; }
    public IReadOnlyList<string>? SuccessExitCodes { get; init; }
    public int? CliToolTimeoutInSeconds { get; init; }
}
