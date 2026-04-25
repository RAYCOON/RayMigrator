using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Templates;

public class TemplateCache
{
    private readonly RayMigratorOptions? _options;
    private readonly bool _revealSensitiveData;
    private readonly ILogger _logger;

    private Dictionary<string, Dictionary<TemplateType, Template>> _templateDictionary = null!;

    /// <summary>
    /// Constructor. Decoupled from MigrationContext for API compatibility.
    /// When validateConfiguration is true (default), validates that configured DatabaseTypes have matching templates.
    /// Set to false when Products/Repository config is not yet known (Admin-DB mode).
    /// </summary>
    /// <param name="options">RayMigrator configuration options. Can be null when validation is deferred.</param>
    /// <param name="revealSensitiveData">Whether to include sensitive data in log output.</param>
    /// <param name="logger"></param>
    /// <param name="validateConfiguration">Whether to validate Products/Repository against loaded templates at construction time.</param>
    public TemplateCache(IOptions<RayMigratorOptions>? options, bool revealSensitiveData, ILogger<TemplateCache> logger, bool validateConfiguration = true)
    {
        _options = options?.Value;
        _revealSensitiveData = revealSensitiveData;
        _logger = logger;

        Initialize();

        if (validateConfiguration && _options != null)
        {
            ValidateConfigurationAgainstTemplateCache(_options);
        }
    }

    #region PrivateMethods

    /// <summary>
    /// Initialize the TemplateCache from DataAccessLayer template-files on the filesystem.
    /// Loads templates from DataAccessLayers/{Type}/ directories in the application output directory.
    /// </summary>
    private void Initialize()
    {
        _logger.LogDebug("Initializing TemplateCache...");

        Dictionary<string, Dictionary<TemplateType, Template>> templateDictionary = new Dictionary<string, Dictionary<TemplateType, Template>>();
        int numberOfTemplateTypeItems = Enum.GetValues(typeof(TemplateType)).Length;

        // DataAccessLayers directory is in the output directory, propagated transitively via <Content> from DAL projects
        string dataAccessLayersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationConstants.DatabaseAccessLayersRootDirectory);

        bool filesystemAvailable = Directory.Exists(dataAccessLayersPath)
                                   && Directory.GetDirectories(dataAccessLayersPath).Length > 0;

        if (filesystemAvailable)
        {
            _logger.LogDebug("Loading templates from filesystem: {Path}", dataAccessLayersPath);
            LoadTemplatesFromFilesystem(dataAccessLayersPath, templateDictionary);
        }

        if (templateDictionary.Count == 0)
        {
            throw new ConfigurationValidationException(
                $"Error in {nameof(TemplateCache)} initialization: No DataAccessLayer templates found. " +
                $"Ensure the DataAccessLayers/ directory exists at [{dataAccessLayersPath}] " +
                $"and contains subdirectories with SQL template files for each database type.");
        }

        // Validate template completeness for each loaded DAL
        var allowedTemplateTypes = typeof(TemplateType).AllowedValues();
        foreach (var kvp in templateDictionary)
        {
            string databaseType = kvp.Key;
            var fileDict = kvp.Value;

            if (fileDict.Count != numberOfTemplateTypeItems - 1)
            {
                List<string> missingTemplates = new List<string>();
                foreach (TemplateType templateType in Enum.GetValues(typeof(TemplateType)))
                {
                    if (templateType != TemplateType.Undefined
                        && fileDict.All(x => x.Value.TemplateType != templateType))
                    {
                        missingTemplates.Add(Enum.GetName(templateType) + ".sql");
                    }
                }

                throw new ConfigurationValidationException(
                    $"Error in {nameof(TemplateCache)} initialization: Could not find the following templates for DataAccessLayer [{databaseType}]: " +
                    string.Join(", ", missingTemplates));
            }
        }

