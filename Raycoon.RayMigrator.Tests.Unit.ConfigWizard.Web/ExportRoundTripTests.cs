using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// An imported configuration exported unchanged must give the engine the same configuration: no default of the
/// wizard's model may override a value of the base, and no key the wizard does not read may get lost.
/// </summary>
public class ExportRoundTripTests
{
    private const string BaseJson = """
        { "RayMigrator": {
           "Repository": { "DatabaseType": "PostgreSQL", "ConnectionString": "Host=a", "SchemaName": "mig", "HeartbeatIntervalInSeconds": 10 },
           "ProductDefaults": { "RequireRollbackFile": false },
           "Products": [ { "Alias": "Shop", "MigrationFilesRootDirectory": "./m",
             "TargetGroups": [ { "Alias": "Db", "DatabaseType": "PostgreSQL", "Targets": [ { "Alias": "Main", "ConnectionString": "Host=t" } ] } ] } ]
        } }
        """;

    private const string ProductionJson = """{ "RayMigrator": { "Repository": { "ConnectionString": "{ENV:PROD_REPO}" } } }""";

    private static JsonNode? Effective(Dictionary<string, string> files, string product, string environment)
        => ConfigurationJsonMerger.MergeChain(ConfigurationFileChain.FileNamesFor(product, environment)
            .Select(f => files.TryGetValue(f.FileName, out var json) ? ConfigurationJsonMerger.Parse(json) : null)
            .ToList());

    [Fact]
    public void EnvironmentFile_WithOnlyAConnectionString_KeepsTheValuesOfTheBase()
    {
        var input = new Dictionary<string, string> { ["appsettings.json"] = BaseJson, ["appsettings.Production.json"] = ProductionJson };

        var output = ZipExportService.ComputeExportJsons(ConfigurationFileParser.Parse(input));

        var ray = Effective(output, "Shop", "Production")!["RayMigrator"]!;
        ray["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        ray["Repository"]!["SchemaName"]!.GetValue<string>().Should().Be("mig");
        ray["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PROD_REPO}");
        ray["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void ScaffoldedEnvironmentModel_WritesOnlyItsConnectionString()
    {
        var baseModel = ConfigurationSerializer.LoadFromJson(BaseJson);
        var environment = new ConfigurationModel { Repository = new RepositoryModel { ConnectionString = "{ENV:REPO_DEV}" } };

        var layer = JsonNode.Parse(ConfigurationSerializer.ToLayerJson(environment, baseModel))!;

        layer["RayMigrator"]!.ToJsonString().Should().Be("""{"Repository":{"ConnectionString":"{ENV:REPO_DEV}"}}""");
    }

    [Fact]
    public void KeysTheWizardDoesNotRead_AreKept()
    {
        const string json = """
            { "RayMigrator": {
               "Repository": { "DatabaseType": "SqlServer", "ConnectionString": "x", "OrphanedRunThresholdInMinutes": 5 },
               "DatabaseLogging": { "Enabled": false, "DatabaseType": "SqlServer", "ConnectionString": "y" },
               "ProductDefaults": { "TargetGroupDefaults": { "TargetDefaults": { "FutureTargetKey": 1 } } },
               "Products": [ { "Alias": "Shop", "MigrationFilesRootDirectory": "./m" } ],
               "Serilog": { "Using": [ "Serilog.Sinks.File" ], "Enrich": [ "FromLogContext" ], "MinimumLevel": "Debug",
                 "WriteTo": [ { "Name": "File", "Args": { "path": "log.txt", "formatter": { "type": "X, Y" } } } ] }
            } }
            """;

        var output = ZipExportService.ComputeExportJsons(ConfigurationFileParser.Parse(new() { ["appsettings.json"] = json }));

        var ray = JsonNode.Parse(output["appsettings.json"])!["RayMigrator"]!;
        ray["Repository"]!["OrphanedRunThresholdInMinutes"]!.GetValue<int>().Should().Be(5);
        ray["DatabaseLogging"]!["Enabled"]!.GetValue<bool>().Should().BeFalse();
        ray["ProductDefaults"]!["TargetGroupDefaults"]!["TargetDefaults"]!["FutureTargetKey"]!.GetValue<int>().Should().Be(1);
        ray["Serilog"]!["Using"]!.ToJsonString().Should().Be("""["Serilog.Sinks.File"]""");
        ray["Serilog"]!["Enrich"]!.ToJsonString().Should().Be("""["FromLogContext"]""");
        ray["Serilog"]!["WriteTo"]![0]!["Args"]!["formatter"].Should().BeOfType<JsonObject>()
            .Which["type"]!.GetValue<string>().Should().Be("X, Y");
    }
}
