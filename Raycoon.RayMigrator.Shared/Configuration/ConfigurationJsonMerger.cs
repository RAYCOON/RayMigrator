using System.Text.Json;
using System.Text.Json.Nodes;

namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// Merges the files of the appsettings hierarchy with the RayMigrator semantics decided in #23 (ADR-021):
/// objects merge recursively, scalars take the later value, arrays whose every element is an object with a
/// string <c>Alias</c> merge by alias (case-insensitive, at every nesting level, base order kept, new aliases
/// appended, omitted elements kept), and every other array is replaced as a whole by the later file.
/// The engine (<c>JsonOptionsSource</c>) and the Config Wizard (<c>ConfigFileMerger</c>) both call this class,
/// so a configuration previews in the wizard exactly as the engine runs it.
/// </summary>
public static class ConfigurationJsonMerger
{
    /// <summary>The property that identifies an element of an alias-keyed array.</summary>
    public const string AliasPropertyName = "Alias";

    /// <summary>
    /// Reader options shared by every application that parses a hierarchy file: comments and trailing
    /// commas are accepted, as the .NET JSON configuration provider has always accepted them.
    /// </summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Node options paired with <see cref="DocumentOptions"/>; property names keep their case.</summary>
    public static JsonNodeOptions NodeOptions { get; } = new() { PropertyNameCaseInsensitive = false };

    /// <summary>Parses one hierarchy file's text with the shared reader options. A JSON <c>null</c> document yields null.</summary>
    public static JsonNode? Parse(string json) => JsonNode.Parse(json, NodeOptions, DocumentOptions);

    /// <summary>Merges an ordered chain of documents (lowest priority first). An empty chain yields an empty object.</summary>
    public static JsonNode MergeChain(IReadOnlyList<JsonNode?> orderedDocuments)
    {
        JsonNode? merged = null;
        bool any = false;
        foreach (var document in orderedDocuments)
        {
            if (document == null)
                continue;
            merged = any ? Merge(merged, document) : document.DeepClone();
            any = true;
        }

        return merged ?? new JsonObject();
    }

    /// <summary>
    /// Merges <paramref name="overrideNode"/> onto <paramref name="baseNode"/> and returns a new node; neither
    /// argument is modified. A null override returns a copy of the base, a null base a copy of the override.
    /// </summary>
    public static JsonNode? Merge(JsonNode? baseNode, JsonNode? overrideNode)
    {
        if (overrideNode == null)
            return baseNode?.DeepClone();

        if (baseNode == null)
            return overrideNode.DeepClone();

        if (baseNode is JsonObject baseObject && overrideNode is JsonObject overrideObject)
            return MergeObjects(baseObject, overrideObject);

        if (baseNode is JsonArray baseArray && overrideNode is JsonArray overrideArray &&
            IsAliasKeyedArray(baseArray) && IsAliasKeyedArray(overrideArray))
            return MergeAliasKeyedArrays(baseArray, overrideArray);

        // Scalars, non-alias arrays and mismatched kinds: the later file wins.
        return overrideNode.DeepClone();
    }

    /// <summary>
    /// True when the array is non-empty and every element is an object with a string <c>Alias</c> property.
    /// Such arrays merge by alias; every other array is replaced as a whole.
    /// </summary>
    public static bool IsAliasKeyedArray(JsonArray array)
    {
        if (array.Count == 0)
            return false;

        foreach (var element in array)
        {
            if (element is not JsonObject obj || TryGetAlias(obj) == null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Merges two alias-keyed arrays: elements of the base keep their order and are merged with the override
    /// element of the same alias; override elements with a new alias are appended in the override's order.
    /// </summary>
    public static JsonArray MergeAliasKeyedArrays(JsonArray baseArray, JsonArray overrideArray)
    {
        var result = new JsonArray();
        var matchedOverrideIndexes = new HashSet<int>();

        foreach (var baseElement in baseArray)
        {
            var baseObject = (JsonObject)baseElement!;
            string baseAlias = TryGetAlias(baseObject)!;

            int matchIndex = -1;
            for (int i = 0; i < overrideArray.Count; i++)
            {
                if (matchedOverrideIndexes.Contains(i))
                    continue;
                if (AliasComparer.AliasEquals(TryGetAlias((JsonObject)overrideArray[i]!), baseAlias))
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex >= 0)
            {
                matchedOverrideIndexes.Add(matchIndex);
                result.Add(MergeObjects(baseObject, (JsonObject)overrideArray[matchIndex]!));
            }
            else
            {
                result.Add(baseObject.DeepClone());
            }
        }

        for (int i = 0; i < overrideArray.Count; i++)
        {
            if (!matchedOverrideIndexes.Contains(i))
                result.Add(overrideArray[i]!.DeepClone());
        }

        return result;
    }

    /// <summary>Returns the string value of the element's <c>Alias</c> property (property name compared case-insensitively), or null.</summary>
    public static string? TryGetAlias(JsonObject element)
    {
        foreach (var property in element)
        {
            if (!string.Equals(property.Key, AliasPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value is JsonValue value && value.TryGetValue<string>(out var alias))
                return alias;

            return null;
        }

        return null;
    }

    private static JsonObject MergeObjects(JsonObject baseObject, JsonObject overrideObject)
    {
        var result = (JsonObject)baseObject.DeepClone();

        foreach (var property in overrideObject)
        {
            string key = ResolveKey(result, property.Key);

            if (property.Value == null)
            {
                // An explicit null in the later file clears the value.
                result[key] = null;
                continue;
            }

            if (result.TryGetPropertyValue(key, out var existing) && existing != null)
            {
                var merged = Merge(existing, property.Value);
                result.Remove(key);
                result[key] = merged;
            }
            else
            {
                result[key] = property.Value.DeepClone();
            }
        }

        return result;
    }

    /// <summary>
    /// Configuration keys are case-insensitive for the .NET binder; a later file that spells a key differently
    /// must override the same key instead of adding a second one. The earlier spelling is kept.
    /// </summary>
    private static string ResolveKey(JsonObject target, string key)
    {
        if (target.ContainsKey(key))
            return key;

        foreach (var property in target)
        {
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                return property.Key;
        }

        return key;
    }
}