        _templateDictionary = templateDictionary;
        _logger.LogDebug("{NameOfTemplateCache} successfully initialized", nameof(TemplateCache));
    }

    /// <summary>
    /// Loads templates from the filesystem (DataAccessLayers/{Type}/ directories).
    /// </summary>
    private void LoadTemplatesFromFilesystem(string dataAccessLayersPath,
        Dictionary<string, Dictionary<TemplateType, Template>> templateDictionary)
    {
        var allowedTemplateTypes = typeof(TemplateType).AllowedValues();

        foreach (var subDir in Directory.GetDirectories(dataAccessLayersPath))
        {
            string databaseType = Path.GetFileName(subDir);

            if (_revealSensitiveData)
            {
                _logger.LogDebug("DataAccessLayer [{Dal}] found in directory [{SubDir}]", databaseType, subDir);
            }
            else
            {
                _logger.LogDebug("DataAccessLayer [{Dal}] found", databaseType);
            }

            var fileDict = new Dictionary<TemplateType, Template>();

            foreach (var file in Directory.GetFiles(subDir, "*.sql"))
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string filenameWithExtension = Path.GetFileName(file);

                if (allowedTemplateTypes.Contains(filename))
                {
                    _logger.LogDebug("Reading template-file [{FileName}]", filenameWithExtension);
                }
                else
                {
                    _logger.LogDebug("Skipping unrecognized template-file [{FileName}]", filenameWithExtension);
                    continue;
                }

                if (Enum.TryParse(filename, true, out TemplateType templateType) && templateType != TemplateType.Undefined)
                {
                    string content = File.ReadAllText(file);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        throw new ConfigurationValidationException($"Template file [{file}] for DataAccessLayer [{databaseType}] contains no data. Please put an explaining remark into this template file if it is not needed for this specific DatabaseType!");
                    }

                    string? replacedContent = ProcessEnvironmentVariables(content, databaseType, filenameWithExtension);

                    _logger.LogTrace("File [{FileName}] read with content:\n[{Content}]", filenameWithExtension, SensitiveDataMasker.Mask(replacedContent));

                    var template = new Template
                    {
                        TemplateType = templateType,
                        DatabaseType = databaseType,
                        Filename = filenameWithExtension,
                        Content = replacedContent!
                    };
                    fileDict[templateType] = template;
                }
            }

            if (fileDict.Count > 0)
            {
                templateDictionary[databaseType] = fileDict;
            }
        }
    }

    /// <summary>
    /// Processes environment variable replacements in template content.
    /// </summary>
    private string? ProcessEnvironmentVariables(string content, string databaseType, string filenameWithExtension)
    {
        bool containsEnvironmentVariables = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            content,
            out var replacedContent,
            out List<EnvironmentVariableReplacer.VariableReplacement> variableReplacements);

        if (containsEnvironmentVariables)
        {
            try
            {
                _logger.LogTrace("Template [{FileName}] raw content before ENV replacement:\n[{Content}]",
                    filenameWithExtension, content);

                bool isEnvironmentVariableReplacementSuccessful = true;
                foreach (var replacedEnvironmentVariable in variableReplacements)
                {
                    if (!string.IsNullOrWhiteSpace(replacedEnvironmentVariable.VariableValue))
                    {
                        if (_revealSensitiveData)
                        {
                            _logger.LogDebug("Replaced parts of template-content in [{SubDirName}]\\[{FileNameWithExtension}] from environment-variable [{EnvVarName}] with value [{EnvVarValue}]", databaseType, filenameWithExtension, replacedEnvironmentVariable.VariableName, replacedEnvironmentVariable.VariableValue);
                        }
                        else
                        {
                            _logger.LogDebug("Replaced parts of template-content in [{SubDirName}]\\[{FileNameWithExtension}] from environment-variable(s)", databaseType, filenameWithExtension);
                        }
                    }
                    else
                    {
                        isEnvironmentVariableReplacementSuccessful = false;
                        _logger.LogError(
                            "Could not replace parts of template-content in [{SubDirName}]\\[{FileNameWithExtension}] from environment-variable [{EnvVarName}]: " +
                            "Environment-variable does not exist, is empty or contains whitespaces only", databaseType, filenameWithExtension, replacedEnvironmentVariable.VariableName);
                    }
                }

                if (!isEnvironmentVariableReplacementSuccessful)
                {
                    throw new ConfigurationValidationException("Error(s) replacing configuration variables from environment variables. Check the previous errors for environment-variable replacements that need to be fixed.");
                }
            }
            catch (Exception ex) when (ex is not ConfigurationValidationException)
            {
                throw new ConfigurationValidationException($"Error replacing environment-variable placeholders in sql-template [{filenameWithExtension}]", ex);
            }
        }

        return replacedContent;
    }

    /// <summary>
    /// Validates the TemplateCache against the configured usage. Can be called on-demand
    /// when Products/Repository configuration becomes available (e.g. from Admin-DB).
    /// </summary>
    /// <param name="options">The RayMigratorOptions to validate against loaded templates.</param>
    public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options)
    {
        // Get List of all available DatabaseTypes (e.g. "MSSqlServer", "MariaDb", "PostgreSQL")
        List<string> availableDatabaseTypes = GetAvailableDatabaseTypes();

        // Get DatabaseType of Repository (e.g. "MSSqlServer")
        string? repositoryDatabaseType = options.Repository!.DatabaseType;

        if (string.IsNullOrWhiteSpace(repositoryDatabaseType))
        {
            throw new ConfigurationValidationException($"Missing [DatabaseType]-property for RayMigrator's [Repository]." +
                                                       $"You may use one of the following DatabaseTypes in your DatabaseType-configuration: [{string.Join(',', availableDatabaseTypes)}]");
        }

        var containsDatabaseType = availableDatabaseTypes.Any(s => string.Equals(s, repositoryDatabaseType, StringComparison.OrdinalIgnoreCase));

        if (!containsDatabaseType)
        {
            throw new ConfigurationValidationException($"Configured DatabaseType [{repositoryDatabaseType}] of the Repository could not be found in DataAccessLayer's template directory. " +
                                                $"Please use one of the following DatabaseTypes in your configuration: [{string.Join(',', availableDatabaseTypes)}]");
        }

        foreach (var product in options.Products!)
        {
            foreach (TargetGroupOptions targetGroupOptions in product.TargetGroups!)
            {
                // Get DatabaseType of TargetGroup (e.g. "MSSqlServer")
                string? targetGroupDatabaseType = targetGroupOptions.DatabaseType;

                if (string.IsNullOrWhiteSpace(targetGroupDatabaseType))
                {
                    throw new ConfigurationValidationException($"Missing [DatabaseType]-property for [TargetGroup] with Alias [{targetGroupOptions.Alias}] in Product [{product.Alias}]. " +
                                                               $"You may use one of the following DatabaseTypes in your DatabaseType-configuration: [{string.Join(',', availableDatabaseTypes)}]");
                }

                containsDatabaseType = availableDatabaseTypes.Any(s => string.Equals(s, targetGroupDatabaseType, StringComparison.OrdinalIgnoreCase));

                if (!containsDatabaseType)
                {
                    throw new ConfigurationValidationException($"Configured DatabaseType [{targetGroupDatabaseType}] of TargetGroup with Alias [{targetGroupOptions.Alias}] in Product [{product.Alias}] could not be found in DataAccessLayer's template directory. " +
                                                               $"Please use one of the following DatabaseTypes in your configuration: [{string.Join(',', availableDatabaseTypes)}]");
                }
            }
        }
    }

    /// <summary>
    /// Gets all DAL-names like MSSqlServer, Oracle, etc.
    /// </summary>
    /// <returns>A list of available, configured DAL-names like MSSqlServer, Oracle, etc.</returns>
    public List<string> GetAvailableDatabaseTypes()
    {
        return _templateDictionary.Keys.ToList();
    }

    #endregion PrivateMethods

    /// <summary>
    /// Get a Template by using dynamic configuration-variable substitution passing a T propertyClass.
    /// </summary>
    /// <param name="databaseType"></param>
    /// <param name="templateType"></param>
    /// <param name="propertyClass"></param>
    /// <returns></returns>
    public Template GetTemplate<T>(string databaseType, TemplateType templateType, T propertyClass)
    {
        var originalTemplate = _templateDictionary[databaseType][templateType];
        var newTemplate = new Template
        {
            TemplateType = originalTemplate.TemplateType,
            DatabaseType = originalTemplate.DatabaseType,
            Filename = originalTemplate.Filename,
            Content = originalTemplate.Content.ReplacePlaceholdersFromPropertyClass(propertyClass, ConfigurationConstants.ConfigurationVariableRegex, ConfigurationConstants.AllowedTemplateVariableNameReplacements)
        };

        if (newTemplate.Content.Contains(ConfigurationConstants.ConfigurationVariablePrefix))
        {
            throw new ConfigurationValidationException(
                $"Template [{templateType}] for DatabaseType [{databaseType}] contains unreplaced " +
                $"{ConfigurationConstants.ConfigurationVariablePrefix} placeholders after replacement.");
        }

        return newTemplate;
    }

    /// <summary>
    /// Get a Template by using dynamic configuration-variable substitution passing a T propertyClass.
    /// </summary>
    /// <param name="templateType"></param>
    /// <param name="propertyClass"></param>
    /// <returns></returns>
    public Template GetRepositoryTemplate<T>(TemplateType templateType, T propertyClass) where T : RepositoryOptions
    {
        var originalTemplate = _templateDictionary[propertyClass.DatabaseType!][templateType];
        var newTemplate = new Template
        {
            TemplateType = originalTemplate.TemplateType,
            DatabaseType = originalTemplate.DatabaseType,
            Filename = originalTemplate.Filename,
            Content = originalTemplate.Content.ReplacePlaceholdersFromPropertyClass(propertyClass, ConfigurationConstants.ConfigurationVariableRegex, ConfigurationConstants.AllowedTemplateVariableNameReplacements)
        };

        if (newTemplate.Content.Contains(ConfigurationConstants.ConfigurationVariablePrefix))
        {
            throw new ConfigurationValidationException(
                $"Repository template [{templateType}] for DatabaseType [{propertyClass.DatabaseType}] contains unreplaced " +
                $"{ConfigurationConstants.ConfigurationVariablePrefix} placeholders after replacement.");
        }

        return newTemplate;
    }

    /// <summary>
    /// Get a Template by using dynamic configuration-variable substitution passing a T propertyClass.
    /// </summary>
    /// <param name="databaseType"></param>
    /// <param name="templateType"></param>
    /// <param name="propertyClass"></param>
    /// <returns></returns>
    public string GetTemplateContent<T>(string databaseType, TemplateType templateType, T propertyClass)
    {
        var originalContent = _templateDictionary[databaseType][templateType].Content;
        return originalContent.ReplacePlaceholdersFromPropertyClass(propertyClass, ConfigurationConstants.ConfigurationVariableRegex, ConfigurationConstants.AllowedTemplateVariableNameReplacements);
    }
}
