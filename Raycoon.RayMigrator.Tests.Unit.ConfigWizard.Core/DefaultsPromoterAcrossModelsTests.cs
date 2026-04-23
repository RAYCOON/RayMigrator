// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Tests for DefaultsPromoter.PromoteAcrossModels() — cross-model promotion from
/// ProductEnvironmentModels to BaseModel and EnvironmentModels.
/// </summary>
public class DefaultsPromoterAcrossModelsTests
{
    // ── Guard conditions ──────────────────────────────────────────

    [Fact]
    public void PromoteAcrossModels_LessThanTwoCombinations_ReturnsEmpty()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("SqlServer", "ray");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().BeEmpty();
    }

    [Fact]
    public void PromoteAcrossModels_NoCombinations_ReturnsEmpty()
    {
        var state = new WizardState();

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().BeEmpty();
    }

    // ── Repository.DatabaseType promotion ─────────────────────────
    // Note: BaseModel.Repository.DatabaseType defaults to "SqlServer", SchemaName defaults to "ray".
    // TryPromoteAcrossAll skips when the target already has the same value.
    // Use a non-default value (e.g., "PostgreSQL") to trigger promotion.

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultDatabaseType_PromotesToBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("PostgreSQL", "ray");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r => r.PropertyName == "Repository.DatabaseType" && r.PromotedValue == "PostgreSQL");
        state.BaseModel.Repository.DatabaseType.Should().Be("PostgreSQL");
    }

    [Fact]
    public void PromoteAcrossModels_DifferentDatabaseTypes_DoesNotPromote()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("MariaDb", "ray");
        // Pre-clear the base model so we only verify that promotion doesn't fire
        state.BaseModel.Repository.DatabaseType = "";

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().NotContain(r => r.PropertyName == "Repository.DatabaseType");
    }

    [Fact]
    public void PromoteAcrossModels_PromotedDatabaseType_LevelIsBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("PostgreSQL", "ray");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        var promotion = results.FirstOrDefault(r => r.PropertyName == "Repository.DatabaseType");
        promotion.Should().NotBeNull();
        promotion!.Level.Should().Be("BaseModel");
    }

    [Fact]
    public void PromoteAcrossModels_PromotedDatabaseType_AffectedProductsCount()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("PostgreSQL", "ray");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        var promotion = results.FirstOrDefault(r => r.PropertyName == "Repository.DatabaseType");
        promotion.Should().NotBeNull();
        promotion!.AffectedProducts.Should().Be(2);
    }

    [Fact]
    public void PromoteAcrossModels_PromotedDatabaseType_ClearsFromSourceModels()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("PostgreSQL", "ray");

        DefaultsPromoter.PromoteAcrossModels(state);

        foreach (var model in state.ProductEnvironmentModels.Values)
        {
            model.Repository.DatabaseType.Should().BeNullOrEmpty();
        }
    }

    // ── Repository.SchemaName promotion ──────────────────────────
    // "ray" is already the default in BaseModel, so use a non-default schema name.

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultSchemaName_PromotesToBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("SqlServer", "migrations");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("SqlServer", "migrations");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r => r.PropertyName == "Repository.SchemaName");
        state.BaseModel.Repository.SchemaName.Should().Be("migrations");
    }

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameDefaultSchemaName_AlreadyPromoted_NoResult()
    {
        // "ray" is the default — BaseModel already has "ray" — promotion skipped
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("SqlServer", "ray");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("SqlServer", "ray");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().NotContain(r => r.PropertyName == "Repository.SchemaName");
    }

    // ── ProductDefaults promotion ─────────────────────────────────
    // "Terminate" is the default for MigrationErrorAction — use a non-default value.

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultMigrationErrorAction_PromotesToBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithProductDefaults("Rollback");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithProductDefaults("Rollback");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r => r.PropertyName == "ProductDefaults.MigrationErrorAction");
        state.BaseModel.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");
    }

    [Fact]
    public void PromoteAcrossModels_DifferentMigrationErrorActions_DoesNotPromote()
    {
        var state = new WizardState();
        // Pre-clear BaseModel to a neutral value so either option would constitute a change
        state.BaseModel.ProductDefaults.MigrationErrorAction = "";
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithProductDefaults("Rollback");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithProductDefaults("Terminate");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().NotContain(r => r.PropertyName == "ProductDefaults.MigrationErrorAction");
    }

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultFilesExtension_PromotesToBaseModel()
    {
        var state = new WizardState();
        // "sql" is the default; use a non-default value
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithFilesExtension("psql");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithFilesExtension("psql");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r => r.PropertyName == "ProductDefaults.MigrationFilesExtension");
        state.BaseModel.ProductDefaults.MigrationFilesExtension.Should().Be("psql");
    }

    // ── Serilog.MinimumLevelDefault promotion ─────────────────────
    // "Information" is the default — use a non-default value.

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultSerilogLevel_PromotesToBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithSerilog("Debug");
        state.ProductEnvironmentModels["ProductA.Production"] = CreatePeModelWithSerilog("Debug");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r => r.PropertyName == "Serilog.MinimumLevelDefault");
        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Debug");
    }

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameDefaultSerilogLevel_AlreadyPromoted_NoResult()
    {
        // "Information" is the default — already in BaseModel, promotion skipped
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithSerilog("Information");
        state.ProductEnvironmentModels["ProductA.Production"] = CreatePeModelWithSerilog("Information");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().NotContain(r => r.PropertyName == "Serilog.MinimumLevelDefault");
    }

    // ── Per-environment connection string promotion ───────────────

    [Fact]
    public void PromoteAcrossModels_TwoCombinationsSameEnv_SameConnectionString_PromotesToEnvModel()
    {
        // To test per-env promotion: two products share the same env connection string,
        // but different envs have different strings (so cross-all promotion does not fire first).
        var state = new WizardState();
        const string dockerConn = "{ENV:REPO_CONN_DOCKER}";
        const string prodConn = "{ENV:REPO_CONN_PRODUCTION}";
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithConnectionString(dockerConn);
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithConnectionString(dockerConn);
        state.ProductEnvironmentModels["ProductA.Production"] = CreatePeModelWithConnectionString(prodConn);
        state.ProductEnvironmentModels["ProductB.Production"] = CreatePeModelWithConnectionString(prodConn);

        DefaultsPromoter.PromoteAcrossModels(state);

        // Cross-all promotion should not fire (different conn strings across envs)
        state.BaseModel.Repository.ConnectionString.Should().NotBe(dockerConn);
        // Per-env model for Docker should exist
        state.EnvironmentModels.Should().ContainKey("Docker");
        state.EnvironmentModels.Should().ContainKey("Production");
    }

    [Fact]
    public void PromoteAcrossModels_TwoCombinationsSameEnv_DifferentConnectionStrings_DoesNotPromoteToEnvModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithConnectionString("{ENV:CONN_A}");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithConnectionString("{ENV:CONN_B}");

        DefaultsPromoter.PromoteAcrossModels(state);

        // If an env model was created, it should not have either of the differing values promoted
        if (state.EnvironmentModels.TryGetValue("Docker", out var envModel))
        {
            envModel.Repository.ConnectionString.Should().NotBe("{ENV:CONN_A}");
            envModel.Repository.ConnectionString.Should().NotBe("{ENV:CONN_B}");
        }
    }

    [Fact]
    public void PromoteAcrossModels_SingleCombinationPerEnv_NoPerEnvConnectionStringPromotion()
    {
        // Only one combination per environment — cannot promote per-env (needs ≥2 per env)
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithConnectionString("{ENV:CONN_DOCKER}");
        state.ProductEnvironmentModels["ProductA.Production"] = CreatePeModelWithConnectionString("{ENV:CONN_PROD}");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        // No per-env promotion because only one product per environment
        results.Should().NotContain(r =>
            r.PropertyName == "Repository.ConnectionString (Docker)" ||
            r.PropertyName == "Repository.ConnectionString (Production)");
    }

    [Fact]
    public void PromoteAcrossModels_CreatesEnvironmentModelIfNotExists_WhenPerEnvPromotionFires()
    {
        var state = new WizardState();
        var connString = "{ENV:REPO_CONN_STAGING}";
        state.ProductEnvironmentModels["ProductA.Staging"] = CreatePeModelWithConnectionString(connString);
        state.ProductEnvironmentModels["ProductB.Staging"] = CreatePeModelWithConnectionString(connString);

        DefaultsPromoter.PromoteAcrossModels(state);

        state.EnvironmentModels.Should().ContainKey("Staging");
    }

    [Fact]
    public void PromoteAcrossModels_ExistingEnvironmentModel_IsReused()
    {
        var state = new WizardState();
        var existingEnvModel = new ConfigurationModel
        {
            FilePath = "appsettings.Docker.json",
            FileRole = ConfigFileRole.Environment,
            Repository = new RepositoryModel(),
        };
        state.EnvironmentModels["Docker"] = existingEnvModel;

        var connString = "{ENV:REPO_CONN_DOCKER}";
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithConnectionString(connString);
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithConnectionString(connString);

        DefaultsPromoter.PromoteAcrossModels(state);

        state.EnvironmentModels["Docker"].Should().BeSameAs(existingEnvModel);
    }

    // ── ProductDefaults.StopRollbackOnMissingRollbackFile promotion ──

    [Fact]
    public void PromoteAcrossModels_AllCombinationsSameNonDefaultStopRollback_PromotesToBaseModel()
    {
        var state = new WizardState();
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithStopRollback(false);
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithStopRollback(false);

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().Contain(r =>
            r.PropertyName == "ProductDefaults.StopRollbackOnMissingRollbackFile");
        state.BaseModel.ProductDefaults.StopRollbackOnMissingRollbackFile.Should().BeFalse();
    }

    [Fact]
    public void PromoteAcrossModels_DifferentStopRollbackValues_DoesNotPromote()
    {
        var state = new WizardState();
        state.BaseModel.ProductDefaults.StopRollbackOnMissingRollbackFile = true;
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModelWithStopRollback(true);
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModelWithStopRollback(false);

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        results.Should().NotContain(r =>
            r.PropertyName == "ProductDefaults.StopRollbackOnMissingRollbackFile");
    }

    // ── Combined promotion result ─────────────────────────────────

    [Fact]
    public void PromoteAcrossModels_MultiplePromotableFields_ReturnsMultipleResults()
    {
        var state = new WizardState();
        // Use non-default values that differ from BaseModel defaults
        state.ProductEnvironmentModels["ProductA.Docker"] = CreatePeModel("PostgreSQL", "migrations");
        state.ProductEnvironmentModels["ProductB.Docker"] = CreatePeModel("PostgreSQL", "migrations");

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        // Both DatabaseType and SchemaName should be promoted (both are non-default)
        results.Should().Contain(r => r.PropertyName == "Repository.DatabaseType");
        results.Should().Contain(r => r.PropertyName == "Repository.SchemaName");
    }

    // ── ReconcileBaseProducts: backfill empty ConnectionString ─────

    [Fact]
    public void PromoteAcrossModels_ReconcileBaseProducts_FillsEmptyTargetConnectionString()
    {
        var state = new WizardState();
        state.BaseModel.Products.Add(new ProductModel
        {
            Alias = "App2",
            MigrationFilesRootDirectory = "./Migrations/App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "" }
                    }
                }
            }
        });

        var pe1 = CreatePeModel("SqlServer", "ray");
        pe1.Products.Add(new ProductModel
        {
            Alias = "App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "{ENV:APP2_BACKEND_MAINDB_CONNECTION_STRING_DOCKER}" }
                    }
                }
            }
        });

        var pe2 = CreatePeModel("SqlServer", "ray");
        pe2.Products.Add(new ProductModel
        {
            Alias = "App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "{ENV:APP2_BACKEND_MAINDB_CONNECTION_STRING_PROD}" }
                    }
                }
            }
        });

        state.ProductEnvironmentModels["App2.Docker"] = pe1;
        state.ProductEnvironmentModels["App2.Production"] = pe2;

        var results = DefaultsPromoter.PromoteAcrossModels(state);

        var baseTarget = state.BaseModel.Products
            .Single(p => p.Alias == "App2")
            .TargetGroups.Single(tg => tg.Alias == "Backend")
            .Targets.Single(t => t.Alias == "MainDB");

        baseTarget.ConnectionString.Should().Be("{ENV:APP2_BACKEND_MAINDB_CONNECTION_STRING}");
        results.Should().Contain(r =>
            r.PropertyName.Contains("MainDB") &&
            r.PropertyName.Contains("ConnectionString"));
    }

    [Fact]
    public void PromoteAcrossModels_ReconcileBaseProducts_LeavesNonEmptyConnectionStringUnchanged()
    {
        var state = new WizardState();
        state.BaseModel.Products.Add(new ProductModel
        {
            Alias = "App2",
            MigrationFilesRootDirectory = "./Migrations/App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "{ENV:EXISTING_CONNECTION}" }
                    }
                }
            }
        });

        var pe1 = CreatePeModel("SqlServer", "ray");
        pe1.Products.Add(new ProductModel
        {
            Alias = "App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "{ENV:APP2_CONN_DOCKER}" }
                    }
                }
            }
        });

        var pe2 = CreatePeModel("SqlServer", "ray");
        pe2.Products.Add(new ProductModel
        {
            Alias = "App2",
            TargetGroups = new List<TargetGroupModel>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetModel>
                    {
                        new() { Alias = "MainDB", ConnectionString = "{ENV:APP2_CONN_PROD}" }
                    }
                }
            }
        });

        state.ProductEnvironmentModels["App2.Docker"] = pe1;
        state.ProductEnvironmentModels["App2.Production"] = pe2;

        DefaultsPromoter.PromoteAcrossModels(state);

        var baseTarget = state.BaseModel.Products
            .Single(p => p.Alias == "App2")
            .TargetGroups.Single(tg => tg.Alias == "Backend")
            .Targets.Single(t => t.Alias == "MainDB");

        baseTarget.ConnectionString.Should().Be("{ENV:EXISTING_CONNECTION}");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static ConfigurationModel CreatePeModel(string dbType, string schemaName)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel
            {
                DatabaseType = dbType,
                SchemaName = schemaName,
                ConnectionString = "{ENV:REPO_CONN}",
            },
            ProductDefaults = new ProductDefaultsModel
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesExtension = "sql",
            },
            Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        };
    }

    private static ConfigurationModel CreatePeModelWithProductDefaults(string migrationErrorAction)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel { DatabaseType = "SqlServer", SchemaName = "ray" },
            ProductDefaults = new ProductDefaultsModel { MigrationErrorAction = migrationErrorAction },
            Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        };
    }

    private static ConfigurationModel CreatePeModelWithFilesExtension(string extension)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel { DatabaseType = "SqlServer" },
            ProductDefaults = new ProductDefaultsModel { MigrationFilesExtension = extension },
            Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        };
    }

    private static ConfigurationModel CreatePeModelWithSerilog(string minimumLevel)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel { DatabaseType = "SqlServer" },
            ProductDefaults = new ProductDefaultsModel(),
            Serilog = new SerilogModel { MinimumLevelDefault = minimumLevel },
        };
    }

    private static ConfigurationModel CreatePeModelWithConnectionString(string connectionString)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel
            {
                DatabaseType = "SqlServer",
                ConnectionString = connectionString,
            },
            ProductDefaults = new ProductDefaultsModel(),
            Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        };
    }

    private static ConfigurationModel CreatePeModelWithStopRollback(bool stopRollback)
    {
        return new ConfigurationModel
        {
            FileRole = ConfigFileRole.ProductEnvironment,
            Repository = new RepositoryModel { DatabaseType = "SqlServer", SchemaName = "ray" },
            ProductDefaults = new ProductDefaultsModel { StopRollbackOnMissingRollbackFile = stopRollback },
            Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        };
    }
}
