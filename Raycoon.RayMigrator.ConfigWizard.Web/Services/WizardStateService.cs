using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>Fixed step identifiers, independent of display order and Expert Mode visibility.</summary>
public enum WizardStepId
{
    Repository = 0,
    DatabaseLogging = 1,
    CliTools = 2,
    ProductDefaults = 3,
    ProductSettings = 4,
    Serilog = 5
}

/// <summary>
/// Central state management for the wizard. Wraps Core's WizardState and adds UI state.
/// Scoped lifetime -- one instance per browser tab.
/// </summary>
public class WizardStateService
{
    // ── Core state ──────────────────────────────────────────────
    public WizardState State { get; internal set; } = new();
    public WizardSetupAnswers Answers { get; set; } = new();

    // ── UI state ────────────────────────────────────────────────
    public WizardPhase CurrentPhase { get; private set; } = WizardPhase.Start;
    public WizardStepId CurrentStepId { get; set; } = WizardStepId.Repository;

    /// <summary>Validation cache for the current base model.</summary>
    public WizardValidationResult? LastValidationResult { get; private set; }

    /// <summary>Whether an existing configuration was imported (affects flow).</summary>
    public bool IsImported { get; private set; }

    /// <summary>Whether Expert Mode is active (shows all fields). Easy Mode by default.</summary>
    public bool IsExpertMode { get; set; }

    /// <summary>Tracks visited wizard steps per product+environment combination using fixed step IDs.</summary>
    private readonly Dictionary<string, HashSet<WizardStepId>> _visitedStepsByCombo = new();

    public HashSet<WizardStepId> GetVisitedSteps()
    {
        if (string.IsNullOrEmpty(SelectedProductAlias) || string.IsNullOrEmpty(SelectedEnvironmentName))
            return new HashSet<WizardStepId> { WizardStepId.Repository };

        var key = $"{SelectedProductAlias}.{SelectedEnvironmentName}";
        if (!_visitedStepsByCombo.TryGetValue(key, out var steps))
        {
            steps = new HashSet<WizardStepId> { WizardStepId.Repository };
            _visitedStepsByCombo[key] = steps;
        }
        return steps;
    }

    /// <summary>Currently selected product alias on the Hub page.</summary>
    public string? SelectedProductAlias { get; set; }

    /// <summary>Currently selected environment name on the Hub page.</summary>
    public string? SelectedEnvironmentName { get; set; }


    /// <summary>Fires when state changes and UI should re-render.</summary>
    public event Action? StateChanged;

    // ── Phase transitions ──────────────────────────────────────��─

    /// <summary>
    /// Scaffolds a minimal default configuration and transitions to Guided Configuration.
    /// Replaces the old interview flow.
    /// </summary>
    public void StartNewConfiguration()
    {
        State = ConfigurationScaffolder.ScaffoldMinimal();
        Answers = State.SetupAnswers;
        CurrentPhase = WizardPhase.Hub;
        CurrentStepId = WizardStepId.Repository;
        LastValidationResult = null;
        SelectedProductAlias = null;
        SelectedEnvironmentName = null;
        NotifyStateChanged();
    }

    public void GoToOverview()
    {
        CurrentPhase = WizardPhase.Overview;
        CurrentStepId = WizardStepId.Repository;
        NotifyStateChanged();
    }

    public void GoToGuidedConfig()
    {
        CurrentPhase = WizardPhase.GuidedConfig;
        CurrentStepId = WizardStepId.Repository;
        NotifyStateChanged();
    }

    public void GoToStart()
    {
        CurrentPhase = WizardPhase.Start;
        CurrentStepId = WizardStepId.Repository;
        IsExpertMode = false;
        _visitedStepsByCombo.Clear();
        NotifyStateChanged();
    }

    public void GoToHub()
    {
        CurrentPhase = WizardPhase.Hub;
        CurrentStepId = WizardStepId.Repository;
        NotifyStateChanged();
    }

