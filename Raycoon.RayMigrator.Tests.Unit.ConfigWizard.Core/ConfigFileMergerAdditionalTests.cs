using System.Text.Json.Nodes;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional ConfigFileMerger tests for edge cases not covered in ConfigFileMergerTests.
/// </summary>
public class ConfigFileMergerAdditionalTests
{
    // ── Alias-keyed array merge tests ────────────────────────────────

    [Fact]
    public void MergeJson_ProductsSameAlias_MergesByAlias_PreservesMigrationFilesRootDirectory()
    {
        // base has MigrationFilesRootDirectory + two targets; override has same alias but omits MigrationFilesRootDirectory
        var baseJson = """
        {
          "RayMigrator": {
            "Products": [{
              "Alias": "MyApp",
              "MigrationFilesRootDirectory": "./Migrations/MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [
                  { "Alias": "BackendDB",  "ConnectionString": "base_conn_1" },
                  { "Alias": "BackendDB2", "ConnectionString": "base_conn_2" }
                ]
              }]
            }]
          }
        }
        """;

        var overrideJson = """
        {
          "RayMigrator": {
            "Products": [{
              "Alias": "MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [
                  { "Alias": "BackendDB",  "ConnectionString": "override_conn_1" },
                  { "Alias": "BackendDB2", "ConnectionString": "override_conn_2" }
                ]
              }]
            }]
          }
        }
        """;

        var result = ConfigFileMerger.MergeChain(new List<string> { baseJson, overrideJson });

        result.Products.Should().HaveCount(1);
        var product = result.Products[0];
        product.Alias.Should().Be("MyApp");
        product.MigrationFilesRootDirectory.Should().Be("./Migrations/MyApp"); // preserved from base
        product.TargetGroups.Should().HaveCount(1);
        product.TargetGroups[0].Targets.Should().HaveCount(2);
        product.TargetGroups[0].Targets[0].ConnectionString.Should().Be("override_conn_1"); // overridden
        product.TargetGroups[0].Targets[1].ConnectionString.Should().Be("override_conn_2"); // overridden
    }

    [Fact]
    public void MergeJson_TargetsSameAlias_MergesByAlias_PreservesBaseProperties()
    {
        // base target has ConnectionString + DbCommandMaxRetries; override only sets ConnectionString
        var baseJson = """
        {
          "RayMigrator": {
            "Products": [{
              "Alias": "MyApp",
              "MigrationFilesRootDirectory": "./Migrations/MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{ "Alias": "BackendDB", "ConnectionString": "base_conn", "DbCommandMaxRetries": 0 }]
              }]
            }]
          }
        }
        """;

        var overrideJson = """
        {
          "RayMigrator": {
            "Products": [{
              "Alias": "MyApp",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{ "Alias": "BackendDB", "ConnectionString": "override_conn" }]
              }]
            }]
          }
        }
        """;

        var result = ConfigFileMerger.MergeChain(new List<string> { baseJson, overrideJson });

        var target = result.Products[0].TargetGroups[0].Targets[0];
        target.ConnectionString.Should().Be("override_conn");       // overridden
        target.DbCommandMaxRetries.Value.Should().Be(0);            // preserved from base
    }

