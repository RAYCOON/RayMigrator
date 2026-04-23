// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Database;

/// <summary>
/// Factory class for creating and retrieving database-specific Data Access Layer (DAL) instances.
/// Uses DependencyContext for built-in DAL discovery (works with single-file publish) and
/// filesystem scanning for external DAL plugins in DataAccessLayers/ subdirectories.
/// </summary>
public static class DalFactory
{
    /// <summary>
    /// Mapping of database types to their corresponding DAL types.
    /// </summary>
    private static readonly Dictionary<string, Type> DalTypeMapping = new();

    /// <summary>
    /// Cache of created DAL instances, keyed by database type and connection string.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IDal> DalInstances = new();

    /// <summary>
    /// Static constructor to initialize the DAL type mapping via DependencyContext (built-in DALs)
    /// and filesystem scanning (external DAL plugins).
    /// </summary>
    static DalFactory()
    {
        // Mode 0: Discover built-in DAL assemblies from dependency metadata.
        // DependencyContext.Default reads from deps.json. In single-file publish
        // bundles it may return null (IL3002) — we explicitly handle that below
        // and fall through to Mode 1 (filesystem-based discovery).
        // No hardcoded assembly list — any ProjectReference within our namespace
        // is loaded and scanned for IDal implementations via ScanAssemblyForDals.
#pragma warning disable IL3002 // Null return handled by the check on the next line.
        var context = DependencyContext.Default;
#pragma warning restore IL3002
        if (context != null)
        {
            foreach (var lib in context.RuntimeLibraries)
            {
                if (lib.Name.StartsWith("Raycoon.RayMigrator."))
                {
                    try
                    {
                        var assembly = Assembly.Load(new AssemblyName(lib.Name));
                        ScanAssemblyForDals(assembly);
                    }
                    catch (FileNotFoundException) { /* Assembly not available in this deployment */ }
                    catch (FileLoadException) { /* Assembly version conflict, skip */ }
                }
            }
        }

        // Mode 1: Filesystem-based discovery (external DAL plugins)
        string dalRootPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "DataAccessLayers");

        if (Directory.Exists(dalRootPath))
        {
            foreach (string subDir in Directory.GetDirectories(dalRootPath))
            {
                foreach (string dllFile in Directory.GetFiles(subDir, "*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(dllFile);
                        ScanAssemblyForDals(assembly);
                    }
                    catch (BadImageFormatException) { /* Native DLL, skip */ }
                    catch (FileLoadException) { /* Already loaded, skip */ }
                }
            }
        }
    }

    /// <summary>
    /// Provides read-only access to the registered DAL type mapping for testing and diagnostics.
    /// </summary>
    internal static IReadOnlyDictionary<string, Type> RegisteredDalTypes => DalTypeMapping;

    /// <summary>
    /// Scans an assembly for types implementing IDal with a [DatabaseType] attribute.
    /// </summary>
    internal static void ScanAssemblyForDals(Assembly assembly)
    {
        IEnumerable<Type> dalTypes;
        try
        {
            dalTypes = assembly.GetTypes()
                .Where(t => t.IsClass
                            && !t.IsAbstract
                            && typeof(IDal).IsAssignableFrom(t));
        }
        catch (ReflectionTypeLoadException)
        {
            return; // Assembly has unresolvable type dependencies
        }

        foreach (var type in dalTypes)
        {
            var attr = type.GetCustomAttribute<DatabaseTypeAttribute>();
            if (attr != null)
            {
                DalTypeMapping.TryAdd(attr.DatabaseType, type);
            }
        }
    }

    /// <summary>
    /// Tries to retrieve a database-specific Data Access Layer (DAL) instance for the given DatabaseType and ConnectionString.
    /// </summary>
    /// <param name="databaseType">The type of the database (e.g., "Oracle").</param>
    /// <param name="connectionString">The connection string specific to the database type.</param>
    /// <param name="dalInstance">When this method returns, contains the DAL instance associated with the specified database type and connection string, if the instance was successfully created; otherwise, null.</param>
    /// <returns><see langword="true"/> if the DAL instance was successfully created; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetDal(string databaseType, string connectionString, out IDal? dalInstance)
    {
        if (DalTypeMapping.TryGetValue(databaseType, out Type? dalType))
        {
            string instanceKey = $"{databaseType}_{connectionString}";

            dalInstance = DalInstances.GetOrAdd(instanceKey, _ =>
            {
                var instance = (IDal?)Activator.CreateInstance(dalType, connectionString);
                return instance ?? throw new ApplicationStartupException(
                    $"Internal Error: Cannot create DataAccessLayer for DatabaseType [{databaseType}] via [{nameof(DalFactory)}].");
            });

            return true;
        }

        // Unknown DatabaseType
        throw new ConfigurationValidationException($"Cannot create specific DataAccessLayer. Unknown DataAccessLayer for DatabaseType [{databaseType}].");
    }
}
