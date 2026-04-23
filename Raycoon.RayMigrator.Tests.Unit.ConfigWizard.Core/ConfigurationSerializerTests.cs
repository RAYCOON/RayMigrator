// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class ConfigurationSerializerTests
{
    [Fact]
    public void LoadFromJson_ValidJson_ParsesAllSections()
    {
        var model = ConfigurationSerializer.LoadFromJson(TestModelFactory.CreateValidJson());

        model.Repository.DatabaseType.Should().Be("SqlServer");
        model.Repository.SchemaName.Should().Be("migrations");
        model.Products.Should().HaveCount(1);
        model.Products[0].Alias.Should().Be("MyApp");
        model.Products[0].TargetGroups.Should().HaveCount(1);
        model.Products[0].TargetGroups[0].Targets.Should().HaveCount(1);
        model.Serilog.MinimumLevelDefault.Should().Be("Information");
        model.Serilog.WriteTo.Should().HaveCount(1);
    }

    [Fact]
    public void LoadFromJson_EmptyRayMigrator_ReturnsEmptyModel()
    {
        var model = ConfigurationSerializer.LoadFromJson("""{"RayMigrator": {}}""");
        model.Products.Should().BeEmpty();
        model.DatabaseLogging.Should().BeNull();
    }

    [Fact]
    public void LoadFromJson_NoRayMigratorKey_ReturnsEmptyModel()
    {
        var model = ConfigurationSerializer.LoadFromJson("""{"Other": {}}""");
        model.Products.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var original = ConfigurationSerializer.LoadFromJson(TestModelFactory.CreateValidJson());
        var json = ConfigurationSerializer.ToJson(original);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.Repository.DatabaseType.Should().Be(original.Repository.DatabaseType);
        roundTripped.Repository.ConnectionString.Should().Be(original.Repository.ConnectionString);
        roundTripped.Products.Should().HaveCount(original.Products.Count);
        roundTripped.Products[0].Alias.Should().Be(original.Products[0].Alias);
    }

    [Fact]
    public void RoundTrip_PreservesUnknownKeys()
    {
        string json = """
        {
          "RayMigrator": {
            "Repository": { "DatabaseType": "SqlServer" },
            "AdminDb": { "Url": "http://localhost:5000" },
            "ApiUrl": "http://admin.local"
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        var output = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(output);

        doc!["RayMigrator"]!["AdminDb"]!["Url"]!.GetValue<string>().Should().Be("http://localhost:5000");
        doc!["RayMigrator"]!["ApiUrl"]!.GetValue<string>().Should().Be("http://admin.local");
    }

    // ── CliTools Serialization ───────────────────────────────────

    [Fact]
    public void LoadFromJson_WithCliTools_ParsesCliTools()
    {
        string json = """
        {
          "RayMigrator": {
            "CliTools": [{
              "Alias": "sqlcmd",
              "DatabaseType": "SqlServer",
              "ExecutablePath": "sqlcmd",
              "ArgumentTemplate": "-S {Server} -i {FilePath}",
              "InputMode": "File",
              "SuccessExitCodes": ["0"],
              "CliToolTimeoutInSeconds": 120
            }]
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.CliTools.Should().HaveCount(1);
        model.CliTools[0].Alias.Should().Be("sqlcmd");
        model.CliTools[0].InputMode.Should().Be("File");
        model.CliTools[0].SuccessExitCodes.Should().ContainSingle().Which.Should().Be("0");
    }

    [Fact]
    public void ToJson_WithCliTools_SerializesCliTools()
    {
        var model = new ConfigurationModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool());

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["CliTools"]!.AsArray().Should().HaveCount(1);
        doc!["RayMigrator"]!["CliTools"]![0]!["Alias"]!.GetValue<string>().Should().Be("sqlcmd");
    }

    [Fact]
    public void ToJson_NoCliTools_OmitsCliToolsKey()
    {
        var model = new ConfigurationModel();
        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["CliTools"].Should().BeNull();
    }

    // ── UseCliToolAlias Serialization ────────────────────────────────

    [Fact]
    public void RoundTrip_UseCliToolAlias_AllLevels()
    {
        string json = """
        {
          "RayMigrator": {
            "ProductDefaults": { "UseCliToolAlias": "sqlcmd" },
            "Products": [{
              "Alias": "MyApp",
              "MigrationFilesRootDirectory": "./Migrations",
              "UseCliToolAlias": "psql",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "PostgreSQL",
                "UseCliToolAlias": "psql-docker",
                "Targets": [{
                  "Alias": "MainDB",
                  "ConnectionString": "Host=localhost",
                  "UseCliToolAlias": "custom-tool",
                  "CliToolParameters": {
                    "Server": "localhost",
                    "Database": "mydb"
                  }
                }]
              }]
            }]
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);

        model.ProductDefaults.UseCliToolAlias.Should().Be("sqlcmd");
        model.Products[0].UseCliToolAlias.IsOverridden.Should().BeTrue();
        model.Products[0].UseCliToolAlias.Value.Should().Be("psql");
        model.Products[0].TargetGroups[0].UseCliToolAlias.IsOverridden.Should().BeTrue();
        model.Products[0].TargetGroups[0].UseCliToolAlias.Value.Should().Be("psql-docker");
        model.Products[0].TargetGroups[0].Targets[0].UseCliToolAlias.IsOverridden.Should().BeTrue();
        model.Products[0].TargetGroups[0].Targets[0].UseCliToolAlias.Value.Should().Be("custom-tool");
        model.Products[0].TargetGroups[0].Targets[0].CliToolParameters.Should().ContainKey("Server");
        model.Products[0].TargetGroups[0].Targets[0].CliToolParameters!["Database"].Should().Be("mydb");

        // Round-trip
        var outputJson = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(outputJson);

        roundTripped.ProductDefaults.UseCliToolAlias.Should().Be("sqlcmd");
        roundTripped.Products[0].UseCliToolAlias.Value.Should().Be("psql");
        roundTripped.Products[0].TargetGroups[0].Targets[0].CliToolParameters!["Server"].Should().Be("localhost");
    }

    // ── DatabaseLogging Serialization ────────────────────────────

    [Fact]
    public void RoundTrip_WithDatabaseLogging()
    {
        string json = """
        {
          "RayMigrator": {
            "DatabaseLogging": {
              "DatabaseType": "PostgreSQL",
              "ConnectionString": "Host=localhost",
              "SchemaName": "logs",
              "MinimumLevel": "Warning",
              "DbCommandTimeoutInSeconds": 30
            }
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.DatabaseLogging.Should().NotBeNull();
        model.DatabaseLogging!.DatabaseType.Should().Be("PostgreSQL");
        model.DatabaseLogging.MinimumLevel.Should().Be("Warning");

        var output = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(output);
        roundTripped.DatabaseLogging!.MinimumLevel.Should().Be("Warning");
    }

    [Fact]
    public void LoadFromJson_SetsFilePathAndIsModified()
    {
        var model = ConfigurationSerializer.LoadFromJson("""{"RayMigrator":{}}""", "test.json");
        model.FilePath.Should().Be("test.json");
        model.IsModified.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════
    // Products field-level diff — alias-based matching
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ToJson_Diff_Products_MatchingAlias_OnlyConnectionStringInDiff()
    {
        var baseModel = TestModelFactory.CreateValidModel();
        baseModel.Products[0].TargetGroups[0].Targets[0].ConnectionString = "{ENV:BASE_CONN}";

        var peModel = TestModelFactory.CreateValidModel();
        peModel.Products[0].TargetGroups[0].Targets[0].ConnectionString = "{ENV:DEV_CONN}";

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var products = JsonNode.Parse(json)?["RayMigrator"]?["Products"]?.AsArray();

        products.Should().NotBeNull();
        products!.Count.Should().Be(1);

        var target = products[0]?["TargetGroups"]?[0]?["Targets"]?[0];
        target!["Alias"]!.GetValue<string>().Should().Be("MainDB", "always included as identity");
        target["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:DEV_CONN}");
    }

    [Fact]
    public void ToJson_Diff_Products_NoChanges_ProductsOmitted()
    {
        var baseModel = TestModelFactory.CreateValidModel();
        var peModel = TestModelFactory.CreateValidModel();

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"]?.AsObject();

        ray!["Products"].Should().BeNull("no product fields changed");
    }

    [Fact]
    public void ToJson_Diff_Products_NewTarget_FullySerialized()
    {
        var baseModel = TestModelFactory.CreateValidModel();

        var peModel = TestModelFactory.CreateValidModel();
        peModel.Products[0].TargetGroups[0].Targets.Add(new TargetModel
        {
            Alias = "ArchiveDB",
            ConnectionString = "{ENV:ARCHIVE_CONN}",
        });

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var targets = JsonNode.Parse(json)?["RayMigrator"]?["Products"]?[0]?["TargetGroups"]?[0]?["Targets"]?.AsArray();

        targets.Should().NotBeNull();
        // Only the new target appears in the diff; unchanged MainDB is inherited from base
        targets!.Count.Should().Be(1);
        targets[0]!["Alias"]!.GetValue<string>().Should().Be("ArchiveDB");
        targets[0]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:ARCHIVE_CONN}");
    }

    [Fact]
    public void ToJson_Diff_Products_MigrationFilesRootDirectoryChanged_IncludedInDiff()
    {
        var baseModel = TestModelFactory.CreateValidModel();
        baseModel.Products[0].MigrationFilesRootDirectory = "./Migrations/MyApp";

        var peModel = TestModelFactory.CreateValidModel();
        peModel.Products[0].MigrationFilesRootDirectory = "./DevMigrations/MyApp";

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var product = JsonNode.Parse(json)?["RayMigrator"]?["Products"]?[0];

        product!["MigrationFilesRootDirectory"]!.GetValue<string>().Should().Be("./DevMigrations/MyApp");
    }

    [Fact]
    public void ToJson_Diff_Products_TargetTimeoutOverride_IncludedInDiff()
    {
        var baseModel = TestModelFactory.CreateValidModel();

        var peModel = TestModelFactory.CreateValidModel();
        peModel.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds.IsOverridden = true;
        peModel.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds.Value = 5;

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var target = JsonNode.Parse(json)?["RayMigrator"]?["Products"]?[0]?["TargetGroups"]?[0]?["Targets"]?[0];

        target!["DbCommandTimeoutInSeconds"]!.GetValue<int>().Should().Be(5);
    }

    [Fact]
    public void ToJson_Diff_Products_UnmatchedAlias_FullySerialized()
    {
        var baseModel = TestModelFactory.CreateValidModel(); // Has "MainDB"

        var peModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "MyApp" };
        var tg = new TargetGroupModel { Alias = "Backend", DatabaseType = "SqlServer" };
        tg.Targets.Add(new TargetModel { Alias = "RenamedDB", ConnectionString = "{ENV:RENAMED_CONN}" });
        product.TargetGroups.Add(tg);
        peModel.Products.Add(product);

        var json = ConfigurationSerializer.ToJson(peModel, baseModel);
        var target = JsonNode.Parse(json)?["RayMigrator"]?["Products"]?[0]?["TargetGroups"]?[0]?["Targets"]?[0];

        // Unmatched alias → full serialization (Alias + ConnectionString)
        target!["Alias"]!.GetValue<string>().Should().Be("RenamedDB");
        target["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:RENAMED_CONN}");
    }
}
