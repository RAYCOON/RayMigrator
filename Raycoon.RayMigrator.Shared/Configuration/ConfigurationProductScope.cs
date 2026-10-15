using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// Reduces a merged configuration to the scope of one product. A run loads, validates and masks the shared
/// sections (<c>Repository</c>, <c>DatabaseLogging</c>, <c>ProductDefaults</c>, <c>CliTools</c>, <c>Serilog</c>)
/// and the product it runs for; the other products of the merged document do not exist for it, so a fault in one
/// product never stops the runs of another. The engine's loader (<c>JsonOptionsSource</c>) applies it to the merged
/// document before the document is bound, so that the bound options and the section as written, which the rule
/// catalog pairs by index, describe the same product; a host that binds a document of its own, such as RayMigrator
/// Studio, is to apply it the same way.
/// </summary>
public static class ConfigurationProductScope
{
    /// <summary>The property of the <c>RayMigrator</c> section that lists the products.</summary>
    public const string ProductsPropertyName = "Products";

    /// <summary>
    /// Returns a deep copy of <paramref name="merged"/> whose <c>RayMigrator.Products</c> holds only the elements
    /// whose <c>Alias</c> equals <paramref name="productAlias"/> under <see cref="AliasComparer"/>, the comparison
    /// the merger uses. Every other element is left out, elements without an alias included (no run can select
    /// them); elements that repeat the product's alias are kept, so that the rule catalog reports the duplicate.
    /// Every other property of the document stays as it is, the keys keep their spelling. Without a
    /// <c>RayMigrator</c> object, without a <c>Products</c> property or with a <c>Products</c> that is not an array,
    /// the copy is unchanged. A product the document does not name yields an empty <c>Products</c> array; callers
    /// that want to name the known aliases ask <see cref="ProductAliases"/> first.
    /// </summary>
    /// <param name="merged">The merged document.</param>
    /// <param name="productAlias">The alias of the product the run is for.</param>
    /// <param name="resolveAlias">
    /// Maps an alias as written to the text that is compared, for example by resolving a <c>{ENV:...}</c>
    /// placeholder in it; the kept element stays as written. Null compares the aliases as written.
    /// </param>
    public static JsonNode Isolate(JsonNode merged, string productAlias, Func<string, string>? resolveAlias = null)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentException.ThrowIfNullOrWhiteSpace(productAlias);

        var copy = merged.DeepClone();
        if (!TryGetProducts(copy, out var section, out string productsKey, out var products))
            return copy;

        var kept = new JsonArray();
        foreach (var element in products)
        {
            if (element is JsonObject obj && AliasComparer.AliasEquals(AliasOf(obj, resolveAlias), productAlias))
                kept.Add(obj.DeepClone());
        }

        section[productsKey] = kept;
        return copy;
    }

    /// <summary>
    /// The aliases of the <c>RayMigrator.Products</c> elements in their order, resolved with
    /// <paramref name="resolveAlias"/> when one is given; elements without an alias are skipped. Empty when the
    /// document has no products.
    /// </summary>
    public static IReadOnlyList<string> ProductAliases(JsonNode merged, Func<string, string>? resolveAlias = null)
    {
        ArgumentNullException.ThrowIfNull(merged);

        if (!TryGetProducts(merged, out _, out _, out var products))
            return Array.Empty<string>();

        var aliases = new List<string>();
        foreach (var element in products)
        {
            if (element is JsonObject obj && AliasOf(obj, resolveAlias) is { } alias)
                aliases.Add(alias);
        }

        return aliases;
    }

    /// <summary>
    /// The alias of an element as the configuration binder reads it: the text of a string <c>Alias</c>, the digits
    /// of a numeric one (the binder reads every value as text); a blank, missing or other-typed alias is none.
    /// </summary>
    private static string? AliasOf(JsonObject element, Func<string, string>? resolveAlias)
    {
        foreach (var property in element)
        {
            if (!string.Equals(property.Key, ConfigurationJsonMerger.AliasPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value is not JsonValue value)
                return null;

            string? text = value.TryGetValue<string>(out var alias)
                ? alias
                : value.GetValueKind() == JsonValueKind.Number ? value.ToJsonString() : null;
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return resolveAlias is null ? text : resolveAlias(text);
        }

        return null;
    }

    /// <summary>
    /// Finds the <c>Products</c> array of the <c>RayMigrator</c> section; both keys are looked up like the
    /// configuration binder looks them up, without regard to case, and the key as written is returned.
    /// </summary>
    private static bool TryGetProducts(JsonNode root, out JsonObject section, out string productsKey, out JsonArray products)
    {
        section = null!;
        productsKey = ProductsPropertyName;
        products = null!;

        if (root is not JsonObject rootObject ||
            !ConfigurationJsonDiff.TryGetProperty(rootObject, InternalConstants.RayMigratorSectionName, out var sectionNode) ||
            sectionNode is not JsonObject sectionObject)
            return false;

        foreach (var property in sectionObject)
        {
            if (!string.Equals(property.Key, ProductsPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value is not JsonArray array)
                return false;

            section = sectionObject;
            productsKey = property.Key;
            products = array;
            return true;
        }

        return false;
    }
}
