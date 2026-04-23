// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Expected values for a MigrationRun record in the repository.
/// Only non-null fields are asserted, allowing partial matching.
/// </summary>
public record MigrationRunExpectation
{
    public int? MigrationRunResultId { get; init; }
    public int? EnvironmentId { get; init; }
    public string? FromReleaseVersion { get; init; }
    public string? ToReleaseVersion { get; init; }
}
