// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>Repository or DatabaseLogging section snapshot for validation.</summary>
public sealed class RepositoryInput
{
    public string? DatabaseType { get; init; }
    public string? ConnectionString { get; init; }
    public string? SchemaName { get; init; }
    public string? TableBaseName { get; init; }
}
