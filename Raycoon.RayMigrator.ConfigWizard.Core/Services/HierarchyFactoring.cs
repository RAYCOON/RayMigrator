using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Splits the effective configuration of every product-environment combination into the appsettings hierarchy
/// following the placement principle of #23 Part B: every value goes into the highest file in which it holds
/// for every combination that file applies to (base, then environment, then product, then product-environment),
/// and no file repeats what its effective parent, merged with <see cref="ConfigurationJsonMerger"/>, already
/// provides. Elements of the top-level <c>Products</c> array belong to their own product: their values are
/// placed in the base file (common to all environments of that product) or in that product's
/// product-environment file; what another product's run sees of them is inherited and not factored.
/// </summary>
public static class HierarchyFactoring
{
    /// <summary>One runtime combination and the configuration the engine must end up with for it.</summary>
    public sealed record Combination(string? Product, string? Environment, JsonNode Effective);

    private const string RootKey = "RayMigrator";
    private const string ProductsKey = "Products";

    /// <summary>
    /// Returns the files of the hierarchy (file name to document); the base file is always present, every other
    /// file only when it has content.
    /// </summary>
    public static Dictionary<string, JsonNode> Factor(IReadOnlyList<Combination> combinations)
    {
        var files = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        if (combinations.Count == 0)
        {
            files[ConfigurationFileChain.BaseFileName] = new JsonObject();
            return files;
        }

        // 1. Separate the product-owned elements from everything else.
        var shared = new List<JsonNode?>();
        var elements = new List<Dictionary<string, JsonObject>>();
        var aliasOrder = new List<string>();
        bool productsAreAliasKeyed = true;

        foreach (var combination in combinations)
        {
            var (sharedPart, ownElements, aliasKeyed) = Split(combination.Effective);
            productsAreAliasKeyed &= aliasKeyed;
            shared.Add(sharedPart);
            elements.Add(ownElements);
            foreach (var alias in ownElements.Keys)
            {
                if (!aliasOrder.Any(a => AliasComparer.AliasEquals(a, alias)))
                    aliasOrder.Add(alias);
            }
        }

        if (!productsAreAliasKeyed)
        {
            // A Products array without aliases cannot be merged by alias; treat it like any other value.
            shared = combinations.Select(c => (JsonNode?)c.Effective).ToList();
            elements = combinations.Select(_ => new Dictionary<string, JsonObject>()).ToList();
            aliasOrder.Clear();
        }

        // 2. Shared part: four levels.
        ConfigurationJsonDiff.TryCommon(shared, out var baseDoc);
        var baseObject = baseDoc as JsonObject ?? new JsonObject();

        var environments = combinations.Select(c => c.Environment).Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(AliasComparer.Instance).ToList();
        var products = combinations.Select(c => c.Product).Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(AliasComparer.Instance).ToList();

        var envDocs = new Dictionary<string, JsonNode?>(AliasComparer.Instance);
        foreach (var environment in environments)
        {
            var ofEnvironment = Indexes(combinations, c => AliasComparer.AliasEquals(c.Environment, environment));
            ConfigurationJsonDiff.TryCommon(ofEnvironment.Select(i => shared[i]).ToList(), out var envCommon);
            envDocs[environment!] = ConfigurationJsonDiff.Diff(baseObject, envCommon);
        }

        var productDocs = new Dictionary<string, JsonNode?>(AliasComparer.Instance);
        foreach (var product in products)
        {
            var ofProduct = Indexes(combinations, c => AliasComparer.AliasEquals(c.Product, product));
            ConfigurationJsonDiff.TryCommon(ofProduct.Select(i => shared[i]).ToList(), out var productCommon);

            // The product file may omit what every environment file of this product already provides.
            JsonNode parent = baseObject;
            var environmentDocsOfProduct = ofProduct
                .Select(i => combinations[i].Environment)
                .Select(e => string.IsNullOrWhiteSpace(e) ? null : envDocs.GetValueOrDefault(e))
                .ToList();
            if (environmentDocsOfProduct.Count > 0 && environmentDocsOfProduct.All(d => d != null) &&
                ConfigurationJsonDiff.TryCommon(environmentDocsOfProduct, out var envCommonOfProduct) && envCommonOfProduct != null)
            {
                parent = ConfigurationJsonMerger.Merge(baseObject, envCommonOfProduct)!;
            }

            productDocs[product!] = ConfigurationJsonDiff.Diff(parent, productCommon);
        }

        var peDocs = new Dictionary<int, JsonNode?>();
        for (int i = 0; i < combinations.Count; i++)
        {
            var combination = combinations[i];
            if (string.IsNullOrWhiteSpace(combination.Product) || string.IsNullOrWhiteSpace(combination.Environment))
                continue;

            var parent = ConfigurationJsonMerger.MergeChain(new List<JsonNode?>
            {
                baseObject, envDocs.GetValueOrDefault(combination.Environment), productDocs.GetValueOrDefault(combination.Product)
            });
            peDocs[i] = ConfigurationJsonDiff.Diff(parent, shared[i]);
        }

        // 3. Product-owned elements: base (common to the product's combinations) and product-environment.
        var baseProducts = new JsonArray();
        var extraProducts = new Dictionary<int, JsonArray>();
        var envProducts = new Dictionary<string, JsonArray>(AliasComparer.Instance);
        var productProducts = new Dictionary<string, JsonArray>(AliasComparer.Instance);

        foreach (var alias in aliasOrder)
        {
            var owners = Indexes(combinations, c => AliasComparer.AliasEquals(c.Product, alias));
            if (owners.Count == 0)
                owners = Indexes(combinations, c => c.Product == null);
            if (owners.Count == 0)
                owners = Indexes(combinations, _ => true);

            var ownedElements = owners.Where(i => elements[i].ContainsKey(alias)).ToList();
            if (ownedElements.Count == 0)
                continue;

            var samples = ownedElements.Select(i => (JsonNode?)elements[i][alias]).ToList();
            JsonObject baseElement;
            if (ConfigurationJsonDiff.TryCommon(samples, out var elementCommon) && elementCommon is JsonObject commonObject)
                baseElement = commonObject;
            else
                baseElement = new JsonObject { [ConfigurationJsonMerger.AliasPropertyName] = alias };
            baseProducts.Add(baseElement);

            foreach (int i in ownedElements)
            {
                if (ConfigurationJsonDiff.Diff(baseElement, elements[i][alias]) is not JsonObject delta)
                    continue;
                ConfigurationJsonDiff.EnsureAlias(delta, elements[i][alias]);

                var combination = combinations[i];
                if (!string.IsNullOrWhiteSpace(combination.Product) && !string.IsNullOrWhiteSpace(combination.Environment))
                    Append(extraProducts, i, delta);
                else if (!string.IsNullOrWhiteSpace(combination.Product))
                    Append(productProducts, combination.Product!, delta);
                else if (!string.IsNullOrWhiteSpace(combination.Environment))
                    Append(envProducts, combination.Environment!, delta);
            }
        }

        // 4. Assemble the files.
        files[ConfigurationFileChain.BaseFileName] = WithProducts(baseObject, baseProducts.Count > 0 ? baseProducts : null)!;

        foreach (var environment in environments)
        {
            var document = WithProducts(envDocs[environment!], envProducts.GetValueOrDefault(environment!));
            if (document != null)
                files[ConfigurationFileChain.EnvironmentFileName(environment!)] = document;
        }

        foreach (var product in products)
        {
            var document = WithProducts(productDocs[product!], productProducts.GetValueOrDefault(product!));
            if (document != null)
                files[ConfigurationFileChain.ProductFileName(product!)] = document;
        }

        for (int i = 0; i < combinations.Count; i++)
        {
            var combination = combinations[i];
            if (string.IsNullOrWhiteSpace(combination.Product) || string.IsNullOrWhiteSpace(combination.Environment))
                continue;

            var document = WithProducts(peDocs.GetValueOrDefault(i), extraProducts.GetValueOrDefault(i));
            if (document != null)
                files[ConfigurationFileChain.ProductEnvironmentFileName(combination.Product!, combination.Environment!)] = document;
        }

        return files;
    }

