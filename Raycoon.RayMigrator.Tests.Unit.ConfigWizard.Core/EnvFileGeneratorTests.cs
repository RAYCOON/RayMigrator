
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class EnvFileGeneratorTests
{
    [Fact]
    public void Generate_NoEnvVars_ReturnsNoVarsMessage()
    {
        var model = new ConfigurationModel();
        model.Repository.ConnectionString = "Server=localhost;Database=test";

        var result = EnvFileGenerator.Generate(model);
        result.Should().Contain("No environment variables");
    }

    [Fact]
    public void Generate_WithEnvVars_ListsThem()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:REPO_CONN}";

        var result = EnvFileGenerator.Generate(model);
        result.Should().Contain("REPO_CONN");
        result.Should().Contain("Repository.ConnectionString");
    }

    [Fact]
    public void Generate_CustomResolver_UsesIt()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:REPO_CONN}";

        var result = EnvFileGenerator.Generate(model, varName => varName == "REPO_CONN" ? "already-set" : null);
        result.Should().Contain("REPO_CONN=already-set");
    }

    [Fact]
    public void Generate_UnresolvedEnvVar_EmptyValue()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:UNKNOWN_VAR}";

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain("UNKNOWN_VAR=\n");
    }

    [Fact]
    public void Generate_MultipleLocations_TracksAll()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.ConnectionString = "{ENV:SHARED_CONN}";
        model.Products[0].TargetGroups[0].Targets[0].ConnectionString = "{ENV:SHARED_CONN}";

        var result = EnvFileGenerator.Generate(model, _ => null);
        result.Should().Contain("Repository.ConnectionString");
        result.Should().Contain("ConnectionString");
        // The variable should appear only once
        var lines = result.Split('\n').Where(l => l.StartsWith("SHARED_CONN=")).ToList();
        lines.Should().HaveCount(1);
    }
}
