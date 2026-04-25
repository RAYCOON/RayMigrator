
namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class ConfigFileMergerTests
{
    [Fact]
    public void MergeChain_EmptyList_ReturnsEmptyModel()
    {
        var result = ConfigFileMerger.MergeChain(new List<string>());
        result.Products.Should().BeEmpty();
    }

    [Fact]
    public void MergeChain_SingleFile_ParsesCorrectly()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"PostgreSQL"}}}"""
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.Repository.DatabaseType.Should().Be("PostgreSQL");
    }

    [Fact]
    public void MergeChain_OverrideScalar_ReplacesValue()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer","SchemaName":"old"}}}""",
            """{"RayMigrator":{"Repository":{"SchemaName":"new"}}}"""
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.Repository.DatabaseType.Should().Be("SqlServer");
        result.Repository.SchemaName.Should().Be("new");
    }

    [Fact]
    public void MergeChain_AliasKeyedArrays_MergedByAlias_DifferentAliasesPreserveBoth()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Products":[{"Alias":"OldApp","MigrationFilesRootDirectory":"./old","TargetGroups":[{"Alias":"TG","DatabaseType":"SqlServer","Targets":[{"Alias":"T1","ConnectionString":"old"}]}]}]}}""",
            """{"RayMigrator":{"Products":[{"Alias":"NewApp","MigrationFilesRootDirectory":"./new","TargetGroups":[{"Alias":"TG","DatabaseType":"PostgreSQL","Targets":[{"Alias":"T1","ConnectionString":"new"}]}]}]}}"""
        };

        var result = ConfigFileMerger.MergeChain(files);
        // Different aliases: both preserved (override first, then base-only)
        result.Products.Should().HaveCount(2);
        result.Products[0].Alias.Should().Be("NewApp");
        result.Products[1].Alias.Should().Be("OldApp");
    }

    [Fact]
    public void MergeChainToJson_ReturnsValidJson()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}"""
        };

        var json = ConfigFileMerger.MergeChainToJson(files);
        json.Should().Contain("SqlServer");
    }

    [Fact]
    public void MergeJson_BothNull_ReturnsNull()
    {
        ConfigFileMerger.MergeJson(null, null).Should().BeNull();
    }

    [Fact]
    public void MergeJson_BaseNull_ReturnsOverride()
    {
        var json = """{"key":"value"}""";
        var result = ConfigFileMerger.MergeJson(null, System.Text.Json.Nodes.JsonNode.Parse(json));
        result.Should().NotBeNull();
    }

    [Fact]
    public void MergeChain_MalformedJson_Skipped()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}""",
            "not valid json",
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.Repository.DatabaseType.Should().Be("SqlServer");
    }

    [Fact]
    public void MergeJson_DeepNesting_MergesRecursively()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetMigrationOrder":"Successively","TargetDefaults":{"DbCommandTimeoutInSeconds":20}}}}}""",
            """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetDefaults":{"DbCommandTimeoutInSeconds":60}}}}}"""
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder.Should().Be("Successively"); // not overridden
        result.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds.Should().Be(60); // overridden
    }
}
