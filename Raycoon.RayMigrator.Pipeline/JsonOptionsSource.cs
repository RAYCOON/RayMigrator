using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Shared.Configuration;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Loads RayMigrator configuration from the appsettings hierarchy: base, environment-specific, product-specific
/// and product+environment-specific file, in that order (<see cref="ConfigurationFileChain"/>).
/// The files are merged as JSON documents with <see cref="ConfigurationJsonMerger"/> before they enter the
/// configuration builder, so arrays whose elements carry an <c>Alias</c> merge by alias and every other array
/// is replaced by the later file (#23). Replaces {ENV:...} placeholders with environment variable values.
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
        IConfigurationRoot hostConfiguration;
        var configFilesSearched = new List<(string Filename, bool Found)>();

        try
        {
            if (string.IsNullOrWhiteSpace(product))
            {
                throw new ConfigurationValidationException(
                    $"Could not properly read RayMigrator configuration for product [{product ?? "{null}"}] and environment [{environment ?? "{null}"}].");
            }

            JsonNode merged = MergeConfigurationFiles(_basePath, product, environment, configFilesSearched, _logger);
            byte[] mergedJson = Encoding.UTF8.GetBytes(merged.ToJsonString());

            IConfigurationRoot rayMigratorConfiguration = BuildConfiguration(mergedJson);

            if (!rayMigratorConfiguration.AsEnumerable().Any())
                throw new ConfigurationValidationException("Could not find any RayMigrator configuration in provided configuration files.");

            rayMigratorConfigurationSection = rayMigratorConfiguration.GetSection(InternalConstants.RayMigratorSectionName);

            // Replace {ENV:...} placeholders with environment variable values
            replacedEnvironmentVariables = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(rayMigratorConfigurationSection);
            _logger?.LogDebug("Environment variable replacement completed: {Count} variable(s) replaced", replacedEnvironmentVariables.Count);

            // The host configuration is a second, untouched build of the same merged document.
            hostConfiguration = BuildConfiguration(mergedJson);
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
            HostConfiguration = hostConfiguration,
            ModeName = "Standalone mode",
            ConfigFileDiagnostics = configFilesSearched
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reads the files of the hierarchy that exist under <paramref name="basePath"/> and merges them with
    /// <see cref="ConfigurationJsonMerger.MergeChain"/>. Every file name of the chain is reported in
    /// <paramref name="diagnostics"/> with its full path and whether it was found.
    /// </summary>
    internal static JsonNode MergeConfigurationFiles(
        string basePath,
        string product,
        string environment,
        List<(string Filename, bool Found)> diagnostics,
        ILogger? logger = null)
    {
        var documents = new List<JsonNode?>();

        foreach (var (_, fileName) in ConfigurationFileChain.FileNamesFor(product, environment))
        {
            string fullPath = Path.Combine(basePath, fileName);
            bool exists = File.Exists(fullPath);
            diagnostics.Add((fullPath, exists));
            logger?.LogDebug("Configuration file {Filename}: {Found}", fullPath, exists ? "found" : "not found");

            if (exists)
                documents.Add(ConfigurationJsonMerger.Parse(File.ReadAllText(fullPath, Encoding.UTF8)));
        }

        return ConfigurationJsonMerger.MergeChain(documents);
    }

    private static IConfigurationRoot BuildConfiguration(byte[] mergedJson)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(mergedJson))
            .Build();
    }
}
