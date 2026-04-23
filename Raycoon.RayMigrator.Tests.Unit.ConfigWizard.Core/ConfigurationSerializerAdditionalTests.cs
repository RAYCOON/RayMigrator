// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional ConfigurationSerializer tests for Serilog overrides, sink args,
/// overridable bool serialization, and edge cases not covered in ConfigurationSerializerTests.
/// </summary>
public class ConfigurationSerializerAdditionalTests
{
    // ── Serilog advanced ─────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SerilogWithLevelOverrides_PreservesOverrides()
    {
        string json = """
        {
          "RayMigrator": {
            "Serilog": {
              "MinimumLevel": {
                "Default": "Information",
                "Override": {
                  "Microsoft": "Warning",
                  "System": "Error"
                }
              },
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.Serilog.MinimumLevelDefault.Should().Be("Information");
        model.Serilog.MinimumLevelOverrides.Should().ContainKey("Microsoft");
        model.Serilog.MinimumLevelOverrides["Microsoft"].Should().Be("Warning");
        model.Serilog.MinimumLevelOverrides.Should().ContainKey("System");

        var output = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(output);
        roundTripped.Serilog.MinimumLevelOverrides["Microsoft"].Should().Be("Warning");
        roundTripped.Serilog.MinimumLevelOverrides["System"].Should().Be("Error");
    }

    [Fact]
    public void RoundTrip_SerilogWithSinkArgs_PreservesArgs()
    {
        string json = """
        {
          "RayMigrator": {
            "Serilog": {
              "MinimumLevel": { "Default": "Information" },
              "WriteTo": [{
                "Name": "File",
                "Args": {
                  "path": "./logs/app.log",
                  "rollingInterval": "Day"
                }
              }]
            }
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.Serilog.WriteTo.Should().HaveCount(1);
        model.Serilog.WriteTo[0].Name.Should().Be("File");
        model.Serilog.WriteTo[0].Args.Should().ContainKey("path");
        model.Serilog.WriteTo[0].Args["path"].Should().Be("./logs/app.log");

        var output = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(output);
        roundTripped.Serilog.WriteTo[0].Args["rollingInterval"].Should().Be("Day");
    }

    [Fact]
    public void LoadFromJson_SerilogMinimumLevelAsString_ParsesCorrectly()
    {
        // Some configs write MinimumLevel directly as a string value
        string json = """
        {
          "RayMigrator": {
            "Serilog": {
              "MinimumLevel": "Warning",
              "WriteTo": [{ "Name": "Console" }]
            }
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.Serilog.MinimumLevelDefault.Should().Be("Warning");
    }

    // ── RequireRollbackFile = false serialization ────────────────────

    [Fact]
    public void RoundTrip_RequireRollbackFileFalse_PreservesValue()
    {
        var model = TestModelFactory.CreateValidModel();
        model.ProductDefaults.RequireRollbackFile = false;

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.ProductDefaults.RequireRollbackFile.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_ProductOverridableRequireRollbackFileFalse_PreservesOverride()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["Products"]![0]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    // ── Repository extra fields ───────────────────────────────────────

    [Fact]
    public void RoundTrip_Repository_AllFields_Preserved()
    {
        string json = """
        {
          "RayMigrator": {
            "Repository": {
              "DatabaseType": "PostgreSQL",
              "ConnectionString": "Host=db;Database=repos",
              "SchemaName": "ray",
              "TableBaseName": "MyPrefix",
              "DbCommandTimeoutInSeconds": 90,
              "DbCommandMaxRetries": 5,
              "DbCommandWaitTimeInMsBeforeRetry": 750
            }
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);
        model.Repository.TableBaseName.Should().Be("MyPrefix");
        model.Repository.DbCommandTimeoutInSeconds.Should().Be(90);
        model.Repository.DbCommandMaxRetries.Should().Be(5);
        model.Repository.DbCommandWaitTimeInMsBeforeRetry.Should().Be(750);

        var output = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(output);
        roundTripped.Repository.TableBaseName.Should().Be("MyPrefix");
        roundTripped.Repository.DbCommandMaxRetries.Should().Be(5);
    }

    // ── Target overridable int round-trip ────────────────────────────

    [Fact]
    public void RoundTrip_TargetOverridableTimeout_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds =
            new OverridableValue<int> { IsOverridden = true, Value = 45 };

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds.IsOverridden.Should().BeTrue();
        roundTripped.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds.Value.Should().Be(45);
    }

    [Fact]
    public void RoundTrip_TargetNotOverriddenTimeout_OmittedFromJson()
    {
        var model = TestModelFactory.CreateValidModel();
        // DbCommandTimeoutInSeconds is NOT overridden

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        // When not overridden, the key should not appear on the target object
        var targetNode = doc!["RayMigrator"]!["Products"]![0]!["TargetGroups"]![0]!["Targets"]![0]!;
        targetNode["DbCommandTimeoutInSeconds"].Should().BeNull();
    }

    // ── TargetGroup overridable strings ──────────────────────────────

    [Fact]
    public void RoundTrip_TargetGroupTargetMigrationOrderOverride_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroups[0].TargetMigrationOrder =
            new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.Products[0].TargetGroups[0].TargetMigrationOrder.IsOverridden.Should().BeTrue();
        roundTripped.Products[0].TargetGroups[0].TargetMigrationOrder.Value.Should().Be("Simultaneously");
    }

    // ── IsModified flag ───────────────────────────────────────────────

    [Fact]
    public void LoadFromJson_SetsIsModifiedFalse()
    {
        var model = ConfigurationSerializer.LoadFromJson(TestModelFactory.CreateValidJson());
        model.IsModified.Should().BeFalse();
    }

    // ── StopRollbackOnMissingRollbackFile round-trips ─────────────────

    [Fact]
    public void RoundTrip_StopRollbackOnMissingRollbackFile_ProductDefaults_True_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.ProductDefaults.StopRollbackOnMissingRollbackFile = true;

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.ProductDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_StopRollbackOnMissingRollbackFile_TargetGroupDefaults_True_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.ProductDefaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile = true;

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.ProductDefaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_StopRollbackOnMissingRollbackFile_ProductOverride_True_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["Products"]![0]!["StopRollbackOnMissingRollbackFile"]!.GetValue<bool>()
            .Should().BeTrue();

        var roundTripped = ConfigurationSerializer.LoadFromJson(json);
        roundTripped.Products[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeTrue();
        roundTripped.Products[0].StopRollbackOnMissingRollbackFile.Value.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_StopRollbackOnMissingRollbackFile_TargetGroupOverride_True_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["Products"]![0]!["TargetGroups"]![0]!["StopRollbackOnMissingRollbackFile"]!
            .GetValue<bool>().Should().BeTrue();

        var roundTripped = ConfigurationSerializer.LoadFromJson(json);
        roundTripped.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeTrue();
        roundTripped.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile.Value.Should().BeTrue();
    }

    [Fact]
    public void LoadFromJson_MissingStopRollbackOnMissingRollbackFile_DefaultsToTrue()
    {
        string json = """
        {
          "RayMigrator": {
            "ProductDefaults": {
              "MigrationErrorAction": "Terminate",
              "TargetGroupDefaults": {
                "TargetMigrationOrder": "Successively",
                "HashValidationScope": "File",
                "TargetDefaults": { "DbCommandTimeoutInSeconds": 20 }
              }
            },
            "Products": [{
              "Alias": "App",
              "MigrationFilesRootDirectory": "./Migrations",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{ "Alias": "DB", "ConnectionString": "Server=localhost" }]
              }]
            }]
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);

        model.ProductDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
        model.ProductDefaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
        model.Products[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
        model.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
    }

    // ── TargetGroupMigrationOrder round-trip ──────────────────────────

    [Fact]
    public void RoundTrip_TargetGroupMigrationOrder_Preserved()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        model.Products[0].TargetGroupMigrationOrder = "Backend,Frontend";

        var json = ConfigurationSerializer.ToJson(model);
        var roundTripped = ConfigurationSerializer.LoadFromJson(json);

        roundTripped.Products[0].TargetGroupMigrationOrder.Should().Be("Backend,Frontend");
    }

    [Fact]
    public void LoadFromJson_MissingTargetGroupMigrationOrder_DefaultsToNull()
    {
        string json = """
        {
          "RayMigrator": {
            "Products": [{
              "Alias": "App",
              "MigrationFilesRootDirectory": "./Migrations",
              "TargetGroups": [{
                "Alias": "Backend",
                "DatabaseType": "SqlServer",
                "Targets": [{ "Alias": "DB", "ConnectionString": "Server=localhost" }]
              }]
            }]
          }
        }
        """;

        var model = ConfigurationSerializer.LoadFromJson(json);

        model.Products[0].TargetGroupMigrationOrder.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_TargetGroupMigrationOrder_Null_OmittedFromJson()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroupMigrationOrder = null;

        var json = ConfigurationSerializer.ToJson(model);
        var doc = JsonNode.Parse(json);

        doc!["RayMigrator"]!["Products"]![0]!["TargetGroupMigrationOrder"].Should().BeNull();
    }
}