    [Fact]
    public void MergeJson_NonAliasKeyedArray_StillReplaced()
    {
        // Serilog.WriteTo has "Name" property, not "Alias" — must be completely replaced
        var baseJson = """
        {
          "RayMigrator": {
            "Serilog": {
              "WriteTo": [{ "Name": "Console" }, { "Name": "File" }]
            }
          }
        }
        """;

        var overrideJson = """
        {
          "RayMigrator": {
            "Serilog": {
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;

        var result = ConfigFileMerger.MergeChain(new List<string> { baseJson, overrideJson });

        // Override replaces entirely — File sink from base must be gone
        result.Serilog.WriteTo.Should().HaveCount(1);
        result.Serilog.WriteTo[0].Name.Should().Be("Console");
    }

    // ── IsAliasKeyedArray helper tests ───────────────────────────────

    [Fact]
    public void IsAliasKeyedArray_EmptyArray_ReturnsFalse()
    {
        var arr = new JsonArray();
        ConfigFileMerger.IsAliasKeyedArray(arr).Should().BeFalse();
    }

    [Fact]
    public void IsAliasKeyedArray_ArrayWithAlias_ReturnsTrue()
    {
        var arr = JsonNode.Parse("""[{"Alias":"A"},{"Alias":"B"}]""")!.AsArray();
        ConfigFileMerger.IsAliasKeyedArray(arr).Should().BeTrue();
    }

    [Fact]
    public void IsAliasKeyedArray_ArrayWithoutAlias_ReturnsFalse()
    {
        var arr = JsonNode.Parse("""[{"Name":"Console"},{"Name":"File"}]""")!.AsArray();
        ConfigFileMerger.IsAliasKeyedArray(arr).Should().BeFalse();
    }

    // ── Existing tests ───────────────────────────────────────────────

    [Fact]
    public void MergeJson_OverrideNull_ReturnsBase()
    {
        var baseJson = JsonNode.Parse("""{"key":"base-value"}""");
        var result = ConfigFileMerger.MergeJson(baseJson, null);
        result.Should().NotBeNull();
        result!["key"]!.GetValue<string>().Should().Be("base-value");
    }

    [Fact]
    public void MergeChain_ThreeFiles_LastWins()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer","SchemaName":"v1"}}}""",
            """{"RayMigrator":{"Repository":{"SchemaName":"v2"}}}""",
            """{"RayMigrator":{"Repository":{"SchemaName":"v3"}}}""",
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.Repository.DatabaseType.Should().Be("SqlServer");
        result.Repository.SchemaName.Should().Be("v3");
    }

    [Fact]
    public void MergeChain_AllMalformedFiles_ReturnsEmptyModel()
    {
        var files = new List<string>
        {
            "not json",
            "{ also not json",
        };

        var result = ConfigFileMerger.MergeChain(files);
        result.Products.Should().BeEmpty();
    }

    [Fact]
    public void MergeJson_ObjectsMergeRecursively_NewKeyAdded()
    {
        var baseNode = JsonNode.Parse("""{"outer":{"a":"1"}}""");
        var overrideNode = JsonNode.Parse("""{"outer":{"b":"2"}}""");

        var result = ConfigFileMerger.MergeJson(baseNode, overrideNode);
        result!["outer"]!["a"]!.GetValue<string>().Should().Be("1");
        result["outer"]!["b"]!.GetValue<string>().Should().Be("2");
    }

    [Fact]
    public void MergeChain_CliToolsAliasKeyed_DifferentAliasesPreserveBoth()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"CliTools":[{"Alias":"sqlcmd","ExecutablePath":"sqlcmd","ArgumentTemplate":"-i {FilePath}","InputMode":"File","SuccessExitCodes":["0"],"CliToolTimeoutInSeconds":120}]}}""",
            """{"RayMigrator":{"CliTools":[{"Alias":"psql","ExecutablePath":"psql","ArgumentTemplate":"-f {FilePath}","InputMode":"File","SuccessExitCodes":["0"],"CliToolTimeoutInSeconds":120}]}}""",
        };

        var result = ConfigFileMerger.MergeChain(files);
        // Different aliases: both preserved
        result.CliTools.Should().HaveCount(2);
        result.CliTools[0].Alias.Should().Be("psql");
        result.CliTools[1].Alias.Should().Be("sqlcmd");
    }

    [Fact]
    public void MergeChainToJson_MultipleFiles_ResultIsValidJson()
    {
        var files = new List<string>
        {
            """{"RayMigrator":{"Repository":{"DatabaseType":"SqlServer"}}}""",
            """{"RayMigrator":{"Repository":{"SchemaName":"migrations"}}}""",
        };

        var json = ConfigFileMerger.MergeChainToJson(files);

        var act = () => JsonNode.Parse(json);
        act.Should().NotThrow();
    }
}
