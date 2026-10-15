using System.Text.Json.Nodes;
using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: the product scope of a run (ADR-026). A run sees the shared sections and its own product only; a fault in
/// another product, or in an element no run can select, must not reach the validation of this run.
/// </summary>
public class ConfigurationProductScopeTests
{
    private const string TwoProducts = """
        {"RayMigrator":{
          "Repository":{"DatabaseType":"Sqlite","ConnectionString":"Data Source=repo.sqlite","TableBaseName":""},
          "ProductDefaults":{"MigrationErrorAction":"Terminate","UseCliToolAlias":"sqlite"},
          "Products":[
            {"Alias":"Shop","MigrationErrorAction":"Terminate","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"shop"}]}]},
            {"Alias":"Crm","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main"}]}]}
          ],
          "CliTools":[{"Alias":"sqlite","ExecutablePath":"sqlite3","ArgumentTemplate":"{FilePath}","InputMode":"File"}],
          "Serilog":{"MinimumLevel":{"Default":"Warning"}},
          "Extra":{"Keep":true}}}
        """;

    private static JsonNode Parse(string json) => ConfigurationJsonMerger.Parse(json)!;

    private static JsonArray Products(JsonNode document) => document["RayMigrator"]!["Products"]!.AsArray();

    private static string? Alias(JsonNode? element) => ConfigurationJsonMerger.TryGetAlias(element!.AsObject());

    [Fact]
    public void Isolate_KeepsOnlyTheProduct_ComparedWithoutCase()
    {
        var document = Parse(TwoProducts);

        var scoped = ConfigurationProductScope.Isolate(document, "crm");

        var products = Products(scoped);
        products.Should().ContainSingle();
        Alias(products[0]).Should().Be("Crm", "the alias is compared like the merger compares it, without regard to case, and keeps its spelling");
        products[0]!["TargetGroups"]![0]!["Targets"]![0]!["Alias"]!.GetValue<string>().Should().Be("Main");
    }

    [Fact]
    public void Isolate_LeavesEveryOtherSectionAndTheInputAsTheyAre()
    {
        var document = Parse(TwoProducts);
        string before = document.ToJsonString();

        var scoped = ConfigurationProductScope.Isolate(document, "Shop");

        scoped.Should().NotBeSameAs(document);
        document.ToJsonString().Should().Be(before, "the input is not changed");
        var section = scoped["RayMigrator"]!.AsObject();
        section["Repository"]!.ToJsonString().Should().Be(document["RayMigrator"]!["Repository"]!.ToJsonString());
        section["ProductDefaults"]!["UseCliToolAlias"]!.GetValue<string>().Should().Be("sqlite");
        section["CliTools"]!.AsArray().Should().ContainSingle("every CLI tool stays, a migration file may name any of them");
        section["Serilog"]!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Warning");
        section["Extra"]!["Keep"]!.GetValue<bool>().Should().BeTrue("unknown keys are not the scope's business");
        section.Select(p => p.Key).Should().Equal(new[] { "Repository", "ProductDefaults", "Products", "CliTools", "Serilog", "Extra" }, "the order of the properties stays");
        Products(scoped).Should().ContainSingle().Which!["Alias"]!.GetValue<string>().Should().Be("Shop");
    }

    [Fact]
    public void Isolate_KeepsEveryElementThatRepeatsTheProductsAlias()
    {
        var document = Parse("""{"RayMigrator":{"Products":[{"Alias":"Shop","A":1},{"Alias":"Crm"},{"Alias":"shop","B":2}]}}""");

        var products = Products(ConfigurationProductScope.Isolate(document, "Shop"));

        products.Should().HaveCount(2, "a duplicate of the product's own alias is the product's own fault and the rule catalog reports it");
        products.Select(Alias).Should().Equal("Shop", "shop");
    }