    /// <summary>
    /// Enters Detailed Configuration for a specific product+environment combination.
    /// Scaffolds a combination model if it doesn't exist yet.
    /// </summary>
    public void StartDetailedConfiguration(string productAlias, string environmentName)
    {
        SelectedProductAlias = productAlias;
        SelectedEnvironmentName = environmentName;

        string key = $"{productAlias}.{environmentName}";

        // Scaffold combination model if not yet created or if existing model is empty (from ScaffoldMinimal)
        if (!State.ProductEnvironmentModels.TryGetValue(key, out var existingModel) || existingModel.Products.Count == 0)
        {
            State.ProductEnvironmentModels[key] = ConfigurationScaffolder.ScaffoldCombination(productAlias, environmentName, State.BaseModel);
        }

        // Ensure CombinationEntry exists
        if (!State.CombinationEntries.ContainsKey(key))
        {
            State.CombinationEntries[key] = new ProductEnvironmentEntry();
        }

        CurrentPhase = WizardPhase.GuidedConfig;
        CurrentStepId = WizardStepId.Repository;
        NotifyStateChanged();
    }

    /// <summary>
    /// Marks the current combination as wizard-completed and returns to Hub.
    /// </summary>
    public void CompleteDetailedConfiguration()
    {
        if (SelectedProductAlias != null && SelectedEnvironmentName != null)
        {
            string key = $"{SelectedProductAlias}.{SelectedEnvironmentName}";
            if (State.CombinationEntries.TryGetValue(key, out var entry))
            {
                entry.WizardCompleted = true;
            }
        }

        CurrentPhase = WizardPhase.Hub;
        CurrentStepId = WizardStepId.Repository;
        NotifyStateChanged();
    }

    /// <summary>
    /// Returns the ConfigurationModel for the currently selected product+environment combination.
    /// </summary>
    public ConfigurationModel? GetSelectedCombinationModel()
    {
        if (SelectedProductAlias == null || SelectedEnvironmentName == null)
            return null;

        string key = $"{SelectedProductAlias}.{SelectedEnvironmentName}";
        return State.ProductEnvironmentModels.TryGetValue(key, out var model) ? model : null;
    }

    // ── Product/Environment CRUD ─────────────────────────────────

    /// <summary>Adds a new product with the given alias.</summary>
    public void AddProduct(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        // Avoid duplicates
        if (State.BaseModel.Products.Any(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase)))
            return;

