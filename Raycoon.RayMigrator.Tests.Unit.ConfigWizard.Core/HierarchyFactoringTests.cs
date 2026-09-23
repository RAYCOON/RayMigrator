using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// The placement principle of #23 Part B: every value in the highest file in which it holds for every
/// combination that file applies to, no file repeating its effective parent, product elements owned by
/// their product.
/// </summary>
public class HierarchyFactoringTests
{
    private static JsonNode P(string json) => ConfigurationJsonMerger.Parse(json)!;

    private static HierarchyFactoring.Combination Combo(string product, string environment, string json)
        => new(product, environment, P(json));

    private static string Doc(string repositoryExtra, string shopTargetConnection, string defaultsExtra = "")
        => $$$"""
            {"RayMigrator":{
              "Repository":{"DatabaseType":"Sqlite","TableBaseName":""{{{repositoryExtra}}}},
              "ProductDefaults":{"MigrationFilesEncoding":"UTF-8"{{{defaultsExtra}}}},
              "Products":[{"Alias":"Shop","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"{{{shopTargetConnection}}}"}]}]}]
            }}
            """;

    private static string? Leaf(JsonNode? doc, string path)
    {
        var current = doc;
        foreach (var segment in path.Split('/'))
            current = int.TryParse(segment, out var index) ? current?[index] : current?[segment];
        return current?.GetValue<string>();
    }

