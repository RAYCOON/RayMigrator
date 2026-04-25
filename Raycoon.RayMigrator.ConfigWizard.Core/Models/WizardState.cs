namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Top-level container for the wizard's in-memory state.
/// Manages multi-file configuration (base + per-environment + per-product + per-product-environment).
/// </summary>
public class WizardState
{
    /// <summary>The base configuration (appsettings.json).</summary>
    public ConfigurationModel BaseModel { get; set; } = new();

    /// <summary>Environment-specific overrides, keyed by environment name (appsettings.{Env}.json).</summary>
    public Dictionary<string, ConfigurationModel> EnvironmentModels { get; set; } = new();

    /// <summary>Product-specific overrides, keyed by product alias (appsettings.{Product}.json).</summary>
    public Dictionary<string, ConfigurationModel> ProductModels { get; set; } = new();

    /// <summary>Product+Environment overrides, keyed by "{Product}.{Env}" (appsettings.{Product}.{Env}.json).</summary>
    public Dictionary<string, ConfigurationModel> ProductEnvironmentModels { get; set; } = new();

    /// <summary>Tracks wizard completion per product+environment combination, keyed by "{Product}.{Env}".</summary>
    public Dictionary<string, ProductEnvironmentEntry> CombinationEntries { get; set; } = new();

    /// <summary>The initial answers that seeded this state.</summary>
    public WizardSetupAnswers SetupAnswers { get; set; } = new();
}
