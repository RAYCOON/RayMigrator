using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Merges multiple appsettings configuration JSON strings following the RayMigrator merge semantics of #23:
/// objects are recursively merged, alias-keyed arrays (any array whose every element is an object with a
/// string <c>Alias</c>, i.e. Products, TargetGroups, Targets, CliTools) are merged by alias with the base
/// order preserved, other arrays are completely replaced. The merge itself is
/// <see cref="ConfigurationJsonMerger"/> in <c>Raycoon.RayMigrator.Shared</c>, the same code the engine's
/// <c>JsonOptionsSource</c> runs, so the wizard shows what the engine does. IO-free: works with strings, not file paths.
/// </summary>
public static class ConfigFileMerger
{
    /// <summary>
    /// Merges a chain of JSON strings (ordered from lowest to highest priority)
    /// and returns the merged result as a ConfigurationModel.
    /// </summary>
    public static ConfigurationModel MergeChain(IReadOnlyList<string> jsonStrings)
    {
        if (jsonStrings.Count == 0)
            return new ConfigurationModel();

        var merged = MergeParsed(jsonStrings);
        if (merged == null)
            return new ConfigurationModel();

        string mergedJson = merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return ConfigurationSerializer.LoadFromJson(mergedJson);
    }

    /// <summary>
    /// Merges a chain of JSON strings and returns the merged JSON.
    /// </summary>
    public static string MergeChainToJson(IReadOnlyList<string> jsonStrings, bool indented = true)
    {
        if (jsonStrings.Count == 0)
            return "{}";

        var merged = MergeParsed(jsonStrings);
        if (merged == null)
            return "{}";

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    /// <summary>
    /// Recursively merges two JSON nodes with the shared RayMigrator semantics; neither argument is modified.
    /// </summary>
    public static JsonNode? MergeJson(JsonNode? baseNode, JsonNode? overrideNode)
        => ConfigurationJsonMerger.Merge(baseNode, overrideNode);

    /// <summary>
    /// Returns true if the array is non-empty and every element is a JsonObject with a string "Alias" property.
    /// </summary>
    internal static bool IsAliasKeyedArray(JsonArray arr) => ConfigurationJsonMerger.IsAliasKeyedArray(arr);

    /// <summary>
    /// Merges two alias-keyed arrays by matching items on "Alias": base items keep their order and are merged
    /// with their override, override items without a base match are appended, base items without an override
    /// match are preserved.
    /// </summary>
    internal static JsonArray MergeAliasKeyedArrays(JsonArray baseArr, JsonArray overrideArr)
        => ConfigurationJsonMerger.MergeAliasKeyedArrays(baseArr, overrideArr);

    /// <summary>Parses every string with the shared reader options (unparsable strings are skipped) and merges the rest.</summary>
    private static JsonNode? MergeParsed(IReadOnlyList<string> jsonStrings)
    {
        var documents = new List<JsonNode?>();
        foreach (var json in jsonStrings)
        {
            try
            {
                var node = ConfigurationJsonMerger.Parse(json);
                if (node != null)
                    documents.Add(node);
            }
            catch (JsonException)
            {
                // Skip strings that can't be parsed
            }
        }

        return documents.Count == 0 ? null : ConfigurationJsonMerger.MergeChain(documents);
    }
}