    /// <summary>
    /// Returns the effective configuration a combination ends up with when the engine merges the given files,
    /// for verification: the shared part and the combination's own product element.
    /// </summary>
    public static JsonNode MergeFor(IReadOnlyDictionary<string, JsonNode> files, string? product, string? environment)
    {
        var chain = ConfigurationFileChain.FileNamesFor(product, environment)
            .Select(f => files.TryGetValue(f.FileName, out var document) ? document : null)
            .ToList();
        return ConfigurationJsonMerger.MergeChain(chain);
    }

    private static (JsonNode? Shared, Dictionary<string, JsonObject> Elements, bool AliasKeyed) Split(JsonNode effective)
    {
        var elements = new Dictionary<string, JsonObject>(AliasComparer.Instance);
        var clone = effective.DeepClone();

        if (clone is JsonObject root && ConfigurationJsonDiff.TryGetProperty(root, RootKey, out var rayNode) && rayNode is JsonObject ray &&
            ConfigurationJsonDiff.TryGetProperty(ray, ProductsKey, out var productsNode) && productsNode is JsonArray productsArray)
        {
            if (productsArray.Count > 0 && !ConfigurationJsonMerger.IsAliasKeyedArray(productsArray))
                return (clone, elements, false);

            foreach (var element in productsArray)
            {
                var obj = (JsonObject)element!;
                string alias = ConfigurationJsonMerger.TryGetAlias(obj)!;
                if (!elements.ContainsKey(alias))
                    elements[alias] = (JsonObject)obj.DeepClone();
            }

            string productsKey = ray.First(p => string.Equals(p.Key, ProductsKey, StringComparison.OrdinalIgnoreCase)).Key;
            ray.Remove(productsKey);
        }

        return (clone, elements, true);
    }

    private static List<int> Indexes(IReadOnlyList<Combination> combinations, Func<Combination, bool> predicate)
    {
        var result = new List<int>();
        for (int i = 0; i < combinations.Count; i++)
        {
            if (predicate(combinations[i]))
                result.Add(i);
        }
        return result;
    }

    private static void Append<TKey>(Dictionary<TKey, JsonArray> target, TKey key, JsonObject element) where TKey : notnull
    {
        if (!target.TryGetValue(key, out var array))
        {
            array = new JsonArray();
            target[key] = array;
        }
        array.Add(element);
    }

    /// <summary>Attaches a Products array to a document; returns null when neither the document nor the array has content.</summary>
    private static JsonNode? WithProducts(JsonNode? document, JsonArray? productsArray)
    {
        if (productsArray == null || productsArray.Count == 0)
            return document;

        var root = document as JsonObject ?? new JsonObject();
        if (!ConfigurationJsonDiff.TryGetProperty(root, RootKey, out var rayNode) || rayNode is not JsonObject ray)
        {
            ray = new JsonObject();
            root[RootKey] = ray;
        }

        ray[ProductsKey] = productsArray.DeepClone();
        return root;
    }
}
