namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class JsonPathRegistryTests
{
    [Fact]
    public void GetPathInfo_RegisteredKey_ReturnsPathInfo()
    {
        var result = JsonPathRegistry.GetPathInfo("Repository_DatabaseType");

        result.Should().NotBeNull();
        result!.ConfigPath.Should().Be("Repository.DatabaseType");
    }

    [Fact]
    public void GetPathInfo_RegisteredKey_InheritedByPathsIsNullByDefault()
    {
        var result = JsonPathRegistry.GetPathInfo("Repository_DatabaseType");

        result.Should().NotBeNull();
        result!.InheritedByPaths.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_RegisteredKey_InheritedFromPathIsNullByDefault()
    {
        var result = JsonPathRegistry.GetPathInfo("Repository_DatabaseType");

        result.Should().NotBeNull();
        result!.InheritedFromPath.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_UnregisteredKey_ReturnsNull()
    {
        var result = JsonPathRegistry.GetPathInfo("NonExistent_Field");

        result.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_ConceptKey_ReturnsNull()
    {
        var result = JsonPathRegistry.GetPathInfo("Concept_Environment");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NonExistent_Something")]
    [InlineData("Unknown_Field")]
    public void GetPathInfo_UnregisteredOrEmptyKey_ReturnsNull(string key)
    {
        var result = JsonPathRegistry.GetPathInfo(key);

        result.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_ProductDefaultsWithInheritedBy_ReturnsInheritedByPaths()
    {
        var result = JsonPathRegistry.GetPathInfo("ProductDefaults_MigrationErrorAction");

        result.Should().NotBeNull();
        result!.ConfigPath.Should().Be("ProductDefaults.MigrationErrorAction");
        result.InheritedByPaths.Should().ContainSingle()
            .Which.Should().Be("Products[].MigrationErrorAction");
        result.InheritedFromPath.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_TargetGroupWithInheritedFrom_ReturnsInheritedFromPath()
    {
        var result = JsonPathRegistry.GetPathInfo("TargetGroup_TargetMigrationOrder");

        result.Should().NotBeNull();
        result!.ConfigPath.Should().Be("Products[].TargetGroups[].TargetMigrationOrder");
        result.InheritedFromPath.Should().Be("ProductDefaults.TargetGroupDefaults.TargetMigrationOrder");
        result.InheritedByPaths.Should().BeNull();
    }

    [Fact]
    public void GetPathInfo_AllContextHelpFieldKeysExceptConceptsAreRegistered()
    {
        var allFieldKeys = ContextHelpProvider.GetAllFieldKeys();
        var conceptKeys = allFieldKeys.Where(k => k.StartsWith("Concept_")).ToList();
        var nonConceptKeys = allFieldKeys.Except(conceptKeys).ToList();

        foreach (var key in nonConceptKeys)
        {
            JsonPathRegistry.GetPathInfo(key).Should().NotBeNull(
                because: $"field key '{key}' should be registered in JsonPathRegistry");
        }

        foreach (var key in conceptKeys)
        {
            JsonPathRegistry.GetPathInfo(key).Should().BeNull(
                because: $"concept key '{key}' has no JSON path");
        }
    }
}
