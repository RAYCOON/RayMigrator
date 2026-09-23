using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Configuration;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the engine loads the appsettings hierarchy through the shared merger (#23):
/// real files, real loader, assertions on the resulting configuration section.
/// </summary>
public class JsonOptionsSourceAliasMergeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RayMigrator_AliasMerge_" + Guid.NewGuid().ToString("N"));

    public JsonOptionsSourceAliasMergeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private void Write(string fileName, string content) => File.WriteAllText(Path.Combine(_dir, fileName), content);

    private const string BaseTwoProducts = """
        {"RayMigrator":{
          "Repository":{"DatabaseType":"Sqlite","ConnectionString":"Data Source=repo.sqlite","TableBaseName":""},
          "Products":[
            {"Alias":"Shop","MigrationErrorAction":"Terminate","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"Data Source=shop.sqlite"}]}]},
            {"Alias":"Crm","MigrationErrorAction":"Terminate","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"Data Source=crm.sqlite"}]}]}
          ],
          "Serilog":{"Using":["Serilog.Sinks.Console"],"MinimumLevel":{"Default":"Warning"},"WriteTo":[{"Name":"Console"}]}}}
        """;

    [Fact]
    public async Task LoadAsync_EnvironmentFile_OverridesProductByAlias()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Production.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationErrorAction":"Rollback"}]}}""");

        var result = await new JsonOptionsSource(_dir).LoadAsync("Crm", "Production");
        var section = result.RayMigratorConfigSection;

        section["Products:0:Alias"].Should().Be("Shop");
        section["Products:0:MigrationErrorAction"].Should().Be("Terminate");
        section["Products:1:Alias"].Should().Be("Crm");
        section["Products:1:MigrationErrorAction"].Should().Be("Rollback");
        section["Products:1:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=crm.sqlite");
        section["Products:2:Alias"].Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ReversedOrder_TargetsStayWithTheirProduct()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Staging.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationErrorAction":"Rollback"},{"Alias":"Shop","MigrationErrorAction":"Terminate"}]}}""");

        var section = (await new JsonOptionsSource(_dir).LoadAsync("Crm", "Staging")).RayMigratorConfigSection;

        section["Products:0:Alias"].Should().Be("Shop");
        section["Products:0:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=shop.sqlite");
        section["Products:1:Alias"].Should().Be("Crm");
        section["Products:1:MigrationErrorAction"].Should().Be("Rollback");
        section["Products:1:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=crm.sqlite");
    }

    [Fact]
    public async Task LoadAsync_NonAliasArray_IsReplaced_NotIndexMerged()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Production.json", """{"RayMigrator":{"Serilog":{"WriteTo":[{"Name":"File","Args":{"path":"x.log"}}]}}}""");

        var section = (await new JsonOptionsSource(_dir).LoadAsync("Shop", "Production")).RayMigratorConfigSection;

        section["Serilog:WriteTo:0:Name"].Should().Be("File");
        section["Serilog:WriteTo:1:Name"].Should().BeNull();
        section["Serilog:MinimumLevel:Default"].Should().Be("Warning");
    }

    [Fact]
    public async Task LoadAsync_EnvPlaceholders_ResolvedAfterMerge()
    {
        const string variable = "RM_TEST_23_PLACEHOLDER";
        Environment.SetEnvironmentVariable(variable, "Data Source=from-env.sqlite");
        try
        {
            Write("appsettings.json", BaseTwoProducts);
            Write("appsettings.Crm.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","TargetGroups":[{"Alias":"Backend","Targets":[{"Alias":"Main","ConnectionString":"{ENV:RM_TEST_23_PLACEHOLDER}"}]}]}]}}""");

            var result = await new JsonOptionsSource(_dir).LoadAsync("Crm", "Production");

            result.RayMigratorConfigSection["Products:1:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=from-env.sqlite");
            result.ReplacedEnvironmentVariables.Should().ContainSingle(v => v.EnvironmentVariableName == variable);
            result.HostConfiguration.Should().NotBeNull();
            result.HostConfiguration!["RayMigrator:Products:1:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("{ENV:RM_TEST_23_PLACEHOLDER}");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task LoadAsync_Diagnostics_ListAllFourFilesWithFoundFlag_FromTheSharedChain()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Shop.json", """{"RayMigrator":{}}""");

        var result = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Production");

        var expected = ConfigurationFileChain.FileNamesFor("Shop", "Production").Select(f => Path.Combine(_dir, f.FileName)).ToList();
        result.ConfigFileDiagnostics.Should().NotBeNull();
        result.ConfigFileDiagnostics!.Select(d => d.Filename).Should().Equal(expected);
        result.ConfigFileDiagnostics.Select(d => d.Found).Should().Equal(true, false, true, false);
    }

    [Fact]
    public async Task LoadAsync_CommentsAndTrailingCommas_StillAccepted()
    {
        Write("appsettings.json", """
            {
              // the base file may carry comments, as it always could
              "RayMigrator": {
                "Repository": { "DatabaseType": "Sqlite", "ConnectionString": "Data Source=repo.sqlite", "TableBaseName": "", },
                "Products": [ { "Alias": "Shop", "TargetGroups": [], }, ],
              },
            }
            """);

        var section = (await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev")).RayMigratorConfigSection;

        section["Repository:DatabaseType"].Should().Be("Sqlite");
        section["Products:0:Alias"].Should().Be("Shop");
    }

    [Fact]
    public async Task LoadAsync_NoRayMigratorNodeAnywhere_ThrowsConfigurationValidationException()
    {
        Write("appsettings.json", "{}");

        var act = () => new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");

        await act.Should().ThrowAsync<ConfigurationValidationException>().WithMessage("*Could not find any RayMigrator configuration*");
    }

    [Fact]
    public async Task LoadAsync_MalformedFile_ThrowsConfigurationValidationException()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Dev.json", "{ not json");

        var act = () => new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");

        await act.Should().ThrowAsync<ConfigurationValidationException>();
    }

    // ── Golden cases shared with the Config Wizard test suite ─────────────────────────────

    public static TheoryData<string> GoldenCases()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases")).OrderBy(d => d))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void GoldenCases_Engine_MergeEqualsExpected(string caseName)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases", caseName);
        var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "case.json")))!;
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "expected.json")))!;
        var diagnostics = new List<(string Filename, bool Found)>();

        var actual = JsonOptionsSource.MergeConfigurationFiles(dir, meta["product"]!.GetValue<string>(), meta["environment"]!.GetValue<string>(), diagnostics);

        JsonNode.DeepEquals(expected, actual).Should().BeTrue(
            $"case '{caseName}' ({meta["description"]}) expected\n{expected.ToJsonString()}\nbut got\n{actual.ToJsonString()}");
        diagnostics.Should().HaveCount(4);
    }
}
