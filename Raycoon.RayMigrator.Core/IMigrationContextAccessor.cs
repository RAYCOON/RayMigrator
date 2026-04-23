// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Provides access to the current MigrationContext.
/// Different implementations support different hosting models (CLI singleton vs API per-request).
/// </summary>
public interface IMigrationContextAccessor
{
    /// <summary>
    /// Gets or sets the current MigrationContext for the execution scope.
    /// </summary>
    MigrationContext Current { get; set; }
}

/// <summary>
/// Singleton-based accessor for CLI usage. Wraps a single MigrationContext instance.
/// </summary>
public class SingletonMigrationContextAccessor : IMigrationContextAccessor
{
    public MigrationContext Current { get; set; } = null!;
}

/// <summary>
/// AsyncLocal-based accessor for API usage. Provides per-request MigrationContext isolation.
/// Analogous to IHttpContextAccessor in ASP.NET Core.
/// </summary>
public class AsyncLocalMigrationContextAccessor : IMigrationContextAccessor
{
    private static readonly AsyncLocal<MigrationContext?> _current = new();

    public MigrationContext Current
    {
        get => _current.Value ?? throw new InvalidOperationException(
            "No MigrationContext available for the current execution context. " +
            "Ensure the context is set before accessing it.");
        set => _current.Value = value;
    }
}
