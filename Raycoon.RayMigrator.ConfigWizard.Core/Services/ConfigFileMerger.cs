using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Merges multiple appsettings configuration JSON strings following RayMigrator merge semantics:
/// objects are recursively merged, alias-keyed arrays (Products, TargetGroups, Targets, CliTools)
/// are merged by matching Alias, other arrays are completely replaced.
/// IO-free: works with strings, not file paths.
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

        JsonNode? merged = null;
        foreach (var json in jsonStrings)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) continue;
                merged = merged == null ? node : MergeJson(merged, node);
            }
            catch (Exception)
            {
                // Skip strings that can't be parsed
            }
        }

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

        JsonNode? merged = null;
        foreach (var json in jsonStrings)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) continue;
                merged = merged == null ? node : MergeJson(merged, node);
            }
            catch (Exception)
            {
                // Skip strings that can't be parsed
            }
        }

        if (merged == null)
            return "{}";

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    /// <summary>
    /// Recursively merges two JSON nodes. Objects are merged recursively,
    /// alias-keyed arrays are merged by matching Alias, other arrays are
    /// completely replaced, and scalar values are overwritten by the override.
    /// </summary>
    public static JsonNode? MergeJson(JsonNode? baseNode, JsonNode? overrideNode)
    {
        if (overrideNode == null)
            return baseNode != null ? JsonNode.Parse(baseNode.ToJsonString()) : null;

        if (baseNode == null)
            return JsonNode.Parse(overrideNode.ToJsonString());

        // Both are objects: recursive merge
        if (baseNode is JsonObject baseObj && overrideNode is JsonObject overrideObj)
        {
            var result = JsonNode.Parse(baseObj.ToJsonString())!.AsObject();

            foreach (var kvp in overrideObj)
            {
                if (result.ContainsKey(kvp.Key))
                {
                    var baseValue = result[kvp.Key];
                    var overrideValue = kvp.Value;

                    if (overrideValue is JsonArray overrideArr)
                    {
                        // Alias-keyed arrays: merge items by Alias (Products, TargetGroups, Targets, CliTools)
                        if (baseValue is JsonArray baseArr && IsAliasKeyedArray(baseArr) && IsAliasKeyedArray(overrideArr))
                        {
                            result.Remove(kvp.Key);
                            result[kvp.Key] = MergeAliasKeyedArrays(baseArr, overrideArr);
                        }
                        else
                        {
                            // Non-keyed arrays: complete replacement
                            result.Remove(kvp.Key);
                            result[kvp.Key] = JsonNode.Parse(overrideValue.ToJsonString());
                        }
                    }
                    // Objects are recursively merged
                    else if (baseValue is JsonObject && overrideValue is JsonObject)
                    {
                        result.Remove(kvp.Key);
                        result[kvp.Key] = MergeJson(baseValue, overrideValue);
                    }
                    // Scalars are replaced
                    else
                    {
                        result.Remove(kvp.Key);
                        result[kvp.Key] = overrideValue != null
                            ? JsonNode.Parse(overrideValue.ToJsonString())
                            : null;
                    }
                }
                else
                {
                    // New key from override
                    result[kvp.Key] = kvp.Value != null
                        ? JsonNode.Parse(kvp.Value.ToJsonString())
                        : null;
                }
            }

            return result;
        }

        // Override is not an object or types differ: replace entirely
        return JsonNode.Parse(overrideNode.ToJsonString());
    }

    /// <summary>
    /// Returns true if every element in the array is a JsonObject with a string "Alias" property.
    /// Used to identify arrays that should be merged by alias (Products, TargetGroups, Targets, CliTools).
    /// </summary>
    internal static bool IsAliasKeyedArray(JsonArray arr)
    {
        if (arr.Count == 0) return false;

        foreach (var item in arr)
        {
            if (item is not JsonObject obj) return false;
            if (obj["Alias"] is not JsonValue aliasVal) return false;
            if (aliasVal.GetValueKind() != JsonValueKind.String) return false;
        }

        return true;
    }

    /// <summary>
    /// Merges two alias-keyed arrays by matching items on "Alias" and recursively merging them.
    /// Override items without a matching base item are appended.
    /// Base items without a matching override item are preserved.
    /// </summary>
    internal static JsonArray MergeAliasKeyedArrays(JsonArray baseArr, JsonArray overrideArr)
    {
        // Build a lookup of base items by alias (preserve original order)
        var baseItems = new List<(string alias, JsonObject obj)>();
        foreach (var item in baseArr)
        {
            if (item is JsonObject obj && obj["Alias"]?.GetValue<string>() is { } alias)
                baseItems.Add((alias, JsonNode.Parse(obj.ToJsonString())!.AsObject()));
        }

        var matchedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new JsonArray();

        // Process override items: merge with matching base items, or add new
        foreach (var overrideItem in overrideArr)
        {
            if (overrideItem is not JsonObject overrideObj)
            {
                // Non-object items: add as-is
                result.Add(overrideItem != null ? JsonNode.Parse(overrideItem.ToJsonString()) : null);
                continue;
            }

            var overrideAlias = overrideObj["Alias"]?.GetValue<string>();
            if (overrideAlias == null)
            {
                result.Add(JsonNode.Parse(overrideObj.ToJsonString()));
                continue;
            }

            var baseMatch = baseItems.FirstOrDefault(b =>
                string.Equals(b.alias, overrideAlias, StringComparison.OrdinalIgnoreCase));

            if (baseMatch.obj != null)
            {
                // Matched by alias: recursively merge base + override
                matchedAliases.Add(overrideAlias);
                var merged = MergeJson(baseMatch.obj, overrideObj);
                result.Add(merged);
            }
            else
            {
                // New item from override
                result.Add(JsonNode.Parse(overrideObj.ToJsonString()));
            }
        }

        // Append base items that had no match in override (preserve base-only items)
        foreach (var (alias, obj) in baseItems)
        {
            if (!matchedAliases.Contains(alias))
                result.Add(JsonNode.Parse(obj.ToJsonString()));
        }

        return result;
    }
}
