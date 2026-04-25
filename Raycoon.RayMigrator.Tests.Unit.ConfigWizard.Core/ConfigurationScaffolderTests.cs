
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class ConfigurationScaffolderTests
{
    [Fact]
    public void Scaffold_ValidAnswers_ProducesValidState()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        var state = ConfigurationScaffolder.Scaffold(answers);

        state.BaseModel.Should().NotBeNull();
        state.BaseModel.Repository.DatabaseType.Should().Be("SqlServer");
        state.BaseModel.Products.Should().HaveCount(1);
        state.BaseModel.Products[0].Alias.Should().Be("MyApp");
        state.SetupAnswers.Should().BeSameAs(answers);
    }

    [Fact]
    public void Scaffold_WithDatabaseLogging_CreatesDatabaseLogging()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        answers.UseDatabaseLogging = true;

        var state = ConfigurationScaffolder.Scaffold(answers);
        state.BaseModel.DatabaseLogging.Should().NotBeNull();
        state.BaseModel.DatabaseLogging!.DatabaseType.Should().Be("SqlServer");
    }

    [Fact]
    public void Scaffold_WithoutDatabaseLogging_NoDatabaseLogging()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        answers.UseDatabaseLogging = false;

        var state = ConfigurationScaffolder.Scaffold(answers);
        state.BaseModel.DatabaseLogging.Should().BeNull();
    }

    [Fact]
    public void Scaffold_WithEnvironments_CreatesEnvironmentModels()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        var state = ConfigurationScaffolder.Scaffold(answers);

        state.EnvironmentModels.Should().ContainKey("Development");
        state.EnvironmentModels.Should().ContainKey("Docker");
    }

    [Fact]
    public void Scaffold_WithEnvironments_CreatesProductEnvironmentModels()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        var state = ConfigurationScaffolder.Scaffold(answers);

        state.ProductEnvironmentModels.Should().ContainKey("MyApp.Development");
        state.ProductEnvironmentModels.Should().ContainKey("MyApp.Docker");
    }

    [Fact]
    public void Scaffold_WithCliTools_AddsCliToolsAndSetsUseCliToolAlias()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        answers.UseCliTools = true;

        var state = ConfigurationScaffolder.Scaffold(answers);

        state.BaseModel.CliTools.Should().HaveCount(1);
        state.BaseModel.CliTools[0].Alias.Should().Be("sqlcmd");
        state.BaseModel.ProductDefaults.UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void Scaffold_WithCliTools_MultipleDbTypes_NoDefaultUseCliToolAlias()
    {
        var answers = new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            UseCliTools = true,
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyApp",
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new() { Alias = "Backend", DatabaseType = "SqlServer", TargetAliases = new List<string> { "MainDB" } },
                        new() { Alias = "Frontend", DatabaseType = "PostgreSQL", TargetAliases = new List<string> { "FrontDB" } }
                    }
                }
            }
        };

        var state = ConfigurationScaffolder.Scaffold(answers);

        state.BaseModel.CliTools.Should().HaveCount(2);
        state.BaseModel.ProductDefaults.UseCliToolAlias.Should().BeNull();
    }

    [Fact]
    public void Scaffold_EmptyProducts_ProducesEmptyModel()
    {
        var answers = new WizardSetupAnswers { Products = new List<ProductSetup>() };
        var state = ConfigurationScaffolder.Scaffold(answers);

        state.BaseModel.Products.Should().BeEmpty();
        state.EnvironmentModels.Should().BeEmpty();
    }

    [Fact]
    public void Scaffold_NoTargetAliases_CreatesDefaultMainDB()
    {
        var answers = new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyApp",
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new() { Alias = "Backend", DatabaseType = "SqlServer", TargetAliases = new List<string>() }
                    }
                }
            }
        };

        var state = ConfigurationScaffolder.Scaffold(answers);
        state.BaseModel.Products[0].TargetGroups[0].Targets.Should().HaveCount(1);
        state.BaseModel.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("MainDB");
    }

    [Fact]
    public void Scaffold_SerilogIsConfigured()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        var state = ConfigurationScaffolder.Scaffold(answers);

        state.BaseModel.Serilog.MinimumLevelDefault.Should().Be("Information");
        state.BaseModel.Serilog.WriteTo.Should().ContainSingle().Which.Name.Should().Be("Console");
    }

    [Fact]
    public void Scaffold_SqliteRepository_EmptySchemaName()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        answers.RepositoryDatabaseType = "Sqlite";

        var state = ConfigurationScaffolder.Scaffold(answers);
        state.BaseModel.Repository.SchemaName.Should().BeEmpty();
    }

    [Fact]
    public void Scaffold_ResultPassesValidation()
    {
        var answers = TestModelFactory.CreateValidSetupAnswers();
        var state = ConfigurationScaffolder.Scaffold(answers);
        var validationResult = ConfigurationValidator.ValidateAll(state.BaseModel);

        // Should not have any errors (warnings about ENV placeholders are OK)
        validationResult.Errors.Should().BeEmpty();
    }

    // ── ScaffoldCombination ───────────────────────────────────────

    [Fact]
    public void ScaffoldCombination_FileRoleIsProductEnvironment()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.FileRole.Should().Be(ConfigFileRole.ProductEnvironment);
    }

    [Fact]
    public void ScaffoldCombination_FilePathMatchesPattern()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.FilePath.Should().Be("appsettings.ProductA.Docker.json");
    }

    [Fact]
    public void ScaffoldCombination_HasOneProductWithMatchingAlias()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Products.Should().ContainSingle();
        model.Products[0].Alias.Should().Be("ProductA");
    }

    [Fact]
    public void ScaffoldCombination_HasDefaultRepository_SqlServer()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Repository.Should().NotBeNull();
        model.Repository.DatabaseType.Should().Be("SqlServer");
    }

    [Fact]
    public void ScaffoldCombination_HasDefaultSchemaName_Ray()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Repository.SchemaName.Should().Be("ray");
    }

    [Fact]
    public void ScaffoldCombination_HasProductDefaults()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.ProductDefaults.Should().NotBeNull();
        model.ProductDefaults.MigrationErrorAction.Should().Be("Terminate");
        model.ProductDefaults.MigrationFilesExtension.Should().Be("sql");
    }

    [Fact]
    public void ScaffoldCombination_HasSerilog()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Serilog.Should().NotBeNull();
        model.Serilog.MinimumLevelDefault.Should().Be("Information");
        model.Serilog.WriteTo.Should().ContainSingle().Which.Name.Should().Be("Console");
    }

    [Fact]
    public void ScaffoldCombination_HasOneTargetGroupWithOneTarget()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Products[0].TargetGroups.Should().ContainSingle();
        model.Products[0].TargetGroups[0].Targets.Should().ContainSingle();
    }

    [Fact]
    public void ScaffoldCombination_TargetGroupAlias_IsBackend()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Products[0].TargetGroups[0].Alias.Should().Be("Backend");
    }

    [Fact]
    public void ScaffoldCombination_TargetAlias_IsMainDB()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("ProductA", "Docker");

        model.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("MainDB");
    }

    [Fact]
    public void ScaffoldCombination_DifferentProductAndEnvironment_ProducesCorrectFilePath()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("MyShop", "Production");

        model.FilePath.Should().Be("appsettings.MyShop.Production.json");
        model.Products[0].Alias.Should().Be("MyShop");
    }

    // ── ScaffoldCombination with base model ──────────────────────

    [Fact]
    public void ScaffoldCombination_WithBaseModel_CopiesTargetAlias()
    {
        var baseModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "MyApp", MigrationFilesRootDirectory = "./Migrations/MyApp" };
        var tg = new TargetGroupModel { Alias = "Backend", DatabaseType = "SqlServer" };
        tg.Targets.Add(new TargetModel { Alias = "BackendDB", ConnectionString = "{ENV:CONN}" });
        product.TargetGroups.Add(tg);
        baseModel.Products.Add(product);

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Docker", baseModel);

        model.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("BackendDB",
            "should copy target alias from base model");
    }

    [Fact]
    public void ScaffoldCombination_WithBaseModel_GeneratesEnvSpecificConnectionString()
    {
        var baseModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "MyApp", MigrationFilesRootDirectory = "./Migrations/MyApp" };
        var tg = new TargetGroupModel { Alias = "Backend", DatabaseType = "SqlServer" };
        tg.Targets.Add(new TargetModel { Alias = "BackendDB", ConnectionString = "{ENV:CONN}" });
        product.TargetGroups.Add(tg);
        baseModel.Products.Add(product);

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Docker", baseModel);

        model.Products[0].TargetGroups[0].Targets[0].ConnectionString
            .Should().Be("{ENV:MYAPP_BACKEND_BACKENDDB_CONNECTION_STRING_DOCKER}");
    }

    [Fact]
    public void ScaffoldCombination_WithBaseModel_CopiesMultipleTargetGroups()
    {
        var baseModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "MyApp" };
        var tg1 = new TargetGroupModel { Alias = "Backend", DatabaseType = "SqlServer" };
        tg1.Targets.Add(new TargetModel { Alias = "MainDB", ConnectionString = "{ENV:C1}" });
        var tg2 = new TargetGroupModel { Alias = "Frontend", DatabaseType = "PostgreSQL" };
        tg2.Targets.Add(new TargetModel { Alias = "FrontDB", ConnectionString = "{ENV:C2}" });
        product.TargetGroups.Add(tg1);
        product.TargetGroups.Add(tg2);
        baseModel.Products.Add(product);

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Prod", baseModel);

        model.Products[0].TargetGroups.Should().HaveCount(2);
        model.Products[0].TargetGroups[0].Alias.Should().Be("Backend");
        model.Products[0].TargetGroups[0].DatabaseType.Should().Be("SqlServer");
        model.Products[0].TargetGroups[1].Alias.Should().Be("Frontend");
        model.Products[0].TargetGroups[1].DatabaseType.Should().Be("PostgreSQL");
    }

    [Fact]
    public void ScaffoldCombination_WithBaseModel_CopiesMultipleTargets()
    {
        var baseModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "MyApp" };
        var tg = new TargetGroupModel { Alias = "Backend", DatabaseType = "SqlServer" };
        tg.Targets.Add(new TargetModel { Alias = "MainDB", ConnectionString = "{ENV:C1}" });
        tg.Targets.Add(new TargetModel { Alias = "ArchiveDB", ConnectionString = "{ENV:C2}" });
        product.TargetGroups.Add(tg);
        baseModel.Products.Add(product);

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Dev", baseModel);

        model.Products[0].TargetGroups[0].Targets.Should().HaveCount(2);
        model.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("MainDB");
        model.Products[0].TargetGroups[0].Targets[1].Alias.Should().Be("ArchiveDB");
        model.Products[0].TargetGroups[0].Targets[1].ConnectionString
            .Should().Be("{ENV:MYAPP_BACKEND_ARCHIVEDB_CONNECTION_STRING_DEV}");
    }

    [Fact]
    public void ScaffoldCombination_WithBaseModel_CopiesRepositoryDatabaseType()
    {
        var baseModel = new ConfigurationModel();
        baseModel.Repository.DatabaseType = "PostgreSQL";
        baseModel.Repository.SchemaName = "custom";

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Docker", baseModel);

        model.Repository.DatabaseType.Should().Be("PostgreSQL");
        model.Repository.SchemaName.Should().Be("custom");
    }

    [Fact]
    public void ScaffoldCombination_WithoutBaseModel_FallsBackToDefaults()
    {
        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Docker");

        model.Products[0].TargetGroups[0].Alias.Should().Be("Backend");
        model.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("MainDB");
        model.Repository.DatabaseType.Should().Be("SqlServer");
    }

    [Fact]
    public void ScaffoldCombination_WithBaseModel_NoMatchingProduct_FallsBackToDefaults()
    {
        var baseModel = new ConfigurationModel();
        var product = new ProductModel { Alias = "OtherApp" };
        baseModel.Products.Add(product);

        var model = ConfigurationScaffolder.ScaffoldCombination("MyApp", "Docker", baseModel);

        // No matching product alias → falls back to default structure
        model.Products[0].TargetGroups[0].Targets[0].Alias.Should().Be("MainDB");
    }
}
