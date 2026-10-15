using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration.Help;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: every field help key has a configuration path, and a path leads back to its key.
/// </summary>
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
        var allFieldKeys = ConfigurationHelp.GetAllFieldKeys();
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

    [Theory]
    [InlineData("Repository.DatabaseType", "Repository_DatabaseType")]
    [InlineData("products[].targetgroups[].targets[].connectionstring", "Target_ConnectionString")]
    [InlineData("Repository.HeartbeatIntervalInSeconds", "Repository_HeartbeatIntervalInSeconds")]
    public void TryGetFieldKey_RegisteredPath_ReturnsTheKey(string path, string expectedKey)
    {
        JsonPathRegistry.TryGetFieldKey(path, out var key).Should().BeTrue();
        key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Repository.Unknown")]
    public void TryGetFieldKey_UnknownPath_ReturnsFalse(string path)
    {
        JsonPathRegistry.TryGetFieldKey(path, out var key).Should().BeFalse();
        key.Should().BeEmpty();
    }


    [Theory]
    [InlineData("Products[].MigrationErrorAction", "ProductDefaults_MigrationErrorAction")]
    [InlineData("Products[].TargetGroups[].StopRollbackOnMissingRollbackFile", "ProductDefaults_StopRollbackOnMissingRollbackFile")]
    [InlineData("ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds", "Target_Timeout")]
    [InlineData("DatabaseLogging.Enabled", "DatabaseLogging_Enable")]
    [InlineData("Products[Shop].TargetGroups[Backend].Targets[Main].ConnectionString", "Target_ConnectionString")]
    [InlineData("Products[0].Alias", "Product_Alias")]
    [InlineData("products[].targetgroups[].alias", "TargetGroup_Alias")]
    public void TryGetFieldKey_FindsTheKey_ThroughTheInheritanceAndBehindAliasesAndIndexes(string path, string expectedKey)
    {
        JsonPathRegistry.TryGetFieldKey(path, out string key).Should().BeTrue(path);
        key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("DatabaseLogging")]
    [InlineData("Products[]")]
    [InlineData("Nowhere.Field")]
    public void TryGetFieldKey_KnowsNoKeyForASectionOrAnUnknownPath(string path)
    {
        JsonPathRegistry.TryGetFieldKey(path, out string key).Should().BeFalse(path);
        key.Should().BeEmpty();
    }

    [Fact]
    public void TryGetFieldKey_KnowsEveryOptionOfTheEngine()
    {
        var missing = new List<string>();
        foreach (string path in OptionPaths(typeof(Raycoon.RayMigrator.Core.Configuration.Options.RayMigratorOptions), string.Empty))
        {
            if (!JsonPathRegistry.TryGetFieldKey(path, out _))
                missing.Add(path);
        }

        missing.Should().BeEmpty("every option a host can show deserves its help text");
    }

    /// <summary>Every leaf of the options tree as a schema path: objects by their name, lists of option objects as [], everything else a leaf.</summary>
    private static IEnumerable<string> OptionPaths(Type type, string prefix)
    {
        foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            // Only what the binder writes is an option; the read-only enum views of the string options are not.
            if (!property.CanWrite || property.GetSetMethod() is not { IsPublic: true } || property.GetIndexParameters().Length > 0)
                continue;
            string path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var element = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>) ? propertyType.GetGenericArguments()[0] : null;
            if (element is not null && IsOptionsClass(element))
            {
                foreach (string below in OptionPaths(element, path + "[]"))
                    yield return below;
            }
            else if (IsOptionsClass(propertyType))
            {
                foreach (string below in OptionPaths(propertyType, path))
                    yield return below;
            }
            else
            {
                yield return path;
            }
        }
    }

    /// <summary>A class of the options namespace is a section; one without properties (Serilog, which Serilog itself reads) has no leaves.</summary>
    private static bool IsOptionsClass(Type type) =>
        type.IsClass && type.Namespace == typeof(Raycoon.RayMigrator.Core.Configuration.Options.RayMigratorOptions).Namespace;
}
