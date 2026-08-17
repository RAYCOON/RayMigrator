using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Builds an in-memory ZIP archive with all configuration files.
/// </summary>
public class ZipExportService
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>File name of the terms acceptance note inside the exported ZIP.</summary>
    public const string AcceptanceNoteFileName = "TERMS-ACCEPTANCE.txt";

    private readonly FileInteropService _fileInterop;
    private readonly TermsAcceptanceService _terms;

    public ZipExportService(FileInteropService fileInterop, TermsAcceptanceService terms)
    {
        _fileInterop = fileInterop;
        _terms = terms;
    }

    /// <summary>
    /// Exports the current WizardState as a downloadable ZIP file.
    /// </summary>
    public async Task ExportAsync(WizardState state)
    {
        var exportJsons = ComputeExportJsons(state);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, json) in exportJsons)
                AddEntry(archive, fileName, json);

            // example.env -- scan ALL exported files for {ENV:} variables (not just base)
            string envExample = EnvFileGenerator.GenerateFromExportedJsons(exportJsons, _ => null);
            AddEntry(archive, "example.env", envExample);

            // Terms acceptance note — the privacy-compatible record of the
            // click-wrap consent (nothing is transmitted; the note stays with
            // the user). Only written when acceptance actually happened: the
            // note documents a fact and must never fabricate one.
            if (_terms.IsAccepted)
                AddEntry(archive, AcceptanceNoteFileName, _terms.BuildAcceptanceNote());
        }

        memoryStream.Position = 0;
        var bytes = memoryStream.ToArray();
        await _fileInterop.DownloadFileAsync("raymigrator-config.zip", "application/zip", bytes);
    }

    // ── Shared export computation ────────────────────────────────────

    /// <summary>
    /// Computes all export JSON strings with full hierarchy pruning.
    /// Used by both the Overview display and ZIP download to ensure identical output.
    /// Returns a dictionary keyed by filename (e.g., "appsettings.json", "appsettings.Development.json").
    /// </summary>
    public static Dictionary<string, string> ComputeExportJsons(WizardState state)
    {
        // Phase 1: Compute all raw diffs
        string baseJson = ConfigurationSerializer.ToJson(state.BaseModel);

        var envDiffs = new Dictionary<string, string>();
        foreach (var (env, model) in state.EnvironmentModels)
            envDiffs[env] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        var productDiffs = new Dictionary<string, string>();
        foreach (var (product, model) in state.ProductModels)
            productDiffs[product] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        var rawPeDiffs = new Dictionary<string, string>();
        foreach (var (key, model) in state.ProductEnvironmentModels)
            rawPeDiffs[key] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        // Phase 2: Prune PE files (remove values already in env/product parents)
        var prunedPeDiffs = new Dictionary<string, string>();
        foreach (var (key, rawJson) in rawPeDiffs)
        {
            string json = rawJson;
            var parts = key.Split('.', 2);
            if (parts.Length == 2)
            {
                if (envDiffs.TryGetValue(parts[1], out var envJson))
                    json = RemoveRedundantOverrides(json, envJson);
            }

            if (productDiffs.TryGetValue(parts[0], out var prodJson))
                json = RemoveRedundantOverrides(json, prodJson);

            prunedPeDiffs[key] = json;
        }

        // Phase 3: Build combinations for hierarchy pruning
        var combinations = BuildCombinations(state, envDiffs, productDiffs);

        // Phase 4: Prune base file
        if (combinations.Count > 0)
        {
            var childGroups = combinations.Select(c =>
            {
                var group = new List<string>();
                if (c.env != null && envDiffs.TryGetValue(c.env, out var envJson) && !ConfigurationSerializer.IsEmptyDiff(envJson))
                    group.Add(envJson);
                if (c.product != null && productDiffs.TryGetValue(c.product, out var prodJson) && !ConfigurationSerializer.IsEmptyDiff(prodJson))
                    group.Add(prodJson);
                if (c.product != null && c.env != null)
                {
                    string peKey = $"{c.product}.{c.env}";
                    if (rawPeDiffs.TryGetValue(peKey, out var peJson) && !ConfigurationSerializer.IsEmptyDiff(peJson))
                        group.Add(peJson);
                }
                return (IReadOnlyList<string>)group;
            }).ToList();

            baseJson = PruneCoveredProperties(baseJson, childGroups);
        }

        // Phase 5: Prune env files
        foreach (var env in envDiffs.Keys.ToList())
        {
            var productsForEnv = combinations
                .Where(c => c.env == env && c.product != null)
                .Select(c => c.product!)
                .Distinct()
                .ToList();

            if (productsForEnv.Count == 0)
                continue;

            var peChildGroups = new List<IReadOnlyList<string>>();
            bool allProductsCovered = true;
            foreach (var product in productsForEnv)
            {
                string peKey = $"{product}.{env}";
                if (prunedPeDiffs.TryGetValue(peKey, out var peJson) && !ConfigurationSerializer.IsEmptyDiff(peJson))
                    peChildGroups.Add(new List<string> { peJson });
                else
                {
                    allProductsCovered = false;
                    break;
                }
            }

            if (allProductsCovered && peChildGroups.Count > 0)
                envDiffs[env] = PruneCoveredProperties(envDiffs[env], peChildGroups);
        }

        // Phase 6: Prune product files
        foreach (var product in productDiffs.Keys.ToList())
        {
            var envsForProduct = combinations
                .Where(c => c.product == product && c.env != null)
                .Select(c => c.env!)
                .Distinct()
                .ToList();

            if (envsForProduct.Count == 0)
                continue;

            var peChildGroups = new List<IReadOnlyList<string>>();
            bool allEnvsCovered = true;
            foreach (var env in envsForProduct)
            {
                string peKey = $"{product}.{env}";
                if (prunedPeDiffs.TryGetValue(peKey, out var peJson) && !ConfigurationSerializer.IsEmptyDiff(peJson))
                    peChildGroups.Add(new List<string> { peJson });
                else
                {
                    allEnvsCovered = false;
                    break;
                }
            }

            if (allEnvsCovered && peChildGroups.Count > 0)
                productDiffs[product] = PruneCoveredProperties(productDiffs[product], peChildGroups);
        }

        // Phase 7: Build result dictionary
        var result = new Dictionary<string, string>
        {
            ["appsettings.json"] = baseJson
        };

        foreach (var (env, json) in envDiffs)
        {
            if (!ConfigurationSerializer.IsEmptyDiff(json))
                result[$"appsettings.{env}.json"] = json;
        }

        foreach (var (product, json) in productDiffs)
        {
            if (!ConfigurationSerializer.IsEmptyDiff(json))
                result[$"appsettings.{product}.json"] = json;
        }

        foreach (var (key, json) in prunedPeDiffs)
        {
            if (!ConfigurationSerializer.IsEmptyDiff(json))
                result[$"appsettings.{key}.json"] = json;
        }

        return result;
    }

    // ── Combination building ─────────────────────────────────────────

    private static List<(string? product, string? env)> BuildCombinations(
        WizardState state,
        Dictionary<string, string> envDiffs,
        Dictionary<string, string> productDiffs)
    {
        var combinations = new List<(string? product, string? env)>();

        if (state.ProductEnvironmentModels.Count > 0)
        {
            // Derive from PE model keys
            foreach (var key in state.ProductEnvironmentModels.Keys)
            {
                var parts = key.Split('.', 2);
                if (parts.Length == 2)
                    combinations.Add((parts[0], parts[1]));
            }
        }
        else if (envDiffs.Count > 0 && state.BaseModel.Products.Count > 0)
        {
            // No PE models: product x environment
            foreach (var product in state.BaseModel.Products)
            {
                foreach (var env in envDiffs.Keys)
                    combinations.Add((product.Alias, env));
            }
        }
        else if (envDiffs.Count > 0)
        {
            // Env-only: one combo per environment
            foreach (var env in envDiffs.Keys)
                combinations.Add((null, env));
        }
        else if (productDiffs.Count > 0)
        {
            // Product-only: one combo per product
            foreach (var product in productDiffs.Keys)
                combinations.Add((product, null));
        }

        return combinations;
    }

    // ── Hierarchy pruning ────────────────────────────────────────────

    /// <summary>
    /// Removes leaf properties from a parent JSON when they are present in every child group.
    /// Each group represents one runtime combination. A property is "covered" in a group if
    /// at least one child JSON in that group contains the property. If covered in EVERY group,
    /// the property is removed (it's never the effective value at runtime).
    /// </summary>
    internal static string PruneCoveredProperties(string parentJson, IReadOnlyList<IReadOnlyList<string>> combinationChildJsons)
    {
        if (combinationChildJsons.Count == 0)
            return parentJson;

        var parentDoc = JsonNode.Parse(parentJson);
        if (parentDoc?["RayMigrator"] is not JsonObject parentRay)
            return parentJson;

        // Parse all child groups
        var parsedGroups = combinationChildJsons
            .Select(group => (IReadOnlyList<JsonObject>)group
                .Select(json => JsonNode.Parse(json)?["RayMigrator"] as JsonObject)
                .Where(obj => obj != null)
                .Cast<JsonObject>()
                .ToList())
            .ToList();

        PruneCoveredFields(parentRay, parsedGroups);

        return parentDoc.ToJsonString(IndentedOptions);
    }

    private static void PruneCoveredFields(JsonObject parent, IReadOnlyList<IReadOnlyList<JsonObject>> childGroups)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in parent.ToList())
        {
            if (kvp.Value is JsonArray)
            {
                // Skip arrays — they have replacement semantics in .NET configuration
                continue;
            }

            if (kvp.Value is JsonObject parentNested)
            {
                // Narrow child groups to this section
                var nestedGroups = childGroups
                    .Select(group => (IReadOnlyList<JsonObject>)group
                        .Select(c => c[kvp.Key] as JsonObject)
                        .Where(n => n != null)
                        .Cast<JsonObject>()
                        .ToList())
                    .ToList();

                // Only recurse if every group has at least one child with this section
                if (nestedGroups.All(g => g.Count > 0))
                    PruneCoveredFields(parentNested, nestedGroups);

                if (parentNested.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            else
            {
                // Scalar leaf — covered if every group has at least one child with this key
                bool coveredInAllGroups = childGroups.All(group =>
                    group.Any(child => child.ContainsKey(kvp.Key)));

                if (coveredInAllGroups)
                    keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
            parent.Remove(key);
    }

    // ── PE redundancy removal ────────────────────────────────────────

    /// <summary>
    /// Removes fields from <paramref name="childJson"/> that already appear with identical values
    /// in <paramref name="parentJson"/>. This prevents product-environment files from repeating
    /// overrides that are already present in environment or product override files.
    /// </summary>
    internal static string RemoveRedundantOverrides(string childJson, string parentJson)
    {
        var childDoc = JsonNode.Parse(childJson);
        var parentDoc = JsonNode.Parse(parentJson);

        if (childDoc?["RayMigrator"] is JsonObject childRay &&
            parentDoc?["RayMigrator"] is JsonObject parentRay)
        {
            RemoveMatchingFields(childRay, parentRay);
        }

        return childDoc!.ToJsonString(IndentedOptions);
    }

    private static void RemoveMatchingFields(JsonObject target, JsonObject source)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in target.ToList())
        {
            if (source[kvp.Key] is not { } sourceVal)
                continue;

            if (kvp.Value is JsonObject targetObj && sourceVal is JsonObject sourceObj)
            {
                // Recurse into nested objects
                RemoveMatchingFields(targetObj, sourceObj);
                if (targetObj.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            else if (kvp.Value?.ToJsonString() == sourceVal.ToJsonString())
            {
                // Scalar or array with identical JSON representation
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
            target.Remove(key);
    }

    private static void AddEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
