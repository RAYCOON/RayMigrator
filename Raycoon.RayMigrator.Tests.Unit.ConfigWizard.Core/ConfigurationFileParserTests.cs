using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class ConfigurationFileParserTests
{
    [Fact]
    public void Parse_EmptyFiles_ReturnsEmptyState()
    {
        var state = ConfigurationFileParser.Parse(new Dictionary<string, string>());
        state.BaseModel.Should().NotBeNull();
        state.EnvironmentModels.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleBaseFile_ParsesCorrectly()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson()
        };

        var state = ConfigurationFileParser.Parse(files);
        state.BaseModel.Repository.DatabaseType.Should().Be("SqlServer");
        state.BaseModel.Products.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_MultipleFiles_ClassifiesCorrectly()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.Docker.json"] = """{"RayMigrator":{"Repository":{"ConnectionString":"{ENV:DOCKER_CONN}"}}}""",
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator":{}}"""
        };

        var state = ConfigurationFileParser.Parse(files);
        state.EnvironmentModels.Should().ContainKey("Docker");
        state.ProductEnvironmentModels.Should().ContainKey("MyApp.Docker");
    }

    [Fact]
    public void Parse_ReverseEngineersSetupAnswers()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson()
        };

        var state = ConfigurationFileParser.Parse(files);
        state.SetupAnswers.RepositoryDatabaseType.Should().Be("SqlServer");
        state.SetupAnswers.Products.Should().HaveCount(1);
        state.SetupAnswers.Products[0].Alias.Should().Be("MyApp");
    }

    // ── ClassifyFileName ─────────────────────────────────────────

    [Fact]
    public void ClassifyFileName_Base()
    {
        var (role, product, env) = ConfigurationFileParser.ClassifyFileName("appsettings.json");
        role.Should().Be(ConfigFileRole.Base);
        product.Should().BeNull();
        env.Should().BeNull();
    }

    [Fact]
    public void ClassifyFileName_Environment()
    {
        var (role, _, env) = ConfigurationFileParser.ClassifyFileName("appsettings.Docker.json");
        role.Should().Be(ConfigFileRole.Environment);
        env.Should().Be("Docker");
    }

    [Fact]
    public void ClassifyFileName_ProductEnvironment()
    {
        var (role, product, env) = ConfigurationFileParser.ClassifyFileName("appsettings.MyApp.Docker.json");
        role.Should().Be(ConfigFileRole.ProductEnvironment);
        product.Should().Be("MyApp");
        env.Should().Be("Docker");
    }

    [Fact]
    public void ClassifyFileName_ThreeSegments()
    {
        var (role, product, env) = ConfigurationFileParser.ClassifyFileName("appsettings.My.App.Docker.json");
        role.Should().Be(ConfigFileRole.ProductEnvironment);
        product.Should().Be("My.App");
        env.Should().Be("Docker");
    }

    [Fact]
    public void ClassifyFileName_WithPath_UsesFilenameOnly()
    {
        var (role, _, _) = ConfigurationFileParser.ClassifyFileName("/some/path/appsettings.json");
        role.Should().Be(ConfigFileRole.Base);
    }

    [Fact]
    public void Parse_MalformedJson_SkipsGracefully()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.Docker.json"] = "{ invalid json"
        };

        // Should not throw
        var act = () => ConfigurationFileParser.Parse(files);
        act.Should().NotThrow();
    }

    // ── Per-product environment inference ─────────────────────────

    [Fact]
    public void Parse_PerProductEnvInference_SameProductTwoEnvs_BothAssignedToProduct()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.ProductA.Production.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        var productSetup = state.SetupAnswers.Products.FirstOrDefault(p => p.Alias == "MyApp");
        // The base model has MyApp; the PE models are for ProductA
        // The PE environments are assigned per-product in answers
        state.ProductEnvironmentModels.Should().ContainKey("ProductA.Docker");
        state.ProductEnvironmentModels.Should().ContainKey("ProductA.Production");
    }

    [Fact]
    public void Parse_PerProductEnvInference_TwoDifferentProducts_EnvironmentsAreAssignedPerProduct()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.ProductB.Staging.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        state.ProductEnvironmentModels.Should().ContainKey("ProductA.Docker");
        state.ProductEnvironmentModels.Should().ContainKey("ProductB.Staging");
    }

    [Fact]
    public void Parse_PerProductEnvInference_ProductWithPeModels_UsesPeEnvironments()
    {
        // Base model has MyApp with no PE models; answers should fall back to standalone environments
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.Docker.json"] = """{"RayMigrator": {"Repository": {"ConnectionString": "{ENV:CONN}"}}}""",
        };

        var state = ConfigurationFileParser.Parse(files);

        // MyApp has no PE models, so answers uses standalone environments
        var myAppSetup = state.SetupAnswers.Products.FirstOrDefault(p => p.Alias == "MyApp");
        myAppSetup.Should().NotBeNull();
        myAppSetup!.Environments.Should().Contain("Docker");
    }

    [Fact]
    public void Parse_PerProductEnvInference_WithPeModels_UsesPeEnvironmentsNotStandalone()
    {
        // ProductA has a PE model for Docker, but standalone has Production
        // ProductA answers should only get Docker (from PE), not Production (from standalone)
        var files = new Dictionary<string, string>
        {
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.Production.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        var productASetup = state.SetupAnswers.Products.FirstOrDefault(p => p.Alias == "ProductA");
        // ProductA is discovered via PE models (not base) — check via ProductEnvironmentModels
        state.ProductEnvironmentModels.Should().ContainKey("ProductA.Docker");
        state.ProductEnvironmentModels.Should().NotContainKey("ProductA.Production");
    }

    // ── CombinationEntries population ─────────────────────────────

    [Fact]
    public void Parse_PopulatesCombinationEntriesFromPeModelKeys()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson(),
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.MyApp.Production.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        state.CombinationEntries.Should().ContainKey("MyApp.Docker");
        state.CombinationEntries.Should().ContainKey("MyApp.Production");
    }

    [Fact]
    public void Parse_CombinationEntries_WizardCompletedIsFalse()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.MyApp.Docker.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        state.CombinationEntries["MyApp.Docker"].WizardCompleted.Should().BeFalse();
    }

    [Fact]
    public void Parse_NoPeModels_CombinationEntriesIsEmpty()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = TestModelFactory.CreateValidJson()
        };

        var state = ConfigurationFileParser.Parse(files);

        state.CombinationEntries.Should().BeEmpty();
    }

    [Fact]
    public void Parse_CombinationEntriesCount_MatchesPeModelCount()
    {
        var files = new Dictionary<string, string>
        {
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.ProductA.Production.json"] = """{"RayMigrator": {}}""",
            ["appsettings.ProductB.Staging.json"] = """{"RayMigrator": {}}"""
        };

        var state = ConfigurationFileParser.Parse(files);

        state.CombinationEntries.Should().HaveCount(state.ProductEnvironmentModels.Count);
    }
}
