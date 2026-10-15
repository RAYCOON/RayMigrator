using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Replacer;

namespace Raycoon.RayMigrator.Core.Configuration.Sources;

/// <summary>
/// Result of loading configuration from an <see cref="IOptionsSource"/>.
/// Contains everything the execution pipeline needs to build the DI host and run migrations.
/// </summary>
public class OptionsSourceResult
{
    /// <summary>
    /// The RayMigrator configuration section: the shared sections and the product the run is for, the other
    /// products of the files left out (ADR-026). Used for Serilog configuration reading and verbose
    /// configuration output.
    /// </summary>
    public required IConfigurationSection RayMigratorConfigSection { get; init; }

    /// <summary>
    /// The RayMigrator configuration section as written, before <c>{ENV:...}</c> placeholders were resolved.
    /// <see cref="Validation.RayMigratorOptionsValidator"/> reads the connection strings from it, so
    /// that RULE_7_3 reports a hardcoded credential only when the file contains one and not when the credential
    /// enters through a placeholder (#30). Null when the source has no unresolved form; the bound values are
    /// checked as they are then.
    /// </summary>
    public IConfigurationSection? RayMigratorConfigSectionAsWritten { get; init; }

    /// <summary>
    /// Environment variables that were replaced during configuration loading.
    /// </summary>
    public List<EnvironmentVariableWithMetadata> ReplacedEnvironmentVariables { get; init; } = new();

    /// <summary>
    /// Configuration root to add to the DI host builder.
    /// When null, no additional configuration is added to the host.
    /// </summary>
    public IConfigurationRoot? HostConfiguration { get; init; }

    /// <summary>
    /// Display name for log messages (e.g. "Standalone mode").
    /// </summary>
    public required string ModeName { get; init; }

    /// <summary>
    /// Configuration file search diagnostics for error messages.
    /// Contains the filenames searched and whether each was found.
    /// </summary>
    public List<(string Filename, bool Found)>? ConfigFileDiagnostics { get; init; }
}