    [Fact]
    public void Isolate_DropsElementsWithoutAStringAlias()
    {
        var document = Parse("""{"RayMigrator":{"Products":[{"MigrationErrorAction":"Terminate"},{"Alias":7},{"Alias":"Shop"},"text",null]}}""");

        var products = Products(ConfigurationProductScope.Isolate(document, "Shop"));

        products.Should().ContainSingle("no --product can select an element without an alias");
        Alias(products[0]).Should().Be("Shop");
    }

    [Fact]
    public void Isolate_UnknownProduct_LeavesAnEmptyProductsArray()
    {
        var document = Parse(TwoProducts);

        var scoped = ConfigurationProductScope.Isolate(document, "Erp");

        Products(scoped).Should().BeEmpty();
        scoped["RayMigrator"]!["Repository"].Should().NotBeNull();
    }

    [Fact]
    public void Isolate_FindsTheKeysWithoutRegardToCase_AndKeepsTheirSpelling()
    {
        var document = Parse("""{"raymigrator":{"products":[{"alias":"Shop"},{"alias":"Crm"}],"repository":{"DatabaseType":"Sqlite"}}}""");

        var scoped = ConfigurationProductScope.Isolate(document, "Crm");

        var section = scoped["raymigrator"]!.AsObject();
        section.ContainsKey("products").Should().BeTrue("the key as written stays");
        section["products"]!.AsArray().Should().ContainSingle().Which!["alias"]!.GetValue<string>().Should().Be("Crm");
        section["repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("Sqlite");
    }

    [Theory]
    [InlineData("""{"Other":{"Products":[{"Alias":"Shop"}]}}""")]
    [InlineData("""{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"}}}""")]
    [InlineData("""{"RayMigrator":{"Products":"Shop"}}""")]
    [InlineData("""{"RayMigrator":"text"}""")]
    [InlineData("""[1,2]""")]
    public void Isolate_WithoutAProductsArray_ReturnsAnUnchangedCopy(string json)
    {
        var document = Parse(json);

        var scoped = ConfigurationProductScope.Isolate(document, "Shop");

        scoped.Should().NotBeSameAs(document);
        scoped.ToJsonString().Should().Be(document.ToJsonString());
    }

    [Fact]
    public void Isolate_RefusesMissingArguments()
    {
        var document = Parse(TwoProducts);

        var noDocument = () => ConfigurationProductScope.Isolate(null!, "Shop");
        var noProduct = () => ConfigurationProductScope.Isolate(document, " ");

        noDocument.Should().Throw<ArgumentNullException>();
        noProduct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Isolate_OnAMergedChain_KeepsTheOverridesOfTheProduct()
    {
        var merged = ConfigurationJsonMerger.MergeChain(new List<JsonNode?>
        {
            Parse(TwoProducts),
            Parse("""{"RayMigrator":{"Products":[{"Alias":"CRM","TargetGroups":[{"Alias":"Backend","Targets":[{"Alias":"Main","ConnectionString":"crm-prod"}]}]}]}}""")
        });

        var products = Products(ConfigurationProductScope.Isolate(merged, "Crm"));

        products.Should().ContainSingle();
        products[0]!["TargetGroups"]![0]!["Targets"]![0]!["ConnectionString"]!.GetValue<string>().Should().Be("crm-prod");
    }

    [Fact]
    public void ProductAliases_ListsTheAliasesInOrder_SkippingElementsWithoutOne()
    {
        var document = Parse("""{"RayMigrator":{"Products":[{"Alias":"Shop"},{"Name":"nameless"},{"Alias":"Crm"},{"Alias":"shop"}]}}""");

        ConfigurationProductScope.ProductAliases(document).Should().Equal("Shop", "Crm", "shop");
    }

    [Theory]
    [InlineData("""{"RayMigrator":{"Products":[]}}""")]
    [InlineData("""{"RayMigrator":{}}""")]
    [InlineData("""{"RayMigrator":{"Products":{"Alias":"Shop"}}}""")]
    [InlineData("""{}""")]
    public void ProductAliases_WithoutProducts_IsEmpty(string json)
    {
        ConfigurationProductScope.ProductAliases(Parse(json)).Should().BeEmpty();
    }
}
