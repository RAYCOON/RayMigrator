using System.Text.Json.Nodes;
using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: the RayMigrator merge semantics of the appsettings hierarchy (#23, ADR-021).
/// A wrong merge attaches an override to the wrong product and migrates the wrong database.
/// </summary>
public class ConfigurationJsonMergerTests
{
    private const string Base = """
        {"RayMigrator":{
          "Repository":{"DatabaseType":"Sqlite","SchemaName":"ray","TableBaseName":""},
          "Products":[
            {"Alias":"Shop","MigrationErrorAction":"Terminate","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"shop"}]}]},
            {"Alias":"Crm","MigrationErrorAction":"Terminate","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"crm"}]}]}
          ]}}
        """;

    private static JsonNode Merge(string baseJson, params string[] overrides)
    {
        var chain = new List<JsonNode?> { ConfigurationJsonMerger.Parse(baseJson) };
        chain.AddRange(overrides.Select(ConfigurationJsonMerger.Parse));
        return ConfigurationJsonMerger.MergeChain(chain);
    }

    private static JsonArray Products(JsonNode merged) => merged["RayMigrator"]!["Products"]!.AsArray();
    private static string? S(JsonNode? node, string path)
    {
        var current = node;
        foreach (var segment in path.Split('/'))
            current = int.TryParse(segment, out var index) ? current?[index] : current?[segment];
        return current?.GetValue<string>();
    }

    [Fact]
    public void Products_SameAlias_MergesProperties_OtherProductsUntouched()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationErrorAction":"Rollback"}]}}""");

        var products = Products(merged);
        products.Should().HaveCount(2);
        S(products[0], "Alias").Should().Be("Shop");
        S(products[0], "MigrationErrorAction").Should().Be("Terminate");
        S(products[1], "Alias").Should().Be("Crm");
        S(products[1], "MigrationErrorAction").Should().Be("Rollback");
        S(products[1], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("crm");
    }

    [Fact]
    public void Products_ReversedOrder_MatchByAlias_NotByPosition()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationErrorAction":"Rollback"},{"Alias":"Shop","MigrationErrorAction":"Terminate"}]}}""");

        var products = Products(merged);
        S(products[0], "Alias").Should().Be("Shop");
        S(products[0], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("shop");
        S(products[1], "Alias").Should().Be("Crm");
        S(products[1], "MigrationErrorAction").Should().Be("Rollback");
        S(products[1], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("crm");
    }

    [Fact]
    public void AliasMatch_IsCaseInsensitive_LaterSpellingWins()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"crm","MigrationErrorAction":"Rollback"}]}}""");

        var products = Products(merged);
        products.Should().HaveCount(2);
        S(products[1], "Alias").Should().Be("crm");
        S(products[1], "MigrationErrorAction").Should().Be("Rollback");
    }

    [Fact]
    public void NewAlias_IsAppended_AfterBaseElements_InOverrideOrder()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"D","TargetGroups":[]},{"Alias":"C","TargetGroups":[]}]}}""");

        Products(merged).Select(p => S(p, "Alias")).Should().Equal("Shop", "Crm", "D", "C");
    }

    [Fact]
    public void BaseOrder_IsPreserved_WhenOverrideReorders()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"Crm"},{"Alias":"Shop"}]}}""");

        Products(merged).Select(p => S(p, "Alias")).Should().Equal("Shop", "Crm");
    }

    [Fact]
    public void OmittedAlias_IsKept_NeverRemovedByOmission()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Products":[{"Alias":"Crm"}]}}""");

        var products = Products(merged);
        products.Should().HaveCount(2);
        S(products[0], "Alias").Should().Be("Shop");
        S(products[0], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("shop");
    }

    [Fact]
    public void NestedAliasArrays_MergeByAlias_AtEveryLevel()
    {
        const string baseJson = """
            {"RayMigrator":{"Products":[
              {"Alias":"P1","TargetGroups":[{"Alias":"G1","Targets":[{"Alias":"T1","ConnectionString":"p1g1t1"}]}]},
              {"Alias":"P2","TargetGroups":[
                {"Alias":"G1","Targets":[{"Alias":"T1","ConnectionString":"p2g1t1"}]},
                {"Alias":"G2","Targets":[{"Alias":"T1","ConnectionString":"p2g2t1"},{"Alias":"T2","ConnectionString":"p2g2t2"}]}]}]}}
            """;
        var merged = Merge(baseJson, """{"RayMigrator":{"Products":[{"Alias":"P2","TargetGroups":[{"Alias":"G2","Targets":[{"Alias":"T2","ConnectionString":"changed"}]}]}]}}""");

        var products = Products(merged);
        S(products[0], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("p1g1t1");
        S(products[1], "TargetGroups/0/Targets/0/ConnectionString").Should().Be("p2g1t1");
        S(products[1], "TargetGroups/1/Targets/0/ConnectionString").Should().Be("p2g2t1");
        S(products[1], "TargetGroups/1/Targets/1/ConnectionString").Should().Be("changed");
        products[1]!["TargetGroups"]!.AsArray().Should().HaveCount(2);
        products[1]!["TargetGroups"]![1]!["Targets"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public void CliTools_MergeByAlias()
    {
        var merged = Merge(
            """{"RayMigrator":{"CliTools":[{"Alias":"sqlcmd","ExecutablePath":"sqlcmd","InputMode":"File","SuccessExitCodes":"0"}]}}""",
            """{"RayMigrator":{"CliTools":[{"Alias":"psql","ExecutablePath":"psql","InputMode":"File"},{"Alias":"sqlcmd","SuccessExitCodes":"0,1"}]}}""");

        var tools = merged["RayMigrator"]!["CliTools"]!.AsArray();
        tools.Select(t => S(t, "Alias")).Should().Equal("sqlcmd", "psql");
        S(tools[0], "ExecutablePath").Should().Be("sqlcmd");
        S(tools[0], "SuccessExitCodes").Should().Be("0,1");
    }

    [Fact]
    public void NonAliasArray_IsReplacedAsWhole()
    {
        var merged = Merge(
            """{"RayMigrator":{"Serilog":{"Using":["A","B"],"WriteTo":[{"Name":"Console"},{"Name":"File"}]}}}""",
            """{"RayMigrator":{"Serilog":{"Using":["C"],"WriteTo":[{"Name":"Console","Args":{"x":"1"}}]}}}""");

        var serilog = merged["RayMigrator"]!["Serilog"]!;
        serilog["Using"]!.AsArray().Select(u => u!.GetValue<string>()).Should().Equal("C");
        serilog["WriteTo"]!.AsArray().Should().HaveCount(1);
        S(serilog["WriteTo"], "0/Args/x").Should().Be("1");
    }

    [Fact]
    public void ArrayWithElementWithoutAlias_IsReplacedAsWhole()
    {
        var merged = Merge(
            """{"RayMigrator":{"Products":[{"Alias":"Shop","X":"1"},{"Alias":"Crm","X":"2"}]}}""",
            """{"RayMigrator":{"Products":[{"Alias":"Crm","X":"3"},{"NoAlias":true}]}}""");

        var products = Products(merged);
        products.Should().HaveCount(2);
        S(products[0], "Alias").Should().Be("Crm");
        S(products[0], "X").Should().Be("3");
        products[1]!["NoAlias"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void EmptyArray_IsNotAliasKeyed()
    {
        ConfigurationJsonMerger.IsAliasKeyedArray(new JsonArray()).Should().BeFalse();
        ConfigurationJsonMerger.IsAliasKeyedArray(new JsonArray(new JsonObject { ["Alias"] = "A" })).Should().BeTrue();
        ConfigurationJsonMerger.IsAliasKeyedArray(new JsonArray(new JsonObject { ["alias"] = "A" })).Should().BeTrue();
        ConfigurationJsonMerger.IsAliasKeyedArray(new JsonArray(new JsonObject { ["Alias"] = 1 })).Should().BeFalse();
    }

    [Fact]
    public void ExplicitNull_InLaterFile_SetsNull()
    {
        var merged = Merge(
            """{"RayMigrator":{"ProductDefaults":{"UseCliToolAlias":"sqlcmd","MigrationFilesEncoding":"UTF-8"}}}""",
            """{"RayMigrator":{"ProductDefaults":{"UseCliToolAlias":null}}}""");

        var defaults = merged["RayMigrator"]!["ProductDefaults"]!.AsObject();
        defaults.ContainsKey("UseCliToolAlias").Should().BeTrue();
        defaults["UseCliToolAlias"].Should().BeNull();
        S(defaults, "MigrationFilesEncoding").Should().Be("UTF-8");
    }

    [Fact]
    public void ScalarsAndObjects_FollowLaterWins_AndRecursiveMerge()
    {
        var merged = Merge(Base, """{"RayMigrator":{"Repository":{"SchemaName":"other"}}}""");

        var repository = merged["RayMigrator"]!["Repository"]!;
        S(repository, "SchemaName").Should().Be("other");
        S(repository, "DatabaseType").Should().Be("Sqlite");
        S(repository, "TableBaseName").Should().Be("");
    }

    [Fact]
    public void KeyCasing_LaterFileWithOtherSpelling_OverridesSameKey()
    {
        var merged = Merge(
            """{"RayMigrator":{"Repository":{"SchemaName":"ray"}}}""",
            """{"RayMigrator":{"repository":{"schemaname":"other"}}}""");

        var repository = merged["RayMigrator"]!["Repository"]!.AsObject();
        repository.Count.Should().Be(1);
        S(repository, "SchemaName").Should().Be("other");
    }

    [Fact]
    public void FourFileChain_Precedence_Base_Env_Product_ProductEnv()
    {
        var merged = Merge(
            """{"RayMigrator":{"Products":[{"Alias":"Shop","A":"base","B":"base","C":"base","D":"base"}]}}""",
            """{"RayMigrator":{"Products":[{"Alias":"Shop","B":"env","C":"env","D":"env"}]}}""",
            """{"RayMigrator":{"Products":[{"Alias":"Shop","C":"product","D":"product"}]}}""",
            """{"RayMigrator":{"Products":[{"Alias":"Shop","D":"pe"}]}}""");

        var shop = Products(merged)[0];
        S(shop, "A").Should().Be("base");
        S(shop, "B").Should().Be("env");
        S(shop, "C").Should().Be("product");
        S(shop, "D").Should().Be("pe");
    }

    [Fact]
    public void EmptyOrMissingRayMigratorNode_InOneFile_IsIgnored()
    {
        var withEmpty = Merge(Base, "{}", """{"Other":{"X":1}}""");

        Products(withEmpty).Should().HaveCount(2);
        withEmpty["Other"]!["X"]!.GetValue<int>().Should().Be(1);
        ConfigurationJsonMerger.MergeChain(new List<JsonNode?>()).Should().BeOfType<JsonObject>().Which.Count.Should().Be(0);
        ConfigurationJsonMerger.MergeChain(new List<JsonNode?> { null, ConfigurationJsonMerger.Parse("{}") }).Should().BeOfType<JsonObject>();
    }

    [Fact]
    public void Merge_DoesNotModifyItsArguments()
    {
        var baseNode = ConfigurationJsonMerger.Parse(Base)!;
        var overrideNode = ConfigurationJsonMerger.Parse("""{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationErrorAction":"Rollback"}]}}""")!;
        string baseBefore = baseNode.ToJsonString();
        string overrideBefore = overrideNode.ToJsonString();

        ConfigurationJsonMerger.Merge(baseNode, overrideNode);

        baseNode.ToJsonString().Should().Be(baseBefore);
        overrideNode.ToJsonString().Should().Be(overrideBefore);
    }

    [Fact]
    public void Parse_AcceptsCommentsAndTrailingCommas()
    {
        var node = ConfigurationJsonMerger.Parse("""
            {
              // a comment
              "RayMigrator": { "Repository": { "DatabaseType": "Sqlite", }, },
            }
            """);

        S(node, "RayMigrator/Repository/DatabaseType").Should().Be("Sqlite");
    }
}
