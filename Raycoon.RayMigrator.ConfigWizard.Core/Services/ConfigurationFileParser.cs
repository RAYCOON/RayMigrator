using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Parses uploaded file(s) and classifies them into a WizardState.
/// IO-free: works with filename->content dictionaries.
/// </summary>
public static class ConfigurationFileParser
{
    private const string AppSettingsPrefix = "appsettings";
    private const string JsonExtension = ".json";

    /// <summary>
    /// Parses a set of named JSON strings (filename -> content) and produces a WizardState.
    /// Classifies files by their appsettings naming convention.
    /// </summary>
    public static WizardState Parse(Dictionary<string, string> files)
    {
        var state = new WizardState();

        if (files.Count == 0)
            return state;

        // Classify all files
        var classified = new List<(string fileName, string content, ConfigFileRole role, string? product, string? environment)>();
        foreach (var (fileName, content) in files)
        {
            var (role, product, environment) = ClassifyFileName(fileName);
            classified.Add((fileName, content, role, product, environment));
        }

        // Parse base file
        var baseFile = classified.FirstOrDefault(c => c.role == ConfigFileRole.Base);
        if (baseFile.content != null)
        {
            try
            {
                state.BaseModel = ConfigurationSerializer.LoadFromJson(baseFile.content, baseFile.fileName);
                state.BaseModel.FileRole = ConfigFileRole.Base;
            }
            catch (Exception) { /* Skip if base file can't be parsed */ }
        }

        // Parse environment files
        foreach (var file in classified.Where(c => c.role == ConfigFileRole.Environment && c.environment != null))
        {
            try
            {
                var envModel = ConfigurationSerializer.LoadFromJson(file.content, file.fileName);
                envModel.FileRole = ConfigFileRole.Environment;
                state.EnvironmentModels[file.environment!] = envModel;
            }
            catch (Exception) { /* Skip files that can't be parsed */ }
        }

        // Parse product files
        foreach (var file in classified.Where(c => c.role == ConfigFileRole.Product && c.product != null))
        {
            try
            {
                var prodModel = ConfigurationSerializer.LoadFromJson(file.content, file.fileName);
                prodModel.FileRole = ConfigFileRole.Product;
                state.ProductModels[file.product!] = prodModel;
            }
            catch (Exception) { /* Skip files that can't be parsed */ }
        }

        // Parse product+environment files
        foreach (var file in classified.Where(c => c.role == ConfigFileRole.ProductEnvironment && c.product != null && c.environment != null))
        {
            try
            {
                string key = $"{file.product}.{file.environment}";
                var peModel = ConfigurationSerializer.LoadFromJson(file.content, file.fileName);
                peModel.FileRole = ConfigFileRole.ProductEnvironment;
                state.ProductEnvironmentModels[key] = peModel;
            }
            catch (Exception) { /* Skip files that can't be parsed */ }
        }

        // Reverse-engineer WizardSetupAnswers
        state.SetupAnswers = ReverseEngineerAnswers(state);

        // Populate CombinationEntries from ProductEnvironmentModels (all imported = not wizard-completed)
        foreach (var key in state.ProductEnvironmentModels.Keys)
        {
            state.CombinationEntries[key] = new ProductEnvironmentEntry { WizardCompleted = false };
        }

        MergeParentLayersIntoPeModels(state);

        return state;
    }

    /// <summary>
    /// Classifies a single filename into a ConfigFileRole.
    /// </summary>
    public static (ConfigFileRole role, string? product, string? environment) ClassifyFileName(string fileName)
    {
        // Normalize: take just the filename, not a path
        fileName = Path.GetFileName(fileName);

        string[] segments = ParseSegments(fileName);

        switch (segments.Length)
        {
            case 0:
                return (ConfigFileRole.Base, null, null);

            case 1:
                // Single segment: could be environment or product.
                // Without additional context, default to environment (most common use case).
                return (ConfigFileRole.Environment, null, segments[0]);

            case 2:
                // appsettings.Product.Environment.json
                return (ConfigFileRole.ProductEnvironment, segments[0], segments[1]);

            default:
                // 3+ segments: first N-1 = Product, last = Environment
                string product = string.Join(".", segments.Take(segments.Length - 1));
                string environment = segments[^1];
                return (ConfigFileRole.ProductEnvironment, product, environment);
        }
    }

