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
    /// The assemblies whose types did not load during the discovery, keyed by their path, each with the messages of its
    /// loader exceptions. A DAL built against an earlier version of the engine does not load (it overrides members that
    /// <see cref="DalBase"/> no longer has and lacks those it has now), and the message for an unknown database type
    /// names these failures instead of leaving the DAL silently unknown.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> LoadFailures = new(StringComparer.Ordinal);

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
    /// Scans an assembly for types implementing IDal with a [DatabaseType] attribute. An assembly whose types do not load
    /// is skipped, and its loader exceptions are kept for the message of an unknown database type.
    /// </summary>
    internal static void ScanAssemblyForDals(Assembly assembly)
    {
        List<Type> dalTypes;
        try
        {
            dalTypes = assembly.GetTypes()
                .Where(t => t.IsClass
                            && !t.IsAbstract
                            && typeof(IDal).IsAssignableFrom(t))
                .ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Assembly has unresolvable type dependencies
            RecordLoadFailure(assembly, ex.LoaderExceptions);
            return;
        }

        foreach (var type in dalTypes)
        {
            DatabaseTypeAttribute? attr;
            try
            {
                attr = type.GetCustomAttribute<DatabaseTypeAttribute>();
            }
            catch (TypeLoadException ex)
            {
                RecordLoadFailure(assembly, [ex]);
                continue;
            }

            if (attr != null)
            {
                DalTypeMapping.TryAdd(attr.DatabaseType, type);
            }
        }
    }

    private static void RecordLoadFailure(Assembly assembly, IEnumerable<Exception?> loaderExceptions)
    {
        string source = string.IsNullOrEmpty(assembly.Location) ? assembly.FullName ?? "unnamed assembly" : assembly.Location;
        var messages = loaderExceptions
            .Where(e => e != null)
            .Select(e => e!.Message)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        LoadFailures[source] = messages.Count > 0 ? string.Join(" ", messages) : "its types did not load";
    }

    /// <summary>
    /// The message for a database type no DAL was found for. It names the assemblies whose types did not load and why,
    /// because a DAL among them is the likely one: a DAL built against an earlier version of the engine must be rebuilt.
    /// </summary>
    private static string UnknownDalMessage(string databaseType)
    {
        string message = $"Cannot create specific DataAccessLayer. Unknown DataAccessLayer for DatabaseType [{databaseType}].";
        if (LoadFailures.IsEmpty)
            return message;

        string failures = string.Join(" ", LoadFailures
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"[{f.Key}]: {f.Value}"));
        return $"{message} The types of {LoadFailures.Count} assembly(ies) did not load, so a DataAccessLayer in them is unknown: " +
               $"{failures} A DataAccessLayer built against an earlier version of RayMigrator must be rebuilt against this version.";
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
                if (instance == null)
                {
                    throw new ApplicationStartupException(
                        $"Internal Error: Cannot create DataAccessLayer for DatabaseType [{databaseType}] via [{nameof(DalFactory)}].");
                }

                LoadTransientErrorCodes(instance, databaseType);
                return instance;
            });

            return true;
        }

        // Unknown DatabaseType
        throw new ConfigurationValidationException(UnknownDalMessage(databaseType));
    }

    /// <summary>
    /// Replaces the DAL's built-in transient error codes with the content of
    /// <c>DataAccessLayers/{DatabaseType}/TransientErrorCodes.txt</c> below the application base directory when that
    /// file exists (ADR-022). A missing file keeps the built-in list; a malformed or unreadable file aborts the start
    /// with the file and the line named.
    /// </summary>
    private static void LoadTransientErrorCodes(IDal instance, string databaseType)
    {
        if (instance is not DalBase dal)
            return;

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            if (TransientErrorCodesFile.TryLoad(baseDirectory, databaseType, out string path, out IReadOnlyList<string> codes))
            {
                dal.SetTransientErrorCodes(codes, path);
            }
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationValidationException(
                $"Cannot load the transient error codes for DatabaseType [{databaseType}] from " +
                $"[{TransientErrorCodesFile.GetPath(baseDirectory, databaseType)}]: {ex.Message}", ex);
        }
    }
}