        State.BaseModel.Products.Add(new ProductModel
        {
            Alias = alias,
            MigrationFilesRootDirectory = $"./Migrations/{alias}",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = $"{{ENV:{SanitizeForEnv(alias)}_BACKEND_MAINDB_CONNECTION_STRING}}" }
                    }
                }
            }
        });

        Answers.Products.Add(new ProductSetup
        {
            Alias = alias,
            Environments = new List<string>(),
            TargetGroups = new List<TargetGroupSetup>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    TargetAliases = new List<string> { "MainDB" }
                }
            },
        });

        NotifyStateChanged();
    }

    /// <summary>Removes a product and cascades to all its combinations.</summary>
    public void RemoveProduct(string alias)
    {
        State.BaseModel.Products.RemoveAll(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase));
        Answers.Products.RemoveAll(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase));

        // Remove all combination entries and models for this product
        var keysToRemove = State.ProductEnvironmentModels.Keys
            .Where(k => k.StartsWith(alias + ".", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            State.ProductEnvironmentModels.Remove(key);
            State.CombinationEntries.Remove(key);
        }

        // Remove product model if exists
        State.ProductModels.Remove(alias);

        // Clean up environment models if no other product uses them
        CleanupOrphanedEnvironmentModels();

        NotifyStateChanged();
    }

    /// <summary>Renames a product alias and cascades to all combination keys, models, and answers.</summary>
    public void RenameProduct(string oldAlias, string newAlias)
    {
        if (string.IsNullOrWhiteSpace(newAlias) || string.Equals(oldAlias, newAlias, StringComparison.Ordinal))
            return;

        // Rename in BaseModel.Products
        var product = State.BaseModel.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, oldAlias, StringComparison.OrdinalIgnoreCase));
        if (product != null)
            product.Alias = newAlias;

        // Rename in Answers.Products
        var setup = Answers.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, oldAlias, StringComparison.OrdinalIgnoreCase));
        if (setup != null)
            setup.Alias = newAlias;

        // Rekey ProductEnvironmentModels and CombinationEntries
        var oldKeys = State.ProductEnvironmentModels.Keys
            .Where(k => k.StartsWith(oldAlias + ".", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var oldKey in oldKeys)
        {
            var env = oldKey[(oldAlias.Length + 1)..];
            var newKey = $"{newAlias}.{env}";

            if (State.ProductEnvironmentModels.TryGetValue(oldKey, out var peModel))
            {
                State.ProductEnvironmentModels.Remove(oldKey);
                peModel.FilePath = $"appsettings.{newAlias}.{env}.json";
                // Update product alias inside the combination model
                var peProduct = peModel.Products.FirstOrDefault();
                if (peProduct != null)
                    peProduct.Alias = newAlias;
                State.ProductEnvironmentModels[newKey] = peModel;
            }

            if (State.CombinationEntries.TryGetValue(oldKey, out var entry))
            {
                State.CombinationEntries.Remove(oldKey);
                State.CombinationEntries[newKey] = entry;
            }
        }

        // Rekey ProductModels if exists
        if (State.ProductModels.TryGetValue(oldAlias, out var prodModel))
        {
            State.ProductModels.Remove(oldAlias);
            State.ProductModels[newAlias] = prodModel;
        }

        // Update selection if it was pointing to the old alias
        if (string.Equals(SelectedProductAlias, oldAlias, StringComparison.OrdinalIgnoreCase))
            SelectedProductAlias = newAlias;

        NotifyStateChanged();
    }

    /// <summary>Renames an environment for a specific product and cascades to all combination keys, models, and answers.</summary>
    public void RenameEnvironment(string productAlias, string oldEnvName, string newEnvName)
    {
        if (string.IsNullOrWhiteSpace(newEnvName) || string.Equals(oldEnvName, newEnvName, StringComparison.Ordinal))
            return;

        // Rename in Answers.Products
        var setup = Answers.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, productAlias, StringComparison.OrdinalIgnoreCase));
        if (setup != null)
        {
            var idx = setup.Environments.FindIndex(e => string.Equals(e, oldEnvName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                setup.Environments[idx] = newEnvName;
        }

        // Rekey ProductEnvironmentModel and CombinationEntry
        var oldKey = $"{productAlias}.{oldEnvName}";
        var newKey = $"{productAlias}.{newEnvName}";

        if (State.ProductEnvironmentModels.TryGetValue(oldKey, out var peModel))
        {
            State.ProductEnvironmentModels.Remove(oldKey);
            peModel.FilePath = $"appsettings.{productAlias}.{newEnvName}.json";
            State.ProductEnvironmentModels[newKey] = peModel;
        }

        if (State.CombinationEntries.TryGetValue(oldKey, out var entry))
        {
            State.CombinationEntries.Remove(oldKey);
            State.CombinationEntries[newKey] = entry;
        }

        // Rekey EnvironmentModel if only this product used the old env name
        var otherUsersOfOldEnv = Answers.Products
            .Where(p => !string.Equals(p.Alias, productAlias, StringComparison.OrdinalIgnoreCase))
            .Any(p => p.Environments.Any(e => string.Equals(e, oldEnvName, StringComparison.OrdinalIgnoreCase)));

        if (!otherUsersOfOldEnv && State.EnvironmentModels.TryGetValue(oldEnvName, out var envModel))
        {
            State.EnvironmentModels.Remove(oldEnvName);
            envModel.FilePath = $"appsettings.{newEnvName}.json";
            State.EnvironmentModels[newEnvName] = envModel;
        }

        // Ensure new env model exists
        if (!State.EnvironmentModels.ContainsKey(newEnvName))
        {
            State.EnvironmentModels[newEnvName] = new ConfigurationModel
            {
                FilePath = $"appsettings.{newEnvName}.json",
                FileRole = ConfigFileRole.Environment,
                Repository = new RepositoryModel
                {
                    ConnectionString = $"{{ENV:REPO_CONNECTION_STRING_{SanitizeForEnv(newEnvName)}}}",
                },
            };
        }

        // Update selection if pointing to old env
        if (string.Equals(SelectedEnvironmentName, oldEnvName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SelectedProductAlias, productAlias, StringComparison.OrdinalIgnoreCase))
            SelectedEnvironmentName = newEnvName;

        NotifyStateChanged();
    }

    /// <summary>Adds an environment to a specific product.</summary>
    public void AddEnvironment(string productAlias, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
            return;

        var productSetup = Answers.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, productAlias, StringComparison.OrdinalIgnoreCase));
        if (productSetup == null)
            return;

        // Avoid duplicates
        if (productSetup.Environments.Any(e => string.Equals(e, environmentName, StringComparison.OrdinalIgnoreCase)))
            return;

        productSetup.Environments.Add(environmentName);

        // Create combination model and entry
        string key = $"{productAlias}.{environmentName}";
        if (!State.ProductEnvironmentModels.ContainsKey(key))
        {
            State.ProductEnvironmentModels[key] = ConfigurationScaffolder.ScaffoldCombination(productAlias, environmentName, State.BaseModel);
        }
        if (!State.CombinationEntries.ContainsKey(key))
        {
            State.CombinationEntries[key] = new ProductEnvironmentEntry();
        }

        // Ensure environment model exists
        if (!State.EnvironmentModels.ContainsKey(environmentName))
        {
            State.EnvironmentModels[environmentName] = new ConfigurationModel
            {
                FilePath = $"appsettings.{environmentName}.json",
                FileRole = ConfigFileRole.Environment,
                Repository = new RepositoryModel
                {
                    ConnectionString = $"{{ENV:REPO_CONNECTION_STRING_{SanitizeForEnv(environmentName)}}}",
                },
            };
        }

        NotifyStateChanged();
    }

    /// <summary>Removes an environment from a specific product.</summary>
    public void RemoveEnvironment(string productAlias, string environmentName)
    {
        var productSetup = Answers.Products.FirstOrDefault(p =>
            string.Equals(p.Alias, productAlias, StringComparison.OrdinalIgnoreCase));
        productSetup?.Environments.RemoveAll(e => string.Equals(e, environmentName, StringComparison.OrdinalIgnoreCase));

        // Remove combination model and entry
        string key = $"{productAlias}.{environmentName}";
        State.ProductEnvironmentModels.Remove(key);
        State.CombinationEntries.Remove(key);

        // Clean up environment model if no other product uses this environment
        CleanupOrphanedEnvironmentModels();

        NotifyStateChanged();
    }

    /// <summary>Validates a specific product+environment combination.</summary>
    public WizardValidationResult ValidateCombination(string productAlias, string environmentName)
    {
        string key = $"{productAlias}.{environmentName}";
        if (State.ProductEnvironmentModels.TryGetValue(key, out var model))
        {
            return ConfigurationValidator.ValidateAll(model);
        }
        return new WizardValidationResult();
    }

    private void CleanupOrphanedEnvironmentModels()
    {
        var usedEnvironments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in State.ProductEnvironmentModels.Keys)
        {
            var parts = key.Split('.', 2);
            if (parts.Length == 2)
                usedEnvironments.Add(parts[1]);
        }

        var orphanedEnvs = State.EnvironmentModels.Keys
            .Where(e => !usedEnvironments.Contains(e))
            .ToList();

        foreach (var env in orphanedEnvs)
        {
            State.EnvironmentModels.Remove(env);
        }
    }

    // ── Structure synchronization ─────────────────────────────────

    /// <summary>
    /// Regenerates environment and product-environment models based on the current
    /// product/environment structure in the base model.
    /// Called when StructureSetupSection changes products or environments.
    /// </summary>
    public void SyncStructure(List<string> allEnvironments)
    {
        // Collect existing keys to preserve user edits
        var existingEnvModels = new Dictionary<string, ConfigurationModel>(State.EnvironmentModels);
        var existingPeModels = new Dictionary<string, ConfigurationModel>(State.ProductEnvironmentModels);

        State.EnvironmentModels.Clear();
        State.ProductEnvironmentModels.Clear();

        // Regenerate environment models
        foreach (var env in allEnvironments.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existingEnvModels.TryGetValue(env, out var existing))
            {
                State.EnvironmentModels[env] = existing;
            }
            else
            {
                State.EnvironmentModels[env] = new ConfigurationModel
                {
                    FilePath = $"appsettings.{env}.json",
                    FileRole = ConfigFileRole.Environment,
                    Repository = new RepositoryModel
                    {
                        ConnectionString = $"{{ENV:REPO_CONNECTION_STRING_{SanitizeForEnv(env)}}}",
                    },
                };
            }
        }

        // Regenerate product-environment models
        foreach (var product in State.BaseModel.Products)
        {
            foreach (var env in allEnvironments.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string key = $"{product.Alias}.{env}";
                if (existingPeModels.TryGetValue(key, out var existing))
                {
                    State.ProductEnvironmentModels[key] = existing;
                }
                else
                {
                    State.ProductEnvironmentModels[key] = new ConfigurationModel
                    {
                        FilePath = $"appsettings.{product.Alias}.{env}.json",
                        FileRole = ConfigFileRole.ProductEnvironment,
                    };
                }
            }
        }

        NotifyStateChanged();
    }

    private static string SanitizeForEnv(string alias)
    {
        return alias.ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('-', '_')
            .Replace('.', '_');
    }

    // ── Import ────────────────────────────────────────────────────

    public void ImportFiles(Dictionary<string, string> files)
    {
        State = ConfigurationFileParser.Parse(files);
        Answers = State.SetupAnswers;
        IsImported = true;
        LastValidationResult = null;
        SelectedProductAlias = null;
        SelectedEnvironmentName = null;
        NotifyStateChanged();
    }

    // ── Validation ────────────────────────────────────────────────

    public WizardValidationResult ValidateSection(string sectionName)
    {
        var model = State.BaseModel;
        return sectionName switch
        {
            "Repository" => ConfigurationValidator.ValidateRepository(model.Repository),
            "DatabaseLogging" when model.DatabaseLogging != null =>
                ConfigurationValidator.ValidateDatabaseLogging(model.DatabaseLogging),
            "ProductDefaults" => ConfigurationValidator.ValidateProductDefaults(model.ProductDefaults),
            "CliTools" => ConfigurationValidator.ValidateCliTools(model.CliTools),
            _ => new WizardValidationResult()
        };
    }

    public WizardValidationResult ValidateAll()
    {
        var aggregated = new WizardValidationResult();
        aggregated.Merge(ConfigurationValidator.ValidateAll(State.BaseModel));

        foreach (var (key, peModel) in State.ProductEnvironmentModels)
        {
            // Empty shells from ConfigurationScaffolder.Scaffold() (placeholder PE entries before the
            // user has visited detailed configuration) have no Products and would produce
            // "no products"-style false positives. Skip them until populated via ScaffoldCombination
            // or an import merge. Anything with Products (scaffolded full model or imported) is
            // self-contained enough to validate.
            if (peModel.Products.Count == 0) continue;

            var peResult = ConfigurationValidator.ValidateAll(peModel);
            PrefixPaths(peResult, $"[{key}] ");
            aggregated.Merge(peResult);
        }

        LastValidationResult = aggregated;
        NotifyStateChanged();
        return aggregated;
    }

    private static void PrefixPaths(WizardValidationResult result, string prefix)
    {
        foreach (var e in result.Errors) e.Path = prefix + e.Path;
        foreach (var e in result.Warnings) e.Path = prefix + e.Path;
    }

    // ── Defaults promotion ────────────────────────────────────────

    public List<PromotionResult> PromoteDefaults()
    {
        // Cross-model promotion: consolidate common values from combination models upward
        var crossModelResults = DefaultsPromoter.PromoteAcrossModels(State);

        // Intra-model promotion: promote within the base model
        var intraModelResults = DefaultsPromoter.Promote(State.BaseModel);

        var results = crossModelResults.Concat(intraModelResults).ToList();
        if (results.Count > 0)
        {
            State.BaseModel.IsModified = true;
            NotifyStateChanged();
        }
        return results;
    }

    // ── Model access helpers ──────────────────────────────────────

    /// <summary>Returns the active configuration model for the base file.</summary>
    public ConfigurationModel BaseModel => State.BaseModel;

    /// <summary>Returns all environment model keys.</summary>
    public IReadOnlyCollection<string> EnvironmentKeys => State.EnvironmentModels.Keys;

    /// <summary>Returns a specific environment model.</summary>
    public ConfigurationModel? GetEnvironmentModel(string env) =>
        State.EnvironmentModels.TryGetValue(env, out var model) ? model : null;

    /// <summary>Returns a specific product-environment model.</summary>
    public ConfigurationModel? GetProductEnvironmentModel(string key) =>
        State.ProductEnvironmentModels.TryGetValue(key, out var model) ? model : null;

    // ── Export JSON (pruned, shared between Overview display and ZIP download) ──

    private Dictionary<string, string>? _exportJsonCache;

    /// <summary>
    /// Returns all pruned export JSON strings, keyed by filename.
    /// Cached until the next state change.
    /// </summary>
    public Dictionary<string, string> GetExportJsons()
    {
        return _exportJsonCache ??= ZipExportService.ComputeExportJsons(State);
    }

    /// <summary>Invalidates the cached export JSONs (called on state changes).</summary>
    internal void InvalidateExportJsonCache() => _exportJsonCache = null;

    /// <summary>Generates the pruned JSON for the base model.</summary>
    public string GetBaseJson()
        => GetExportJsons().GetValueOrDefault("appsettings.json", "{}");

    /// <summary>Generates the pruned JSON for a specific environment model.</summary>
    public string GetEnvironmentJson(string env)
        => GetExportJsons().GetValueOrDefault($"appsettings.{env}.json", "{}");

    /// <summary>Generates the pruned JSON for a specific product-environment model.</summary>
    public string GetProductEnvironmentJson(string key)
        => GetExportJsons().GetValueOrDefault($"appsettings.{key}.json", "{}");

    // ── Restart ────────────────────────────────────────────────────

    public void Reset()
    {
        State = new WizardState();
        Answers = new WizardSetupAnswers();
        CurrentPhase = WizardPhase.Start;
        CurrentStepId = WizardStepId.Repository;
        LastValidationResult = null;
        IsImported = false;
        IsExpertMode = false;
        SelectedProductAlias = null;
        SelectedEnvironmentName = null;
        NotifyStateChanged();
    }

    // ── Notification ──────────────────────────────────────────────

    public void NotifyStateChanged()
    {
        InvalidateExportJsonCache();
        StateChanged?.Invoke();
    }
}

public enum WizardPhase
{
    Start,
    Hub,
    GuidedConfig,
    Overview
}
