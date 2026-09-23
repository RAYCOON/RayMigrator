using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;
using Raycoon.RayMigrator.Shared.Configuration;

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
    /// Computes all export JSON strings keyed by file name (e.g. "appsettings.json", "appsettings.Development.json").
    /// Used by both the Overview display and the ZIP download to ensure identical output.
    /// The wizard's layers (base, environment, product, product-environment models) are merged per runtime
    /// combination with the shared <see cref="ConfigurationJsonMerger"/> into the configuration the engine must
    /// end up with, and <see cref="HierarchyFactoring"/> splits that into the smallest file family in which every
    /// value sits as high as it holds (#23 Part B).
    /// </summary>
    public static Dictionary<string, string> ComputeExportJsons(WizardState state)
    {
        string baseJson = ConfigurationSerializer.ToJson(state.BaseModel);

        var envDiffs = new Dictionary<string, string>();
        foreach (var (env, model) in state.EnvironmentModels)
            envDiffs[env] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        var productDiffs = new Dictionary<string, string>();
        foreach (var (product, model) in state.ProductModels)
            productDiffs[product] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        var peDiffs = new Dictionary<string, string>();
        foreach (var (key, model) in state.ProductEnvironmentModels)
            peDiffs[key] = ConfigurationSerializer.ToJson(model, state.BaseModel);

        var combinations = BuildCombinations(state, envDiffs, productDiffs);
        if (combinations.Count == 0)
            return new Dictionary<string, string> { ["appsettings.json"] = baseJson };

        var baseDocument = ConfigurationJsonMerger.Parse(baseJson);
        var effective = new List<HierarchyFactoring.Combination>();
        foreach (var (product, env) in combinations)
        {
            var chain = new List<JsonNode?> { baseDocument };
            if (env != null && envDiffs.TryGetValue(env, out var envJson))
                chain.Add(ConfigurationJsonMerger.Parse(envJson));
            if (product != null && productDiffs.TryGetValue(product, out var productJson))
                chain.Add(ConfigurationJsonMerger.Parse(productJson));
            if (product != null && env != null && peDiffs.TryGetValue($"{product}.{env}", out var peJson))
                chain.Add(ConfigurationJsonMerger.Parse(peJson));

            effective.Add(new HierarchyFactoring.Combination(product, env, ConfigurationJsonMerger.MergeChain(chain)));
        }

        var files = HierarchyFactoring.Factor(effective);

        var result = new Dictionary<string, string>();
        foreach (var (fileName, document) in files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            result[fileName] = document.ToJsonString(IndentedOptions);
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

    private static void AddEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
