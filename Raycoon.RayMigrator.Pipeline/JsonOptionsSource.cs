
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Loads RayMigrator configuration from JSON files (appsettings.json hierarchy).
/// Searches up to 4 JSON files: base, environment-specific, product-specific, product+environment-specific.
/// Replaces {ENV:...} placeholders with environment variable values.
/// </summary>
public class JsonOptionsSource : IOptionsSource
{
    private readonly ILogger<JsonOptionsSource>? _logger;
    private readonly string _basePath;

    public JsonOptionsSource() : this(configDir: null) { }

    public JsonOptionsSource(ILogger<JsonOptionsSource> logger) : this(configDir: null, logger: logger) { }

    public JsonOptionsSource(string? configDir, ILogger<JsonOptionsSource>? logger = null)
    {
        _logger = logger;
        _basePath = ResolveBasePath(configDir);
    }

    /// <summary>
    /// Resolves the configuration base path. When configDir is null or empty, the current working directory is used.
    /// Otherwise the directory is validated for existence and resolved to an absolute path.
    /// </summary>
    private static string ResolveBasePath(string? configDir)
    {
        if (string.IsNullOrWhiteSpace(configDir))
            return Directory.GetCurrentDirectory();

        var resolved = Path.GetFullPath(configDir);
        if (!Directory.Exists(resolved))
        {
            throw new ConfigurationValidationException(
                $"The specified configuration directory does not exist: '{resolved}'.");
        }

        return resolved;
    }

    public Task<OptionsSourceResult> LoadAsync(string product, string environment)
    {
        _logger?.LogDebug("Loading RayMigrator configuration for product {Product}, environment {Environment} from base path {BasePath}",
            product, environment, _basePath);

        IConfigurationSection rayMigratorConfigurationSection;
        List<EnvironmentVariableWithMetadata> replacedEnvironmentVariables;
        IConfigurationBuilder configurationBuilder;
        var configFilesSearched = new List<(string Filename, bool Found)>();

        try
        {
            // Read base configuration
            configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(_basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var baseConfigFullPath = Path.Combine(_basePath, "appsettings.json");
            bool baseConfigExists = File.Exists(baseConfigFullPath);
            configFilesSearched.Add((baseConfigFullPath, baseConfigExists));
            _logger?.LogDebug("Configuration file {Filename}: {Found}", baseConfigFullPath, baseConfigExists ? "found" : "not found");

            string envConfigurationFilename = $"appsettings.{environment}.json";
            var envConfigFullPath = Path.Combine(_basePath, envConfigurationFilename);
            bool envConfigExists = !string.IsNullOrWhiteSpace(environment) && File.Exists(envConfigFullPath);
            configFilesSearched.Add((envConfigFullPath, envConfigExists));
            _logger?.LogDebug("Configuration file {Filename}: {Found}", envConfigFullPath, envConfigExists ? "found" : "not found");
            if (envConfigExists)
            {
                configurationBuilder.AddJsonFile(envConfigurationFilename, optional: true, reloadOnChange: true);
            }

            if (!string.IsNullOrWhiteSpace(product))
            {
                string productConfigurationFilename = $"appsettings.{product}.json";
                var productConfigFullPath = Path.Combine(_basePath, productConfigurationFilename);
                bool productConfigExists = File.Exists(productConfigFullPath);
                configFilesSearched.Add((productConfigFullPath, productConfigExists));
                if (productConfigExists)
                {
                    configurationBuilder.AddJsonFile(productConfigurationFilename, optional: true, reloadOnChange: true);
                }

                string productEnvConfigurationFilename = $"appsettings.{product}.{environment}.json";
                var productEnvConfigFullPath = Path.Combine(_basePath, productEnvConfigurationFilename);
                bool productEnvConfigExists = File.Exists(productEnvConfigFullPath);
                configFilesSearched.Add((productEnvConfigFullPath, productEnvConfigExists));
                if (productEnvConfigExists)
                {
                    configurationBuilder.AddJsonFile(productEnvConfigurationFilename, optional: true, reloadOnChange: true);
                }
            }
            else
            {
                throw new ConfigurationValidationException(
                    $"Could not properly read RayMigrator configuration for product [{product ?? "{null}"}] and environment [{environment ?? "{null}"}].");
            }

            IConfigurationRoot rayMigratorConfiguration = configurationBuilder.Build();

            if (!rayMigratorConfiguration.AsEnumerable().Any())
                throw new ConfigurationValidationException("Could not find any RayMigrator configuration in provided configuration files.");

            rayMigratorConfigurationSection = rayMigratorConfiguration.GetSection(InternalConstants.RayMigratorSectionName);

            // Replace {ENV:...} placeholders with environment variable values
            replacedEnvironmentVariables = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(rayMigratorConfigurationSection);
            _logger?.LogDebug("Environment variable replacement completed: {Count} variable(s) replaced", replacedEnvironmentVariables.Count);
        }
        catch (Exception ex) when (ex is not ConfigurationValidationException)
        {
            _logger?.LogError(ex, "Failed to load RayMigrator configuration for environment {Environment}", environment);
            throw new ConfigurationValidationException(
                $"Could not properly read RayMigrator configuration for environment [{environment ?? "{null}"}].", ex);
        }

        var result = new OptionsSourceResult
        {
            RayMigratorConfigSection = rayMigratorConfigurationSection,
            PreBuiltOptions = null, // JSON mode: resolved via DI
            ReplacedEnvironmentVariables = replacedEnvironmentVariables,
            HostConfiguration = configurationBuilder.Build(),
            ModeName = "Standalone mode",
            ConfigFileDiagnostics = configFilesSearched
        };

        return Task.FromResult(result);
    }
}
