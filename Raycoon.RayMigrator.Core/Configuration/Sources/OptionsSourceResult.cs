using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;

namespace Raycoon.RayMigrator.Core.Configuration.Sources;

/// <summary>
/// Result of loading configuration from an <see cref="IOptionsSource"/>.
/// Contains everything the execution pipeline needs to build the DI host and run migrations.
/// </summary>
public class OptionsSourceResult
{
    /// <summary>
    /// The RayMigrator configuration section. Used for Serilog configuration reading
    /// and verbose configuration output.
    /// </summary>
    public required IConfigurationSection RayMigratorConfigSection { get; init; }

    /// <summary>
    /// The RayMigrator configuration section as written, before <c>{ENV:...}</c> placeholders were resolved
    /// (JSON mode). <see cref="Validation.RayMigratorOptionsValidator"/> reads the connection strings from it, so
    /// that RULE_7_3 reports a hardcoded credential only when the file contains one and not when the credential
    /// enters through a placeholder (#30). Null when the source has no unresolved form (Admin-DB mode); the bound
    /// values are checked as they are then.
    /// </summary>
    public IConfigurationSection? RayMigratorConfigSectionAsWritten { get; init; }

    /// <summary>
    /// Pre-built RayMigratorOptions (Admin-DB mode). When null, options are resolved
    /// from DI via IOptions binding and DataAnnotations validation (JSON mode).
    /// </summary>
    public RayMigratorOptions? PreBuiltOptions { get; init; }

    /// <summary>
    /// Environment variables that were replaced during configuration loading.
    /// Empty for Admin-DB mode (no env var replacement on loaded values currently).
    /// </summary>
    public List<EnvironmentVariableWithMetadata> ReplacedEnvironmentVariables { get; init; } = new();

    /// <summary>
    /// Configuration root to add to the DI host builder (JSON mode only).
    /// When null, no additional configuration is added to the host.
    /// </summary>
    public IConfigurationRoot? HostConfiguration { get; init; }

    /// <summary>
    /// Display name for log messages (e.g. "Standalone mode", "Managed mode (local)").
    /// </summary>
    public required string ModeName { get; init; }

    /// <summary>
    /// Configuration file search diagnostics for error messages (JSON mode only).
    /// Contains the filenames searched and whether each was found.
    /// </summary>
    public List<(string Filename, bool Found)>? ConfigFileDiagnostics { get; init; }
}