    [Fact]
    public void Export_ValueIdenticalInAllCombinations_LandsInBaseOnly()
    {
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Doc("", "dev")),
            Combo("Shop", "Prod", Doc("", "prod")),
        });

        Leaf(files["appsettings.json"], "RayMigrator/Repository/DatabaseType").Should().Be("Sqlite");
        Leaf(files["appsettings.json"], "RayMigrator/ProductDefaults/MigrationFilesEncoding").Should().Be("UTF-8");
        files.Keys.Should().NotContain("appsettings.Dev.json").And.NotContain("appsettings.Prod.json").And.NotContain("appsettings.Shop.json");
        files.Keys.Should().Contain("appsettings.Shop.Dev.json").And.Contain("appsettings.Shop.Prod.json");
    }

    [Fact]
    public void Export_ValueIdenticalWithinOneEnvironment_LandsInEnvironmentFile()
    {
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Doc(""","ConnectionString":"repo-dev" """, "shop-dev")),
            Combo("Crm", "Dev", Doc(""","ConnectionString":"repo-dev" """, "shop-base")),
            Combo("Shop", "Prod", Doc(""","ConnectionString":"repo-prod" """, "shop-prod")),
            Combo("Crm", "Prod", Doc(""","ConnectionString":"repo-prod" """, "shop-base")),
        });

        Leaf(files["appsettings.Dev.json"], "RayMigrator/Repository/ConnectionString").Should().Be("repo-dev");
        Leaf(files["appsettings.Prod.json"], "RayMigrator/Repository/ConnectionString").Should().Be("repo-prod");
        Leaf(files["appsettings.json"], "RayMigrator/Repository/ConnectionString").Should().BeNull();
        files.Keys.Should().NotContain("appsettings.Crm.json");
    }

    [Fact]
    public void Export_ValueIdenticalAcrossAllEnvironmentsOfOneProduct_LandsInProductFile()
    {
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Doc("", "shop-dev", ""","MigrationErrorAction":"Rollback" """)),
            Combo("Shop", "Prod", Doc("", "shop-prod", ""","MigrationErrorAction":"Rollback" """)),
            Combo("Crm", "Dev", Doc("", "shop-base", ""","MigrationErrorAction":"Terminate" """)),
            Combo("Crm", "Prod", Doc("", "shop-base", ""","MigrationErrorAction":"Terminate" """)),
        });

        Leaf(files["appsettings.Shop.json"], "RayMigrator/ProductDefaults/MigrationErrorAction").Should().Be("Rollback");
        Leaf(files["appsettings.Crm.json"], "RayMigrator/ProductDefaults/MigrationErrorAction").Should().Be("Terminate");
        Leaf(files["appsettings.json"], "RayMigrator/ProductDefaults/MigrationErrorAction").Should().BeNull();
        Leaf(files["appsettings.Shop.Dev.json"], "RayMigrator/ProductDefaults/MigrationErrorAction").Should().BeNull();
    }

    [Fact]
    public void Export_ValueUniqueToOneCombination_LandsInProductEnvironmentFileOnly()
    {
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Doc("", "shop-dev")),
            Combo("Shop", "Prod", Doc("", "shop-prod")),
        });

        var pe = files["appsettings.Shop.Dev.json"];
        Leaf(pe, "RayMigrator/Products/0/TargetGroups/0/Targets/0/ConnectionString").Should().Be("shop-dev");
        pe["RayMigrator"]!["Products"]![0]!["TargetGroups"]![0]!.AsObject().Count.Should().Be(2, "alias and targets only");
        Leaf(files["appsettings.json"], "RayMigrator/Products/0/TargetGroups/0/Targets/0/ConnectionString").Should().BeNull();
        Leaf(files["appsettings.json"], "RayMigrator/Products/0/TargetGroups/0/DatabaseType").Should().Be("Sqlite");
    }

    [Fact]
    public void Export_EnvironmentAndProductFileConflict_ProductEnvironmentFileKeepsItsValue()
    {
        // X = 1 in every Dev combination (environment file), X = 2 in every Crm combination except Crm.Dev,
        // Crm.Dev needs 1: the product file must not carry 2, and Crm.Dev must merge back to 1.
        string WithX(string x, string cs) => Doc("", cs, $$$""","StopRollbackOnMissingRollbackFile":{{{x}}}""");
        var combinations = new[]
        {
            Combo("Shop", "Dev", WithX("true", "s-dev")),
            Combo("Crm", "Dev", WithX("true", "c-dev")),
            Combo("Shop", "Prod", WithX("true", "s-prod")),
            Combo("Crm", "Prod", WithX("false", "c-prod")),
        };

        var files = HierarchyFactoring.Factor(combinations);

        foreach (var combination in combinations)
        {
            var merged = HierarchyFactoring.MergeFor(files, combination.Product, combination.Environment);
            merged["RayMigrator"]!["ProductDefaults"]!["StopRollbackOnMissingRollbackFile"]!.GetValue<bool>()
                .Should().Be(combination.Effective["RayMigrator"]!["ProductDefaults"]!["StopRollbackOnMissingRollbackFile"]!.GetValue<bool>(),
                    $"{combination.Product}.{combination.Environment}");
        }
    }

    [Fact]
    public void Export_ProductElementValuesCommonToItsEnvironments_LandInBase_NotRepeatedPerCombination()
    {
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Doc("", "shop-dev")),
            Combo("Shop", "Prod", Doc("", "shop-prod")),
            Combo("Crm", "Dev", Doc("", "shop-inherited")),
        });

        // DatabaseType of Shop's target group holds in every Shop combination: base, and only there.
        Leaf(files["appsettings.json"], "RayMigrator/Products/0/TargetGroups/0/DatabaseType").Should().Be("Sqlite");
        Leaf(files["appsettings.Shop.Dev.json"], "RayMigrator/Products/0/TargetGroups/0/DatabaseType").Should().BeNull();
        // What the Crm run sees of Shop is inherited and is not factored into Crm's files.
        files.Keys.Should().NotContain("appsettings.Crm.Dev.json");
    }

    [Fact]
    public void Export_CliTools_DefinedOnceInBase_OverriddenByAlias()
    {
        string Tools(string mode) => $$$"""
            {"RayMigrator":{"CliTools":[{"Alias":"sqlcmd","ExecutablePath":"sqlcmd","InputMode":"{{{mode}}}"},{"Alias":"psql","ExecutablePath":"psql"}],
              "Products":[{"Alias":"Shop","TargetGroups":[]}]}}
            """;
        var files = HierarchyFactoring.Factor(new[] { Combo("Shop", "Dev", Tools("File")), Combo("Shop", "Prod", Tools("Stdin")) });

        var baseTools = files["appsettings.json"]["RayMigrator"]!["CliTools"]!.AsArray();
        baseTools.Should().HaveCount(2);
        Leaf(baseTools, "0/ExecutablePath").Should().Be("sqlcmd");
        Leaf(baseTools, "0/InputMode").Should().BeNull();
        // One product only: what is specific to Prod holds for every combination of Prod, so it lives in the environment file.
        files.Keys.Should().NotContain("appsettings.Shop.Prod.json");
        var prodTools = files["appsettings.Prod.json"]["RayMigrator"]!["CliTools"]!.AsArray();
        prodTools.Should().HaveCount(1);
        Leaf(prodTools, "0/Alias").Should().Be("sqlcmd");
        Leaf(prodTools, "0/InputMode").Should().Be("Stdin");
        prodTools[0]!.AsObject().Count.Should().Be(2);
    }

    [Fact]
    public void Export_NonAliasArray_ReplacedAsWhole_WhereItDiffers()
    {
        string Sinks(string sinks) => $$$"""{"RayMigrator":{"Serilog":{"MinimumLevel":{"Default":"Warning"},"WriteTo":{{{sinks}}}},"Products":[{"Alias":"Shop","TargetGroups":[]}]}}""";
        var files = HierarchyFactoring.Factor(new[]
        {
            Combo("Shop", "Dev", Sinks("""[{"Name":"Console"}]""")),
            Combo("Shop", "Prod", Sinks("""[{"Name":"Console"},{"Name":"File"}]""")),
        });

        Leaf(files["appsettings.json"], "RayMigrator/Serilog/MinimumLevel/Default").Should().Be("Warning");
        files["appsettings.json"]["RayMigrator"]!["Serilog"]!.AsObject().ContainsKey("WriteTo").Should().BeFalse();
        files["appsettings.Prod.json"]["RayMigrator"]!["Serilog"]!["WriteTo"]!.AsArray().Should().HaveCount(2);
        files["appsettings.Dev.json"]["RayMigrator"]!["Serilog"]!["WriteTo"]!.AsArray().Should().HaveCount(1);
        files.Keys.Should().NotContain("appsettings.Shop.Prod.json").And.NotContain("appsettings.Shop.Dev.json");
    }

    [Fact]
    public void Export_SingleCombination_EverythingInBase()
    {
        var files = HierarchyFactoring.Factor(new[] { Combo("Shop", "Dev", Doc(""","ConnectionString":"repo" """, "shop-dev")) });

        files.Keys.Should().Equal("appsettings.json");
        Leaf(files["appsettings.json"], "RayMigrator/Products/0/TargetGroups/0/Targets/0/ConnectionString").Should().Be("shop-dev");
    }

    [Fact]
    public void Export_NoCombinations_EmptyBase()
    {
        var files = HierarchyFactoring.Factor(Array.Empty<HierarchyFactoring.Combination>());

        files.Keys.Should().Equal("appsettings.json");
        files["appsettings.json"].AsObject().Count.Should().Be(0);
    }

    // ── Golden export cases and the two properties every case must satisfy ──────────────────────

    public static TheoryData<string> GoldenExportCases()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "ConfigExportCases")).OrderBy(d => d))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    private static (List<HierarchyFactoring.Combination> combinations, Dictionary<string, JsonNode> expected, string description) LoadCase(string caseName)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "ConfigExportCases", caseName);
        string description = P(File.ReadAllText(Path.Combine(dir, "case.json")))["description"]!.GetValue<string>();
        var effective = P(File.ReadAllText(Path.Combine(dir, "effective.json"))).AsObject();
        var combinations = effective.Select(kv =>
        {
            var parts = kv.Key.Split('.', 2);
            return new HierarchyFactoring.Combination(parts[0], parts[1], kv.Value!.DeepClone());
        }).ToList();
        var expected = Directory.GetFiles(Path.Combine(dir, "expected"), "*.json")
            .ToDictionary(f => Path.GetFileName(f), f => P(File.ReadAllText(f)), StringComparer.OrdinalIgnoreCase);
        return (combinations, expected, description);
    }

    [Theory]
    [MemberData(nameof(GoldenExportCases))]
    public void GoldenCases_Export_FilesEqualExpected(string caseName)
    {
        var (combinations, expected, description) = LoadCase(caseName);

        var files = HierarchyFactoring.Factor(combinations);

        files.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Should().Equal(expected.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase), description);
        foreach (var (name, document) in expected)
            JsonNode.DeepEquals(document, files[name]).Should().BeTrue($"{caseName}/{name} expected\n{document.ToJsonString()}\nbut got\n{files[name].ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(GoldenExportCases))]
    public void GoldenCases_RoundTrip_SplitThenMerge(string caseName)
    {
        var (combinations, _, description) = LoadCase(caseName);

        var files = HierarchyFactoring.Factor(combinations);

        foreach (var combination in combinations)
        {
            var merged = HierarchyFactoring.MergeFor(files, combination.Product, combination.Environment);
            AssertSameForRun(combination, merged, description);
        }
    }

    [Theory]
    [MemberData(nameof(GoldenExportCases))]
    public void Export_NoFileRepeatsItsEffectiveParent(string caseName)
    {
        var (combinations, _, _) = LoadCase(caseName);
        var files = HierarchyFactoring.Factor(combinations);

        foreach (var (name, document) in files)
        {
            if (name.Equals(ConfigurationFileChain.BaseFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            ConfigurationFileChain.TryClassify(name, out _, out var product, out var environment,
                combinations.Select(c => c.Product!).Where(p => p != null).ToList()).Should().BeTrue();

            // The parent of a file is the merge of the files below it in its own chain.
            var below = ConfigurationFileChain.FileNamesFor(product, environment).Select(f => f.FileName).TakeWhile(f => !f.Equals(name, StringComparison.OrdinalIgnoreCase));
            var parent = ConfigurationJsonMerger.MergeChain(below.Select(f => files.GetValueOrDefault(f)).ToList());

            ConfigurationJsonDiff.Diff(parent, document).Should().NotBeNull($"{caseName}/{name} adds nothing over its parent");
            AssertNoLeafEqualsParent(parent, document, $"{caseName}/{name}");
        }
    }

    [Theory]
    [MemberData(nameof(GoldenExportCases))]
    public void Export_MinimalAtTheMostSpecificLevel(string caseName)
    {
        var (combinations, _, _) = LoadCase(caseName);
        var files = HierarchyFactoring.Factor(combinations);

        // A leaf that every product-environment file of one product carries with the same value belongs higher.
        foreach (var product in combinations.Select(c => c.Product!).Distinct(AliasComparer.Instance))
        {
            var peFiles = combinations.Where(c => AliasComparer.AliasEquals(c.Product, product))
                .Select(c => files.GetValueOrDefault(ConfigurationFileChain.ProductEnvironmentFileName(product, c.Environment!)))
                .ToList();
            if (peFiles.Count < 2 || peFiles.Any(f => f == null))
                continue;

            ConfigurationJsonDiff.TryCommon(peFiles, out var common);
            AssertOnlyAliasSkeleton(common, $"{caseName}: product {product} repeats a value in every product-environment file");
        }
    }

    private static void AssertSameForRun(HierarchyFactoring.Combination combination, JsonNode merged, string description)
    {
        var expected = combination.Effective.DeepClone().AsObject();
        var actual = merged.AsObject();
        var expectedProducts = expected["RayMigrator"]!["Products"]!.AsArray();
        var actualProducts = actual["RayMigrator"]!["Products"]!.AsArray();
        var expectedOwn = ConfigurationJsonDiff.FindByAlias(expectedProducts, combination.Product!)!;
        var actualOwn = ConfigurationJsonDiff.FindByAlias(actualProducts, combination.Product!);

        actualOwn.Should().NotBeNull(description);
        JsonNode.DeepEquals(expectedOwn, actualOwn).Should().BeTrue($"{combination.Product}.{combination.Environment} own product element ({description}) expected\n{expectedOwn.ToJsonString()}\nbut got\n{actualOwn!.ToJsonString()}");

        expected["RayMigrator"]!.AsObject().Remove("Products");
        actual["RayMigrator"]!.AsObject().Remove("Products");
        JsonNode.DeepEquals(expected, actual).Should().BeTrue($"{combination.Product}.{combination.Environment} shared part ({description}) expected\n{expected.ToJsonString()}\nbut got\n{actual.ToJsonString()}");
    }

    private static void AssertNoLeafEqualsParent(JsonNode? parent, JsonNode? document, string where)
    {
        if (parent == null || document == null)
            return;

        if (parent is JsonObject po && document is JsonObject dobj)
        {
            foreach (var property in dobj)
            {
                if (!ConfigurationJsonDiff.TryGetProperty(po, property.Key, out var parentValue))
                    continue;
                if (property.Value is JsonObject or JsonArray)
                    AssertNoLeafEqualsParent(parentValue, property.Value, where + "/" + property.Key);
                else
                    JsonNode.DeepEquals(parentValue, property.Value).Should().BeFalse($"{where}/{property.Key} repeats its parent");
            }
        }
        else if (parent is JsonArray pa && document is JsonArray da && ConfigurationJsonMerger.IsAliasKeyedArray(pa) && ConfigurationJsonMerger.IsAliasKeyedArray(da))
        {
            foreach (var element in da)
            {
                var obj = (JsonObject)element!;
                var parentElement = ConfigurationJsonDiff.FindByAlias(pa, ConfigurationJsonMerger.TryGetAlias(obj)!);
                if (parentElement == null)
                    continue;
                obj.Count.Should().BeGreaterThan(1, $"{where}: an element that only repeats its alias adds nothing");
                foreach (var property in obj)
                {
                    if (string.Equals(property.Key, "Alias", StringComparison.OrdinalIgnoreCase))
                        continue;
                    AssertNoLeafEqualsParent(new JsonObject { [property.Key] = parentElement[property.Key]?.DeepClone() }, new JsonObject { [property.Key] = property.Value?.DeepClone() }, where);
                }
            }
        }
        else
        {
            JsonNode.DeepEquals(parent, document).Should().BeFalse($"{where} repeats its parent");
        }
    }

    private static void AssertOnlyAliasSkeleton(JsonNode? common, string because)
    {
        switch (common)
        {
            case null:
                return;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (string.Equals(property.Key, "Alias", StringComparison.OrdinalIgnoreCase))
                        continue;
                    (property.Value is JsonObject or JsonArray).Should().BeTrue(because + $" ({property.Key})");
                    AssertOnlyAliasSkeleton(property.Value, because);
                }
                return;
            case JsonArray array:
                foreach (var element in array)
                    AssertOnlyAliasSkeleton(element, because);
                return;
            default:
                throw new Xunit.Sdk.XunitException(because);
        }
    }
}
