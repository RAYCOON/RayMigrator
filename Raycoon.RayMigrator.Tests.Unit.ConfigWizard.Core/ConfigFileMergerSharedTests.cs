using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// The wizard reads the appsettings hierarchy with the same code as the engine (#23):
/// the shared merger, the shared file chain and the shared reader options.
/// </summary>
public class ConfigFileMergerSharedTests
{
    public static TheoryData<string> GoldenCases()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases")).OrderBy(d => d))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    private static (List<string> chain, JsonNode meta, JsonNode expected) LoadCase(string caseName)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases", caseName);
        var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "case.json")))!;
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "expected.json")))!;
        var chain = ConfigurationFileChain
            .FileNamesFor(meta["product"]!.GetValue<string>(), meta["environment"]!.GetValue<string>())
            .Select(f => Path.Combine(dir, f.FileName))
            .Where(File.Exists)
            .Select(File.ReadAllText)
            .ToList();
        return (chain, meta, expected);
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void GoldenCases_Wizard_MergeChainToJsonEqualsExpected(string caseName)
    {
        var (chain, meta, expected) = LoadCase(caseName);

        var actual = JsonNode.Parse(ConfigFileMerger.MergeChainToJson(chain))!;

        JsonNode.DeepEquals(expected, actual).Should().BeTrue(
            $"case '{caseName}' ({meta["description"]}) expected\n{expected.ToJsonString()}\nbut got\n{actual.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void ConfigFileMerger_ProducesSameJsonAs_SharedMerger(string caseName)
    {
        var (chain, _, _) = LoadCase(caseName);

        string viaWizard = ConfigFileMerger.MergeChainToJson(chain, indented: false);
        string viaShared = ConfigurationJsonMerger.MergeChain(chain.Select(ConfigurationJsonMerger.Parse).ToList()).ToJsonString();

        viaWizard.Should().Be(viaShared);
    }

    [Fact]
    public void MergeChain_LoadedModel_ReflectsAliasMerge()
    {
        var (chain, _, _) = LoadCase("reversed-order");

        var model = ConfigFileMerger.MergeChain(chain);

        model.Products.Select(p => p.Alias).Should().Equal("Shop", "Crm");
        model.Products[1].MigrationErrorAction.Value.Should().Be("Rollback");
        model.Products[1].TargetGroups[0].Targets[0].ConnectionString.Should().Be("Data Source=crm.sqlite");
        model.Products[0].TargetGroups[0].Targets[0].ConnectionString.Should().Be("Data Source=shop.sqlite");
    }

    [Fact]
    public void ReaderOptions_CommentsAndTrailingCommas_AreAcceptedByImportAndMerge()
    {
        const string commented = """
            {
              // comment
              "RayMigrator": { "Repository": { "DatabaseType": "PostgreSQL", }, "Products": [ { "Alias": "Shop", "TargetGroups": [], }, ], },
            }
            """;

        var model = ConfigurationSerializer.LoadFromJson(commented);
        model.Repository.DatabaseType.Should().Be("PostgreSQL");
        model.Products.Should().ContainSingle(p => p.Alias == "Shop");

        var merged = ConfigFileMerger.MergeChain(new List<string> { commented, """{"RayMigrator":{"Repository":{"SchemaName":"s"}}}""" });
        merged.Repository.DatabaseType.Should().Be("PostgreSQL");
        merged.Repository.SchemaName.Should().Be("s");
    }

    [Fact]
    public void ConfigurationFileParser_ClassifiesThroughTheSharedChain()
    {
        foreach (var (role, fileName) in ConfigurationFileChain.FileNamesFor("Shop", "Production"))
        {
            var (classified, product, environment) = ConfigurationFileParser.ClassifyFileName(fileName, new[] { "Shop" });
            ConfigurationFileChain.TryClassify(fileName, out var expectedRole, out var expectedProduct, out var expectedEnvironment, new[] { "Shop" }).Should().BeTrue();
            classified.Should().Be(expectedRole).And.Be(role);
            product.Should().Be(expectedProduct);
            environment.Should().Be(expectedEnvironment);
        }

        ConfigurationFileParser.ClassifyFileName("notes.txt").role.Should().Be(ConfigFileRole.Base);
    }

    [Fact]
    public void Parse_ProductFile_IsRecognizedWhenTheBaseFileDefinesTheProduct()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"},"Products":[{"Alias":"Shop","TargetGroups":[]}]}}""",
            ["appsettings.Shop.json"] = """{"RayMigrator":{"Products":[{"Alias":"Shop","MigrationErrorAction":"Rollback"}]}}""",
            ["appsettings.Production.json"] = """{"RayMigrator":{"Repository":{"SchemaName":"prod"}}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        state.ProductModels.Keys.Should().Equal("Shop");
        state.EnvironmentModels.Keys.Should().Equal("Production");
        state.ProductModels["Shop"].FileRole.Should().Be(ConfigFileRole.Product);
    }
}