    /// <summary>
    /// Parses the middle segments from an appsettings filename.
    /// </summary>
    internal static string[] ParseSegments(string fileName)
    {
        if (!fileName.StartsWith(AppSettingsPrefix, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        string withoutPrefix = fileName[AppSettingsPrefix.Length..];

        if (!withoutPrefix.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        string withoutExtension = withoutPrefix[..^JsonExtension.Length];

        // Remove leading dot if present
        if (withoutExtension.StartsWith('.'))
            withoutExtension = withoutExtension[1..];

        if (string.IsNullOrEmpty(withoutExtension))
            return Array.Empty<string>();

        return withoutExtension.Split('.');
    }

    private static WizardSetupAnswers ReverseEngineerAnswers(WizardState state)
    {
        var answers = new WizardSetupAnswers();

        var baseModel = state.BaseModel;
        answers.RepositoryDatabaseType = baseModel.Repository.DatabaseType;
        answers.UseDatabaseLogging = baseModel.DatabaseLogging != null;
        answers.UseCliTools = baseModel.CliTools.Count > 0;

        // Build per-product environment map from ProductEnvironmentModels keys
        var productEnvironments = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in state.ProductEnvironmentModels.Keys)
        {
            var parts = key.Split('.', 2);
            if (parts.Length == 2)
            {
                if (!productEnvironments.ContainsKey(parts[0]))
                    productEnvironments[parts[0]] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                productEnvironments[parts[0]].Add(parts[1]);
            }
        }

        // Also include standalone environment models as fallback for products without PE entries
        var standaloneEnvironments = new HashSet<string>(state.EnvironmentModels.Keys, StringComparer.OrdinalIgnoreCase);

        // Build product setups from the base model's products
        foreach (var product in baseModel.Products)
        {
            // Use per-product environments if available, otherwise fall back to standalone environments
            var envs = productEnvironments.TryGetValue(product.Alias, out var peEnvs)
                ? peEnvs.OrderBy(e => e).ToList()
                : standaloneEnvironments.OrderBy(e => e).ToList();

            var productSetup = new ProductSetup
            {
                Alias = product.Alias,
                Environments = envs,
            };

            foreach (var tg in product.TargetGroups)
            {
                var tgSetup = new TargetGroupSetup
                {
                    Alias = tg.Alias,
                    DatabaseType = tg.DatabaseType,
                    TargetAliases = tg.Targets.Select(t => t.Alias).ToList(),
                };
                productSetup.TargetGroups.Add(tgSetup);
            }

            answers.Products.Add(productSetup);
        }

        // Also check ProductModels for additional products not in base
        foreach (var (productName, prodModel) in state.ProductModels)
        {
            if (answers.Products.Any(p => string.Equals(p.Alias, productName, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (var product in prodModel.Products)
            {
                var envs = productEnvironments.TryGetValue(product.Alias, out var peEnvs)
                    ? peEnvs.OrderBy(e => e).ToList()
                    : standaloneEnvironments.OrderBy(e => e).ToList();

                var productSetup = new ProductSetup
                {
                    Alias = product.Alias,
                    Environments = envs,
                };

                foreach (var tg in product.TargetGroups)
                {
                    var tgSetup = new TargetGroupSetup
                    {
                        Alias = tg.Alias,
                        DatabaseType = tg.DatabaseType,
                        TargetAliases = tg.Targets.Select(t => t.Alias).ToList(),
                    };
                    productSetup.TargetGroups.Add(tgSetup);
                }

                answers.Products.Add(productSetup);
            }
        }

        return answers;
    }

    /// <summary>
    /// Merges parent configuration layers (base -> env -> product -> PE) into each imported PE model
    /// so the Walk Through Wizard shows effective/inherited values.
    /// Only processes imported PE models (those with PreservedDocument from file import).
    /// Scaffolded PE models (no PreservedDocument) are skipped.
    /// </summary>
    private static void MergeParentLayersIntoPeModels(WizardState state)
    {
        foreach (var key in state.ProductEnvironmentModels.Keys.ToList())
        {
            var peModel = state.ProductEnvironmentModels[key];

            // Only merge imported PE models (scaffolded models have no PreservedDocument
            // and their serialized defaults would contaminate the merge chain)
            if (peModel.PreservedDocument == null) continue;

            var parts = key.Split('.', 2);
            if (parts.Length != 2) continue;

            var product = parts[0];
            var environment = parts[1];

            // Build merge chain using original JSON (avoids model default contamination)
            var jsonChain = new List<string>();

            // 1. Base (lowest priority)
            AddOriginalJsonToChain(jsonChain, state.BaseModel);

            // 2. Environment override
            if (state.EnvironmentModels.TryGetValue(environment, out var envModel))
                AddOriginalJsonToChain(jsonChain, envModel);

            // 3. Product override
            if (state.ProductModels.TryGetValue(product, out var prodModel))
                AddOriginalJsonToChain(jsonChain, prodModel);

            // 4. PE override (highest priority)
            AddOriginalJsonToChain(jsonChain, peModel);

            // Only merge if there are parent layers
            if (jsonChain.Count <= 1) continue;

            // Preserve original PE metadata for round-trip export
            var originalPreservedDoc = peModel.PreservedDocument;
            var originalFilePath = peModel.FilePath;
            var originalFileRole = peModel.FileRole;

            var mergedModel = ConfigFileMerger.MergeChain(jsonChain);
            mergedModel.FilePath = originalFilePath;
            mergedModel.FileRole = originalFileRole;
            mergedModel.PreservedDocument = originalPreservedDoc;

            state.ProductEnvironmentModels[key] = mergedModel;
        }
    }

    private static void AddOriginalJsonToChain(List<string> chain, ConfigurationModel model)
    {
        if (model.PreservedDocument != null)
            chain.Add(model.PreservedDocument.ToJsonString());
        else
            chain.Add(ConfigurationSerializer.ToJson(model));
    }
}
