
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// End-to-end tests verifying the interaction between DefaultsPromoter (promotion)
/// and ConfigurationSerializer.ToJson(model, baseModel) (diff-based serialization)
/// across all hierarchy levels and all promotable properties.
/// </summary>
public class PromotionEndToEndTests
{
    // ══════════════════════════════════════════════════════════════════
    // Category 1: Diff-based Serialization (BuildRayMigratorNodeDiff)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void DiffSerialization_AllDefaults_OmitsRepositoryProductDefaultsSerilog()
    {
        var model = new ConfigurationModel();
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray.Should().NotBeNull();
        ray!["Repository"].Should().BeNull();
        ray["ProductDefaults"].Should().BeNull();
        ray["Serilog"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_RepositoryOnlyDatabaseTypeChanged_OnlyDatabaseTypeAppears()
    {
        var model = new ConfigurationModel();
        model.Repository.DatabaseType = "PostgreSQL";
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var repoNode = JsonNode.Parse(json)?["RayMigrator"]?["Repository"];

        repoNode.Should().NotBeNull();
        repoNode!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        // Other default fields should not appear
        repoNode["ConnectionString"].Should().BeNull();
        repoNode["SchemaName"].Should().BeNull();
        repoNode["TableBaseName"].Should().BeNull();
        repoNode["DbCommandTimeoutInSeconds"].Should().BeNull();
        repoNode["DbCommandMaxRetries"].Should().BeNull();
        repoNode["DbCommandWaitTimeInMsBeforeRetry"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_RepositoryMultipleChangedFields_AllChangedFieldsAppear()
    {
        var model = new ConfigurationModel();
        model.Repository.DatabaseType = "PostgreSQL";
        model.Repository.ConnectionString = "Host=localhost;Database=test";
        model.Repository.SchemaName = "migrations";
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var repoNode = JsonNode.Parse(json)?["RayMigrator"]?["Repository"];

        repoNode.Should().NotBeNull();
        repoNode!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        repoNode["ConnectionString"]!.GetValue<string>().Should().Be("Host=localhost;Database=test");
        repoNode["SchemaName"]!.GetValue<string>().Should().Be("migrations");
        // Default fields still absent
        repoNode["TableBaseName"].Should().BeNull();
        repoNode["DbCommandTimeoutInSeconds"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_ProductDefaultsAllMatchingDefaults_NoProductDefaultsKey()
    {
        var model = new ConfigurationModel();
        // ProductDefaults is already at constructor defaults
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_ProductDefaultsOneNonDefaultField_OnlyChangedFieldAppears()
    {
        var model = new ConfigurationModel();
        model.ProductDefaults.MigrationErrorAction = "Rollback";
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var pdNode = JsonNode.Parse(json)?["RayMigrator"]?["ProductDefaults"];

        pdNode.Should().NotBeNull();
        pdNode!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        // Field-level diff: only the changed field appears, unchanged fields are absent
        pdNode["RollbackErrorAction"].Should().BeNull();
        pdNode["MigrationFilesExtension"].Should().BeNull();
        pdNode["TargetGroupDefaults"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_SerilogMatchingDefaults_NoSerilogKey()
    {
        var model = new ConfigurationModel();
        // Serilog is at constructor defaults (MinimumLevelDefault="Information", WriteTo=empty)
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["Serilog"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_SerilogNonDefaultMinimumLevel_SerilogSectionIncluded()
    {
        var model = new ConfigurationModel();
        model.Serilog.MinimumLevelDefault = "Debug";
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var serilogNode = JsonNode.Parse(json)?["RayMigrator"]?["Serilog"];

        serilogNode.Should().NotBeNull();
        serilogNode!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");
    }

    [Fact]
    public void DiffSerialization_ProductsNonEmpty_ProductsAlwaysIncluded()
    {
        var model = new ConfigurationModel();
        model.Products.Add(TestModelFactory.CreateValidProduct("App1"));
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var productsNode = JsonNode.Parse(json)?["RayMigrator"]?["Products"];

        productsNode.Should().NotBeNull();
        productsNode!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public void DiffSerialization_NoProducts_NoProductsKey()
    {
        var model = new ConfigurationModel();
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["Products"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_DatabaseLoggingSet_AlwaysIncluded()
    {
        var model = new ConfigurationModel();
        model.DatabaseLogging = new DatabaseLoggingModel
        {
            DatabaseType = "PostgreSQL",
            ConnectionString = "Host=localhost",
            SchemaName = "logs",
        };
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var dbLogNode = JsonNode.Parse(json)?["RayMigrator"]?["DatabaseLogging"];

        dbLogNode.Should().NotBeNull();
        dbLogNode!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        dbLogNode["ConnectionString"]!.GetValue<string>().Should().Be("Host=localhost");
    }

    // ── New Category 1 tests: field-level diff behaviour ─────────────

    [Fact]
    public void DiffSerialization_RepositoryFieldMatchesBase_FieldAbsentFromDiff()
    {
        // Base has SchemaName = "custom", model also has SchemaName = "custom" → field must NOT appear
        var baseModel = new ConfigurationModel();
        baseModel.Repository.SchemaName = "custom";

        var model = new ConfigurationModel();
        model.Repository.SchemaName = "custom";

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var repoNode = JsonNode.Parse(json)?["RayMigrator"]?["Repository"];

        repoNode.Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_ProductDefaultsFieldMatchesBase_FieldAbsentFromDiff()
    {
        // Base has MigrationErrorAction = "Rollback", model also has "Rollback" → must NOT appear
        var baseModel = new ConfigurationModel();
        baseModel.ProductDefaults.MigrationErrorAction = "Rollback";

        var model = new ConfigurationModel();
        model.ProductDefaults.MigrationErrorAction = "Rollback";

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var pdNode = JsonNode.Parse(json)?["RayMigrator"]?["ProductDefaults"];

        pdNode.Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_ProductDefaultsFieldLevelDiff_OnlyChangedFieldAppears()
    {
        // Base: everything at defaults. Model: only RollbackErrorAction differs.
        var baseModel = new ConfigurationModel();
        var model = new ConfigurationModel();
        model.ProductDefaults.RollbackErrorAction = "Ignore";

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var pdNode = JsonNode.Parse(json)?["RayMigrator"]?["ProductDefaults"];

        pdNode.Should().NotBeNull();
        pdNode!["RollbackErrorAction"]!.GetValue<string>().Should().Be("Ignore");
        pdNode["MigrationErrorAction"].Should().BeNull();
        pdNode["TargetGroupDefaults"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_ProductDefaultsEmptyStringField_AbsentFromDiff()
    {
        // Model has MigrationErrorAction = "" (cleared by PromoteAcrossModels), base has "Rollback".
        // Empty string means "inherit from parent" → must NOT appear in diff.
        var baseModel = new ConfigurationModel();
        baseModel.ProductDefaults.MigrationErrorAction = "Rollback";

        var model = new ConfigurationModel();
        model.ProductDefaults.MigrationErrorAction = "";

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var pdNode = JsonNode.Parse(json)?["RayMigrator"]?["ProductDefaults"];

        // Empty promotion-cleared fields are filtered, so no ProductDefaults section
        pdNode.Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_DatabaseLoggingFieldLevelDiff_OnlyChangedFieldAppears()
    {
        // Base and model share all DatabaseLogging fields except ConnectionString.
        var baseModel = new ConfigurationModel();
        baseModel.DatabaseLogging = new DatabaseLoggingModel
        {
            DatabaseType = "SqlServer",
            ConnectionString = "A",
            SchemaName = "ray",
        };

        var model = new ConfigurationModel();
        model.DatabaseLogging = new DatabaseLoggingModel
        {
            DatabaseType = "SqlServer",
            ConnectionString = "B",
            SchemaName = "ray",
        };

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var dbLogNode = JsonNode.Parse(json)?["RayMigrator"]?["DatabaseLogging"];

        dbLogNode.Should().NotBeNull();
        dbLogNode!["ConnectionString"]!.GetValue<string>().Should().Be("B");
        dbLogNode["DatabaseType"].Should().BeNull();
        dbLogNode["SchemaName"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_SerilogIdentical_SerilogAbsentFromDiff()
    {
        // Both model and base have identical Serilog (Console WriteTo) → section must be absent.
        var baseModel = new ConfigurationModel();
        baseModel.Serilog.WriteTo.Add(new SerilogSinkModel { Name = "Console" });

        var model = new ConfigurationModel();
        model.Serilog.WriteTo.Add(new SerilogSinkModel { Name = "Console" });

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["Serilog"].Should().BeNull();
    }

    [Fact]
    public void DiffSerialization_SerilogMinimumLevelDiffOnly_OnlyMinimumLevelAppears()
    {
        // Base and model both have WriteTo=[Console]; only MinimumLevelDefault differs.
        var baseModel = new ConfigurationModel();
        baseModel.Serilog.WriteTo.Add(new SerilogSinkModel { Name = "Console" });

        var model = new ConfigurationModel();
        model.Serilog.MinimumLevelDefault = "Debug";
        model.Serilog.WriteTo.Add(new SerilogSinkModel { Name = "Console" });

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var serilogNode = JsonNode.Parse(json)?["RayMigrator"]?["Serilog"];

        serilogNode.Should().NotBeNull();
        serilogNode!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");
        serilogNode["WriteTo"].Should().BeNull();
    }

    [Fact]
    public void IsEmptyDiff_EmptyRayMigratorObject_ReturnsTrue()
    {
        var emptyDiff = "{\"RayMigrator\":{}}";
        ConfigurationSerializer.IsEmptyDiff(emptyDiff).Should().BeTrue();
    }

    [Fact]
    public void IsEmptyDiff_NonEmptyDiff_ReturnsFalse()
    {
        var nonEmptyDiff = "{\"RayMigrator\":{\"Repository\":{\"ConnectionString\":\"x\"}}}";
        ConfigurationSerializer.IsEmptyDiff(nonEmptyDiff).Should().BeFalse();
    }

    [Fact]
    public void DiffSerialization_TableBaseNameEmptyInModel_NonDefaultBase_AppearInDiff()
    {
        // Base has TableBaseName = "rm_", model has TableBaseName = "" (a valid value, not a sentinel).
        // Empty string IS a valid value for TableBaseName (not promotion-cleared), so it SHOULD appear.
        var baseModel = new ConfigurationModel();
        baseModel.Repository.TableBaseName = "rm_";

        var model = new ConfigurationModel();
        model.Repository.TableBaseName = "";

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var repoNode = JsonNode.Parse(json)?["RayMigrator"]?["Repository"];

        repoNode.Should().NotBeNull();
        repoNode!["TableBaseName"]!.GetValue<string>().Should().Be("");
    }

    // ══════════════════════════════════════════════════════════════════
    // Category 2: Intra-model Promotion + Serialization
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void PromoteThenSerialize_MigrationErrorAction_AppearsInProductDefaults_AbsentFromProducts()
    {
        var model = CreateModelWithTwoProducts();
        SetProductOverrideString(model.Products[0], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(model.Products[1], p => p.MigrationErrorAction, "Rollback");

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        // Products should not have the override anymore
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            prodNode!["MigrationErrorAction"].Should().BeNull();
        }
    }

    [Fact]
    public void PromoteThenSerialize_RequireRollbackFile_AppearsInProductDefaults_AbsentFromProducts()
    {
        var model = CreateModelWithTwoProducts();
        model.Products[0].RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products[1].RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            prodNode!["RequireRollbackFile"].Should().BeNull();
        }
    }

    [Fact]
    public void PromoteThenSerialize_TargetMigrationOrder_AppearsInTargetGroupDefaults_AbsentFromTargetGroups()
    {
        var model = CreateModelWithTwoProducts();
        model.Products[0].TargetGroups[0].TargetMigrationOrder =
            new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        model.Products[1].TargetGroups[0].TargetMigrationOrder =
            new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["TargetGroupDefaults"]!["TargetMigrationOrder"]!.GetValue<string>()
            .Should().Be("Simultaneously");
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            foreach (var tgNode in prodNode!["TargetGroups"]!.AsArray())
            {
                tgNode!["TargetMigrationOrder"].Should().BeNull();
            }
        }
    }

    [Fact]
    public void PromoteThenSerialize_DbCommandTimeoutInSeconds_AppearsInTargetDefaults_AbsentFromTargets()
    {
        var model = CreateModelWithTwoProducts();
        model.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds =
            new OverridableValue<int> { IsOverridden = true, Value = 90 };
        model.Products[1].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds =
            new OverridableValue<int> { IsOverridden = true, Value = 90 };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["TargetGroupDefaults"]!["TargetDefaults"]!["DbCommandTimeoutInSeconds"]!
            .GetValue<int>().Should().Be(90);
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            foreach (var tgNode in prodNode!["TargetGroups"]!.AsArray())
            {
                foreach (var targetNode in tgNode!["Targets"]!.AsArray())
                {
                    targetNode!["DbCommandTimeoutInSeconds"].Should().BeNull();
                }
            }
        }
    }

    [Fact]
    public void PromoteThenSerialize_AllTwelvePromotableProperties_AllPromotedCorrectly()
    {
        var model = CreateModelWithTwoProducts();

        // Product-level string overrides
        SetProductOverrideString(model.Products[0], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(model.Products[1], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(model.Products[0], p => p.RollbackErrorAction, "Ignore");
        SetProductOverrideString(model.Products[1], p => p.RollbackErrorAction, "Ignore");
        SetProductOverrideString(model.Products[0], p => p.MigrationFilesExtension, "psql");
        SetProductOverrideString(model.Products[1], p => p.MigrationFilesExtension, "psql");
        SetProductOverrideString(model.Products[0], p => p.MigrationRollbackFilesPreExtension, "undo");
        SetProductOverrideString(model.Products[1], p => p.MigrationRollbackFilesPreExtension, "undo");
        SetProductOverrideString(model.Products[0], p => p.MigrationFilesEncoding, "ASCII");
        SetProductOverrideString(model.Products[1], p => p.MigrationFilesEncoding, "ASCII");
        SetProductOverrideString(model.Products[0], p => p.UseCliToolAlias, "sqlcmd");
        SetProductOverrideString(model.Products[1], p => p.UseCliToolAlias, "sqlcmd");

        // Product-level bool override
        model.Products[0].RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products[1].RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };

        // TargetGroup-level string overrides
        model.Products[0].TargetGroups[0].TargetMigrationOrder =
            new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        model.Products[1].TargetGroups[0].TargetMigrationOrder =
            new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        model.Products[0].TargetGroups[0].HashValidationScope =
            new OverridableValue<string> { IsOverridden = true, Value = "Header" };
        model.Products[1].TargetGroups[0].HashValidationScope =
            new OverridableValue<string> { IsOverridden = true, Value = "Header" };

        // Target-level int overrides
        model.Products[0].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds =
            new OverridableValue<int> { IsOverridden = true, Value = 90 };
        model.Products[1].TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds =
            new OverridableValue<int> { IsOverridden = true, Value = 90 };
        model.Products[0].TargetGroups[0].Targets[0].DbCommandMaxRetries =
            new OverridableValue<int> { IsOverridden = true, Value = 5 };
        model.Products[1].TargetGroups[0].Targets[0].DbCommandMaxRetries =
            new OverridableValue<int> { IsOverridden = true, Value = 5 };
        model.Products[0].TargetGroups[0].Targets[0].DbCommandWaitTimeInMsBeforeRetry =
            new OverridableValue<int> { IsOverridden = true, Value = 500 };
        model.Products[1].TargetGroups[0].Targets[0].DbCommandWaitTimeInMsBeforeRetry =
            new OverridableValue<int> { IsOverridden = true, Value = 500 };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        // Verify ProductDefaults
        var pd = ray!["ProductDefaults"];
        pd!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        pd["RollbackErrorAction"]!.GetValue<string>().Should().Be("Ignore");
        pd["MigrationFilesExtension"]!.GetValue<string>().Should().Be("psql");
        pd["MigrationRollbackFilesPreExtension"]!.GetValue<string>().Should().Be("undo");
        pd["MigrationFilesEncoding"]!.GetValue<string>().Should().Be("ASCII");
        pd["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
        // UseCliToolAlias is set via ProductDefaults (non-null → appears)
        pd["UseCliToolAlias"]!.GetValue<string>().Should().Be("sqlcmd");

        // Verify TargetGroupDefaults
        var tgd = pd["TargetGroupDefaults"];
        tgd!["TargetMigrationOrder"]!.GetValue<string>().Should().Be("Simultaneously");
        tgd["HashValidationScope"]!.GetValue<string>().Should().Be("Header");

        // Verify TargetDefaults
        var td = tgd["TargetDefaults"];
        td!["DbCommandTimeoutInSeconds"]!.GetValue<int>().Should().Be(90);
        td["DbCommandMaxRetries"]!.GetValue<int>().Should().Be(5);
        td["DbCommandWaitTimeInMsBeforeRetry"]!.GetValue<int>().Should().Be(500);

        // Verify overrides are absent from Products
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            prodNode!["MigrationErrorAction"].Should().BeNull();
            prodNode["RollbackErrorAction"].Should().BeNull();
            prodNode["MigrationFilesExtension"].Should().BeNull();
            prodNode["MigrationRollbackFilesPreExtension"].Should().BeNull();
            prodNode["MigrationFilesEncoding"].Should().BeNull();
            prodNode["RequireRollbackFile"].Should().BeNull();
            prodNode["UseCliToolAlias"].Should().BeNull();

            foreach (var tgNode in prodNode["TargetGroups"]!.AsArray())
            {
                tgNode!["TargetMigrationOrder"].Should().BeNull();
                tgNode["HashValidationScope"].Should().BeNull();

                foreach (var targetNode in tgNode["Targets"]!.AsArray())
                {
                    targetNode!["DbCommandTimeoutInSeconds"].Should().BeNull();
                    targetNode["DbCommandMaxRetries"].Should().BeNull();
                    targetNode["DbCommandWaitTimeInMsBeforeRetry"].Should().BeNull();
                }
            }
        }
    }

    [Fact]
    public void PromoteThenSerialize_MixedValues_NoPromotion_OverridesRemainOnProducts()
    {
        var model = CreateModelWithTwoProducts();
        SetProductOverrideString(model.Products[0], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(model.Products[1], p => p.MigrationErrorAction, "Terminate");

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        // Products should still have their individual overrides
        ray!["Products"]![0]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        ray["Products"]![1]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Terminate");
    }

    // ══════════════════════════════════════════════════════════════════
    // Category 3: Cross-model Promotion + Diff Serialization
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void PromoteAcrossThenDiffSerialize_RepositoryDatabaseType_PromotedToBase_NotInPeDiff()
    {
        var state = CreateWizardStateWithTwoPeModels();
        state.ProductEnvironmentModels["App1.Docker"].Repository.DatabaseType = "PostgreSQL";
        state.ProductEnvironmentModels["App2.Docker"].Repository.DatabaseType = "PostgreSQL";

        DefaultsPromoter.PromoteAcrossModels(state);

        // Base should have the promoted value
        state.BaseModel.Repository.DatabaseType.Should().Be("PostgreSQL");

        // Serialize base (full) and PE models (diff)
        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseRay = JsonNode.Parse(baseJson)?["RayMigrator"];
        baseRay!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");

        // PE diff: DatabaseType was cleared to "" which differs from default "SqlServer",
        // so Repository section may exist, but the promoted value "PostgreSQL" must NOT appear
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peRay = JsonNode.Parse(peJson)?["RayMigrator"];
            var peDbType = peRay?["Repository"]?["DatabaseType"]?.GetValue<string>();
            peDbType.Should().NotBe("PostgreSQL");
        }
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_RepositoryConnectionString_PromotedToBase()
    {
        var state = CreateWizardStateWithTwoPeModels();
        const string conn = "{ENV:REPO_CONN}";
        state.ProductEnvironmentModels["App1.Docker"].Repository.ConnectionString = conn;
        state.ProductEnvironmentModels["App2.Docker"].Repository.ConnectionString = conn;

        DefaultsPromoter.PromoteAcrossModels(state);

        state.BaseModel.Repository.ConnectionString.Should().Be(conn);

        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseRay = JsonNode.Parse(baseJson)?["RayMigrator"];
        baseRay!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be(conn);

        // PE diff: ConnectionString cleared to "" matches default "", so it should not appear
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peRay = JsonNode.Parse(peJson)?["RayMigrator"];
            var peConn = peRay?["Repository"]?["ConnectionString"]?.GetValue<string>();
            peConn.Should().NotBe(conn);
        }
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_ProductDefaultsMigrationErrorAction_PromotedToBase()
    {
        var state = CreateWizardStateWithTwoPeModels();
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.MigrationErrorAction = "Rollback";

        DefaultsPromoter.PromoteAcrossModels(state);

        state.BaseModel.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");

        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var basePd = JsonNode.Parse(baseJson)?["RayMigrator"]?["ProductDefaults"];
        basePd!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");

        // PE diff: MigrationErrorAction cleared to "" differs from default "Terminate",
        // so ProductDefaults section will be included, but value should not be "Rollback"
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var pePd = JsonNode.Parse(peJson)?["RayMigrator"]?["ProductDefaults"];
            var peValue = pePd?["MigrationErrorAction"]?.GetValue<string>();
            peValue.Should().NotBe("Rollback");
        }
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_SerilogMinimumLevel_PromotedToBase()
    {
        var state = CreateWizardStateWithTwoPeModels();
        state.ProductEnvironmentModels["App1.Docker"].Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App2.Docker"].Serilog.MinimumLevelDefault = "Debug";

        DefaultsPromoter.PromoteAcrossModels(state);

        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Debug");

        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseSerilog = JsonNode.Parse(baseJson)?["RayMigrator"]?["Serilog"];
        baseSerilog!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");

        // PE diff: MinimumLevelDefault cleared to "" differs from default "Information",
        // so Serilog may appear, but value should not be "Debug"
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peSerilog = JsonNode.Parse(peJson)?["RayMigrator"]?["Serilog"];
            var peLevel = peSerilog?["MinimumLevel"]?["Default"]?.GetValue<string>();
            peLevel.Should().NotBe("Debug");
        }
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_PerEnvironmentConnectionString_PromotedToEnvModel()
    {
        var state = new WizardState();
        const string dockerConn = "{ENV:REPO_CONN_DOCKER}";
        const string prodConn = "{ENV:REPO_CONN_PROD}";
        state.ProductEnvironmentModels["App1.Docker"] = CreatePeModelForCrossModel("SqlServer", dockerConn);
        state.ProductEnvironmentModels["App2.Docker"] = CreatePeModelForCrossModel("SqlServer", dockerConn);
        state.ProductEnvironmentModels["App1.Production"] = CreatePeModelForCrossModel("SqlServer", prodConn);
        state.ProductEnvironmentModels["App2.Production"] = CreatePeModelForCrossModel("SqlServer", prodConn);

        DefaultsPromoter.PromoteAcrossModels(state);

        // Per-env promotion should have fired for Docker and Production
        state.EnvironmentModels.Should().ContainKey("Docker");
        state.EnvironmentModels.Should().ContainKey("Production");
        state.EnvironmentModels["Docker"].Repository.ConnectionString.Should().Be(dockerConn);
        state.EnvironmentModels["Production"].Repository.ConnectionString.Should().Be(prodConn);

        // Serialize env models as diff against base
        var dockerEnvJson = ConfigurationSerializer.ToJson(state.EnvironmentModels["Docker"], state.BaseModel);
        var dockerRepo = JsonNode.Parse(dockerEnvJson)?["RayMigrator"]?["Repository"];
        dockerRepo!["ConnectionString"]!.GetValue<string>().Should().Be(dockerConn);
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_AllCrossModelProperties_AllPromotedToBase()
    {
        var state = CreateWizardStateWithTwoPeModels();

        // Repository fields
        state.ProductEnvironmentModels["App1.Docker"].Repository.DatabaseType = "PostgreSQL";
        state.ProductEnvironmentModels["App2.Docker"].Repository.DatabaseType = "PostgreSQL";
        state.ProductEnvironmentModels["App1.Docker"].Repository.SchemaName = "migrations";
        state.ProductEnvironmentModels["App2.Docker"].Repository.SchemaName = "migrations";
        state.ProductEnvironmentModels["App1.Docker"].Repository.ConnectionString = "{ENV:CONN}";
        state.ProductEnvironmentModels["App2.Docker"].Repository.ConnectionString = "{ENV:CONN}";

        // ProductDefaults fields
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.RollbackErrorAction = "Ignore";
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.RollbackErrorAction = "Ignore";
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.MigrationFilesExtension = "psql";
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.MigrationFilesExtension = "psql";
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.MigrationFilesEncoding = "ASCII";
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.MigrationFilesEncoding = "ASCII";
        state.ProductEnvironmentModels["App1.Docker"].ProductDefaults.RequireRollbackFile = false;
        state.ProductEnvironmentModels["App2.Docker"].ProductDefaults.RequireRollbackFile = false;

        // Serilog
        state.ProductEnvironmentModels["App1.Docker"].Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App2.Docker"].Serilog.MinimumLevelDefault = "Debug";

        DefaultsPromoter.PromoteAcrossModels(state);

        // Verify base model
        state.BaseModel.Repository.DatabaseType.Should().Be("PostgreSQL");
        state.BaseModel.Repository.SchemaName.Should().Be("migrations");
        state.BaseModel.Repository.ConnectionString.Should().Be("{ENV:CONN}");
        state.BaseModel.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");
        state.BaseModel.ProductDefaults.RollbackErrorAction.Should().Be("Ignore");
        state.BaseModel.ProductDefaults.MigrationFilesExtension.Should().Be("psql");
        state.BaseModel.ProductDefaults.MigrationFilesEncoding.Should().Be("ASCII");
        state.BaseModel.ProductDefaults.RequireRollbackFile.Should().BeFalse();
        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Debug");

        // Verify base JSON
        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseRay = JsonNode.Parse(baseJson)?["RayMigrator"];
        baseRay!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        baseRay["Repository"]!["SchemaName"]!.GetValue<string>().Should().Be("migrations");
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:CONN}");
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        baseRay["ProductDefaults"]!["MigrationFilesEncoding"]!.GetValue<string>().Should().Be("ASCII");
        baseRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
        baseRay["Serilog"]!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");

        // Verify PE diff JSONs do not contain the promoted values
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

            var peDbType = peRay?["Repository"]?["DatabaseType"]?.GetValue<string>();
            peDbType.Should().NotBe("PostgreSQL");

            var peSchemaName = peRay?["Repository"]?["SchemaName"]?.GetValue<string>();
            peSchemaName.Should().NotBe("migrations");
        }
    }

    [Fact]
    public void PromoteAcrossThenDiffSerialize_MixedValues_NotPromoted_RemainInPeDiff()
    {
        var state = CreateWizardStateWithTwoPeModels();
        state.ProductEnvironmentModels["App1.Docker"].Repository.DatabaseType = "PostgreSQL";
        state.ProductEnvironmentModels["App2.Docker"].Repository.DatabaseType = "MariaDb";

        DefaultsPromoter.PromoteAcrossModels(state);

        // Should not have promoted — base keeps default
        state.BaseModel.Repository.DatabaseType.Should().Be("SqlServer");

        // PE diffs should each retain their own value
        var pe1Json = ConfigurationSerializer.ToJson(
            state.ProductEnvironmentModels["App1.Docker"], state.BaseModel);
        var pe1DbType = JsonNode.Parse(pe1Json)?["RayMigrator"]?["Repository"]?["DatabaseType"]?.GetValue<string>();
        pe1DbType.Should().Be("PostgreSQL");

        var pe2Json = ConfigurationSerializer.ToJson(
            state.ProductEnvironmentModels["App2.Docker"], state.BaseModel);
        var pe2DbType = JsonNode.Parse(pe2Json)?["RayMigrator"]?["Repository"]?["DatabaseType"]?.GetValue<string>();
        pe2DbType.Should().Be("MariaDb");
    }

    // ══════════════════════════════════════════════════════════════════
    // Category 4: Full Pipeline (Promote + PromoteAcrossModels + ToJson)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void FullPipeline_PromoteThenPromoteAcross_AllLevelsCorrect()
    {
        // Set up PE models with products that have overrides
        var state = new WizardState();

        var pe1 = CreatePeModelWithProducts();
        SetProductOverrideString(pe1.Products[0], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(pe1.Products[1], p => p.MigrationErrorAction, "Rollback");
        pe1.Repository.DatabaseType = "PostgreSQL";
        pe1.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App1.Docker"] = pe1;

        var pe2 = CreatePeModelWithProducts();
        SetProductOverrideString(pe2.Products[0], p => p.MigrationErrorAction, "Rollback");
        SetProductOverrideString(pe2.Products[1], p => p.MigrationErrorAction, "Rollback");
        pe2.Repository.DatabaseType = "PostgreSQL";
        pe2.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App2.Docker"] = pe2;

        // Step 1: Promote within each PE model (product-level overrides → ProductDefaults)
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            DefaultsPromoter.Promote(pe);
        }

        // Verify intra-model promotion worked
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            pe.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");
            foreach (var prod in pe.Products)
            {
                prod.MigrationErrorAction.IsOverridden.Should().BeFalse();
            }
        }

        // Step 2: Promote across models (PE → BaseModel)
        DefaultsPromoter.PromoteAcrossModels(state);

        // Verify cross-model promotion worked
        state.BaseModel.Repository.DatabaseType.Should().Be("PostgreSQL");
        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Debug");
        // MigrationErrorAction was promoted from PE ProductDefaults to Base ProductDefaults
        state.BaseModel.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");

        // Step 3: Serialize base (full) + PE (diff)
        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseRay = JsonNode.Parse(baseJson)?["RayMigrator"];
        baseRay!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        baseRay["Serilog"]!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");

        // PE diffs should not contain the promoted values
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

            var peDbType = peRay?["Repository"]?["DatabaseType"]?.GetValue<string>();
            peDbType.Should().NotBe("PostgreSQL");
        }
    }

    [Fact]
    public void FullPipeline_TwoProductsTwoEnvironments_BaseAndEnvLevelSerialization()
    {
        var state = new WizardState();

        // Docker environment: both products share the same connection string
        var pe1Docker = CreatePeModelForCrossModel("PostgreSQL", "{ENV:CONN_DOCKER}");
        pe1Docker.ProductDefaults.MigrationErrorAction = "Rollback";
        pe1Docker.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App1.Docker"] = pe1Docker;

        var pe2Docker = CreatePeModelForCrossModel("PostgreSQL", "{ENV:CONN_DOCKER}");
        pe2Docker.ProductDefaults.MigrationErrorAction = "Rollback";
        pe2Docker.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App2.Docker"] = pe2Docker;

        // Production environment: both products share a different connection string
        var pe1Prod = CreatePeModelForCrossModel("PostgreSQL", "{ENV:CONN_PROD}");
        pe1Prod.ProductDefaults.MigrationErrorAction = "Rollback";
        pe1Prod.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App1.Production"] = pe1Prod;

        var pe2Prod = CreatePeModelForCrossModel("PostgreSQL", "{ENV:CONN_PROD}");
        pe2Prod.ProductDefaults.MigrationErrorAction = "Rollback";
        pe2Prod.Serilog.MinimumLevelDefault = "Debug";
        state.ProductEnvironmentModels["App2.Production"] = pe2Prod;

        // Promote across models
        DefaultsPromoter.PromoteAcrossModels(state);

        // Values common to ALL 4 PEs should be in base
        state.BaseModel.Repository.DatabaseType.Should().Be("PostgreSQL");
        state.BaseModel.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");
        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Debug");

        // Connection strings differ across envs, so NOT promoted to base
        state.BaseModel.Repository.ConnectionString.Should().NotBe("{ENV:CONN_DOCKER}");
        state.BaseModel.Repository.ConnectionString.Should().NotBe("{ENV:CONN_PROD}");

        // Connection strings should be promoted per-environment
        state.EnvironmentModels.Should().ContainKey("Docker");
        state.EnvironmentModels.Should().ContainKey("Production");
        state.EnvironmentModels["Docker"].Repository.ConnectionString.Should().Be("{ENV:CONN_DOCKER}");
        state.EnvironmentModels["Production"].Repository.ConnectionString.Should().Be("{ENV:CONN_PROD}");

        // Serialize and verify base
        var baseJson = ConfigurationSerializer.ToJson(state.BaseModel);
        var baseRay = JsonNode.Parse(baseJson)?["RayMigrator"];
        baseRay!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        baseRay["Serilog"]!["MinimumLevel"]!["Default"]!.GetValue<string>().Should().Be("Debug");

        // Serialize and verify env models (diff against base)
        var dockerEnvJson = ConfigurationSerializer.ToJson(state.EnvironmentModels["Docker"], state.BaseModel);
        var dockerEnvRay = JsonNode.Parse(dockerEnvJson)?["RayMigrator"];
        dockerEnvRay!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:CONN_DOCKER}");

        var prodEnvJson = ConfigurationSerializer.ToJson(state.EnvironmentModels["Production"], state.BaseModel);
        var prodEnvRay = JsonNode.Parse(prodEnvJson)?["RayMigrator"];
        prodEnvRay!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:CONN_PROD}");

        // PE diffs should not contain promoted connection strings
        foreach (var pe in state.ProductEnvironmentModels.Values)
        {
            var peJson = ConfigurationSerializer.ToJson(pe, state.BaseModel);
            var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

            var peConn = peRay?["Repository"]?["ConnectionString"]?.GetValue<string>();
            peConn.Should().NotBe("{ENV:CONN_DOCKER}");
            peConn.Should().NotBe("{ENV:CONN_PROD}");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Category 5: StopRollbackOnMissingRollbackFile promotion + serialization
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void DiffSerialization_StopRollbackOnMissingRollbackFile_ProductDefaultsFalse_IncludesSection()
    {
        var model = new ConfigurationModel();
        model.ProductDefaults.StopRollbackOnMissingRollbackFile = false;
        var baseModel = new ConfigurationModel();

        var json = ConfigurationSerializer.ToJson(model, baseModel);
        var pdNode = JsonNode.Parse(json)?["RayMigrator"]?["ProductDefaults"];

        pdNode.Should().NotBeNull();
        pdNode!["StopRollbackOnMissingRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void PromoteThenSerialize_StopRollbackOnMissingRollbackFile_AppearsInProductDefaults_AbsentFromProducts()
    {
        var model = CreateModelWithTwoProducts();
        model.Products[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products[1].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["StopRollbackOnMissingRollbackFile"]!.GetValue<bool>().Should().BeTrue();
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            prodNode!["StopRollbackOnMissingRollbackFile"].Should().BeNull();
        }
    }

    [Fact]
    public void PromoteThenSerialize_StopRollbackOnMissingRollbackFile_TargetGroup_AppearsInTargetGroupDefaults()
    {
        var model = CreateModelWithTwoProducts();
        model.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products[1].TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };

        DefaultsPromoter.Promote(model);
        var json = ConfigurationSerializer.ToJson(model);
        var ray = JsonNode.Parse(json)?["RayMigrator"];

        ray!["ProductDefaults"]!["TargetGroupDefaults"]!["StopRollbackOnMissingRollbackFile"]!
            .GetValue<bool>().Should().BeTrue();
        foreach (var prodNode in ray["Products"]!.AsArray())
        {
            foreach (var tgNode in prodNode!["TargetGroups"]!.AsArray())
            {
                tgNode!["StopRollbackOnMissingRollbackFile"].Should().BeNull();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private static ConfigurationModel CreateModelWithTwoProducts()
    {
        var model = new ConfigurationModel();
        model.Products.Add(TestModelFactory.CreateValidProduct("App1"));
        model.Products.Add(TestModelFactory.CreateValidProduct("App2"));
        return model;
    }

    private static void SetProductOverrideString(
        ProductModel product,
        Func<ProductModel, OverridableValue<string>> getOverride,
        string value)
    {
        var ov = getOverride(product);
        ov.IsOverridden = true;
        ov.Value = value;
    }

    private static WizardState CreateWizardStateWithTwoPeModels()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["App1.Docker"] = CreatePeModelForCrossModel("SqlServer", "");
        state.ProductEnvironmentModels["App2.Docker"] = CreatePeModelForCrossModel("SqlServer", "");
        return state;
    }

    private static ConfigurationModel CreatePeModelForCrossModel(string dbType, string connectionString)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel
            {
                DatabaseType = dbType,
                ConnectionString = connectionString,
            },
            ProductDefaults = new ProductDefaultsModel(),
            Serilog = new SerilogModel(),
        };
    }

    private static ConfigurationModel CreatePeModelWithProducts()
    {
        var model = new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel(),
            ProductDefaults = new ProductDefaultsModel(),
            Serilog = new SerilogModel(),
        };
        model.Products.Add(TestModelFactory.CreateValidProduct("ProductA"));
        model.Products.Add(TestModelFactory.CreateValidProduct("ProductB"));
        return model;
    }
}
