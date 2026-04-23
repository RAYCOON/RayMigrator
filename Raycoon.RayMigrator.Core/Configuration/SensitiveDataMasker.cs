// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// Centralized masker for sensitive data in log output.
/// Maintains a dictionary of registered sensitive values and replaces them with a mask string.
/// Thread-safe: uses lock for write operations, snapshot-based reads.
/// Supports per-request scoping via AsyncLocal for API mode (parallel requests).
/// CLI uses global state via Initialize(). API uses BeginScope() per request.
/// </summary>
public static class SensitiveDataMasker
{
    public const string MaskString = ConfigurationConstants.NotRevealSensitiveDataString;

    // --- Global state (CLI mode) ---
    private static readonly object Lock = new();
    private static HashSet<string>? _sensitiveValues;
    private static volatile bool _revealSensitiveData;
    private static volatile bool _isInitialized;

    // --- Per-request scope (API mode) ---
    private static readonly AsyncLocal<SensitiveDataMaskingScope?> CurrentScope = new();

    /// <summary>
    /// Initializes the masker globally. Must be called once during startup.
    /// </summary>
    /// <param name="revealSensitiveData">When true, Mask() returns input unchanged.</param>
    public static void Initialize(bool revealSensitiveData)
    {
        lock (Lock)
        {
            _revealSensitiveData = revealSensitiveData;
            _sensitiveValues = new HashSet<string>(StringComparer.Ordinal);
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Creates a new per-request masking scope. Within this scope, Mask() and RegisterSensitiveValue()
    /// use scope-local state instead of global state. Dispose restores the previous scope.
    /// </summary>
    public static IDisposable BeginScope(bool revealSensitiveData)
    {
        var previousScope = CurrentScope.Value;
        var newScope = new SensitiveDataMaskingScope(revealSensitiveData, previousScope);
        CurrentScope.Value = newScope;
        return newScope;
    }

    /// <summary>
    /// Registers a single sensitive value for masking. Null, empty, or whitespace-only values are ignored.
    /// Writes to scope-local state if a scope is active, otherwise to global state.
    /// </summary>
    public static void RegisterSensitiveValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var scope = CurrentScope.Value;
        if (scope != null)
        {
            scope.RegisterSensitiveValue(value);
            return;
        }

        lock (Lock)
        {
            _sensitiveValues?.Add(value);
        }
    }

    /// <summary>
    /// Registers multiple sensitive values for masking.
    /// </summary>
    public static void RegisterSensitiveValues(IEnumerable<string?> values)
    {
        var scope = CurrentScope.Value;
        if (scope != null)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    scope.RegisterSensitiveValue(value);
            }
            return;
        }

        lock (Lock)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    _sensitiveValues?.Add(value);
            }
        }
    }

    /// <summary>
    /// Replaces all registered sensitive values in the input string with <see cref="MaskString"/>.
    /// Checks scope-local state first, then falls back to global state.
    /// Returns input unchanged when RevealSensitiveData is true or when not initialized (fail-open for CLI).
    /// Longer values are replaced first to prevent partial masking.
    /// </summary>
    public static string Mask(string? input)
    {
        if (input is null)
            return string.Empty;

        // Check scope first (API mode)
        var scope = CurrentScope.Value;
        if (scope != null)
            return scope.Mask(input);

        // Fall back to global state (CLI mode)
        if (!_isInitialized || _revealSensitiveData)
            return input;

        // Take a snapshot for thread-safe iteration
        string[] snapshot;
        lock (Lock)
        {
            if (_sensitiveValues is null || _sensitiveValues.Count == 0)
                return input;

            snapshot = _sensitiveValues.ToArray();
        }

        return MaskWithValues(input, snapshot);
    }

    /// <summary>
    /// Registers all sensitive configuration values from options for masking.
    /// Called from CLI (DirectModePipeline) and API (Program.cs, endpoint handlers).
    /// </summary>
    public static void RegisterSensitiveData(RayMigratorOptions options)
    {
        RegisterSensitiveValue(options.Repository?.ConnectionString);
        RegisterSensitiveValue(options.Repository?.SchemaName);
        RegisterSensitiveValue(options.Repository?.TableBaseName);
        RegisterSensitiveValue(options.DatabaseLogging?.ConnectionString);

        foreach (var product in options.Products ?? Enumerable.Empty<ProductOptions>())
        {
            RegisterSensitiveValue(product.MigrationFilesRootDirectory);
            foreach (var tg in product.TargetGroups ?? Enumerable.Empty<TargetGroupOptions>())
                foreach (var target in tg.Targets ?? Enumerable.Empty<TargetOptions>())
                    RegisterSensitiveValue(target.ConnectionString);
        }
    }

    /// <summary>
    /// Number of registered sensitive values (global). For testing purposes.
    /// </summary>
    public static int RegisteredValueCount
    {
        get
        {
            lock (Lock)
            {
                return _sensitiveValues?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// Resets the masker to its uninitialized state. For unit testing only.
    /// </summary>
    internal static void Reset()
    {
        lock (Lock)
        {
            _sensitiveValues = null;
            _revealSensitiveData = false;
            _isInitialized = false;
        }
        CurrentScope.Value = null;
    }

    /// <summary>
    /// Shared masking logic: sort by length descending and replace.
    /// </summary>
    private static string MaskWithValues(string input, string[] sensitiveValues)
    {
        if (sensitiveValues.Length == 0)
            return input;

        // Sort by length descending to replace longer values first
        Array.Sort(sensitiveValues, (a, b) => b.Length.CompareTo(a.Length));

        var result = input;
        foreach (var sensitiveValue in sensitiveValues)
        {
            result = result.Replace(sensitiveValue, MaskString);
        }

        return result;
    }

    /// <summary>
    /// Per-request masking scope with its own revealSensitiveData flag and sensitive values set.
    /// Disposes by restoring the previous scope on the AsyncLocal.
    /// </summary>
    private sealed class SensitiveDataMaskingScope : IDisposable
    {
        private readonly bool _revealSensitiveData;
        private readonly SensitiveDataMaskingScope? _previousScope;
        private readonly object _lock = new();
        private readonly HashSet<string> _sensitiveValues = new(StringComparer.Ordinal);

        internal SensitiveDataMaskingScope(bool revealSensitiveData, SensitiveDataMaskingScope? previousScope)
        {
            _revealSensitiveData = revealSensitiveData;
            _previousScope = previousScope;
        }

        internal void RegisterSensitiveValue(string value)
        {
            lock (_lock)
            {
                _sensitiveValues.Add(value);
            }
        }

        internal string Mask(string input)
        {
            if (_revealSensitiveData)
                return input;

            string[] snapshot;
            lock (_lock)
            {
                if (_sensitiveValues.Count == 0)
                    return input;

                snapshot = _sensitiveValues.ToArray();
            }

            return MaskWithValues(input, snapshot);
        }

        public void Dispose()
        {
            CurrentScope.Value = _previousScope;
        }
    }
}
