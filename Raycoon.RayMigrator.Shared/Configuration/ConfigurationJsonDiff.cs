using System.Text.Json.Nodes;

namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// The two inverse operations of <see cref="ConfigurationJsonMerger.Merge"/> that a writer of the appsettings
/// hierarchy needs (#23 Part B): <see cref="Diff"/> yields the smallest document that, merged onto a parent,
/// reproduces a target; <see cref="TryCommon"/> yields the part several documents agree on. Both follow the
/// merge rules exactly: objects by key, alias-keyed arrays by alias, every other array as a unit.
/// </summary>
public static class ConfigurationJsonDiff
{
    /// <summary>
    /// Returns the smallest document <c>d</c> with <c>Merge(parent, d)</c> equal to <paramref name="target"/>,
    /// or null when the target equals the parent. Precondition: every key and every alias-keyed element of the
    /// parent exists in the target (a merge can add and override but never remove); keys present only in the
    /// parent are ignored, so callers that need the guarantee verify the round trip.
    /// </summary>
    public static JsonNode? Diff(JsonNode? parent, JsonNode? target)
    {
        if (target == null)
            return null;

        if (parent == null)
            return target.DeepClone();

        if (JsonNode.DeepEquals(parent, target))
            return null;

        if (parent is JsonObject parentObject && target is JsonObject targetObject)
        {
            var result = new JsonObject();
            foreach (var property in targetObject)
            {
                bool parentHasKey = TryGetProperty(parentObject, property.Key, out var parentValue);

                if (property.Value == null)
                {
                    if (!parentHasKey || parentValue != null)
                        result[property.Key] = null;
                    continue;
                }

                if (!parentHasKey || parentValue == null)
                {
                    result[property.Key] = property.Value.DeepClone();
                    continue;
                }

                var sub = Diff(parentValue, property.Value);
                if (sub != null)
                    result[property.Key] = sub;
            }

            return result.Count == 0 ? null : result;
        }

        if (parent is JsonArray parentArray && target is JsonArray targetArray &&
            ConfigurationJsonMerger.IsAliasKeyedArray(parentArray) && ConfigurationJsonMerger.IsAliasKeyedArray(targetArray))
        {
            var result = new JsonArray();
            foreach (var element in targetArray)
            {
                var targetElement = (JsonObject)element!;
                string alias = ConfigurationJsonMerger.TryGetAlias(targetElement)!;
                var parentElement = FindByAlias(parentArray, alias);

                if (parentElement == null)
                {
                    result.Add(targetElement.DeepClone());
                    continue;
                }

                if (Diff(parentElement, targetElement) is JsonObject sub)
                {
                    EnsureAlias(sub, targetElement);
                    result.Add(sub);
                }
            }

            return result.Count == 0 ? null : result;
        }

        return target.DeepClone();
    }

    /// <summary>
    /// Computes the part all documents agree on. Returns false when nothing is common. Scalars, non-alias arrays
    /// and JSON nulls are common only when equal in every document; objects contribute the keys that are common
    /// in every document; alias-keyed arrays contribute the elements whose alias exists in every document, each
    /// reduced to its common part, in the order of the first document.
    /// </summary>
    public static bool TryCommon(IReadOnlyList<JsonNode?> documents, out JsonNode? common)
    {
        common = null;
        if (documents.Count == 0)
            return false;

        var first = documents[0];
        if (documents.All(d => JsonNode.DeepEquals(first, d)))
        {
            common = first?.DeepClone();
            return true;
        }

        if (first is JsonObject firstObject && documents.All(d => d is JsonObject))
        {
            var result = new JsonObject();
            foreach (var property in firstObject)
            {
                var values = new List<JsonNode?>();
                bool inAll = true;
                foreach (var document in documents)
                {
                    if (!TryGetProperty((JsonObject)document!, property.Key, out var value))
                    {
                        inAll = false;
                        break;
                    }
                    values.Add(value);
                }

                if (inAll && TryCommon(values, out var valueCommon))
                    result[property.Key] = valueCommon;
            }

            if (result.Count == 0)
                return false;

            common = result;
            return true;
        }

        if (first is JsonArray firstArray && documents.All(d => d is JsonArray a && ConfigurationJsonMerger.IsAliasKeyedArray(a)))
        {
            var result = new JsonArray();
            foreach (var element in firstArray)
            {
                var firstElement = (JsonObject)element!;
                string alias = ConfigurationJsonMerger.TryGetAlias(firstElement)!;

                var elements = new List<JsonNode?>();
                bool inAll = true;
                foreach (var document in documents)
                {
                    var match = FindByAlias((JsonArray)document!, alias);
                    if (match == null)
                    {
                        inAll = false;
                        break;
                    }
                    elements.Add(match);
                }

                if (!inAll)
                    continue;

                if (TryCommon(elements, out var elementCommon) && elementCommon is JsonObject commonObject)
                {
                    EnsureAlias(commonObject, firstElement);
                    result.Add(commonObject);
                }
                else
                {
                    // The alias exists everywhere, nothing else does: the element itself is common.
                    result.Add(new JsonObject { [AliasKey(firstElement)] = alias });
                }
            }

            if (result.Count == 0)
                return false;

            common = result;
            return true;
        }

        return false;
    }

    /// <summary>Finds the element of an alias-keyed array by alias (case-insensitive), or null.</summary>
    public static JsonObject? FindByAlias(JsonArray array, string alias)
    {
        foreach (var element in array)
        {
            if (element is JsonObject obj && AliasComparer.AliasEquals(ConfigurationJsonMerger.TryGetAlias(obj), alias))
                return obj;
        }

        return null;
    }

    /// <summary>Looks a property up by exact key first, then case-insensitively (configuration keys are case-insensitive).</summary>
    public static bool TryGetProperty(JsonObject obj, string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(key, out value))
            return true;

        foreach (var property in obj)
        {
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string AliasKey(JsonObject element)
    {
        foreach (var property in element)
        {
            if (string.Equals(property.Key, ConfigurationJsonMerger.AliasPropertyName, StringComparison.OrdinalIgnoreCase))
                return property.Key;
        }

        return ConfigurationJsonMerger.AliasPropertyName;
    }

    /// <summary>Gives a delta of an alias-keyed element the alias of its source, first, when the delta lacks one.</summary>
    public static void EnsureAlias(JsonObject target, JsonObject source)
    {
        if (ConfigurationJsonMerger.TryGetAlias(target) != null)
            return;

        // Put the alias first so that generated files read naturally.
        var alias = ConfigurationJsonMerger.TryGetAlias(source);
        var rest = target.ToList();
        target.Clear();
        target[AliasKey(source)] = alias;
        foreach (var property in rest)
            target[property.Key] = property.Value?.DeepClone();
    }
}
