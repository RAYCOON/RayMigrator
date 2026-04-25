using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: TemplateCache CFG-placeholder validation tests.
/// Verifies that GetTemplate and GetRepositoryTemplate throw ConfigurationValidationException
/// when unreplaced {CFG:*} placeholders remain after substitution.
/// </summary>
public class TemplateCacheCfgValidationTests
{
    private const string TestDatabaseType = "SqlServer";
    private const TemplateType TestTemplateType = TemplateType.Repository_CheckCreate;

    /// <summary>
    /// Creates a TemplateCache instance with the given template content pre-loaded,
    /// bypassing the file-system-based constructor.
    /// </summary>
    private static TemplateCache CreateTemplateCacheWithContent(string databaseType, TemplateType templateType, string templateContent)
    {
        var cache = (TemplateCache)RuntimeHelpers.GetUninitializedObject(typeof(TemplateCache));

        var templateDict = new Dictionary<string, Dictionary<TemplateType, Template>>
        {
            [databaseType] = new Dictionary<TemplateType, Template>
            {
                [templateType] = new Template
                {
                    TemplateType = templateType,
                    DatabaseType = databaseType,
                    Filename = $"{templateType}.sql",
                    Content = templateContent
                }
            }
        };

        var field = typeof(TemplateCache).GetField("_templateDictionary",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(cache, templateDict);

        return cache;
    }

    #region GetTemplate

    [Fact]
    public void GetTemplate_AllPlaceholdersResolved_ReturnsTemplate()
    {
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType,
            "CREATE SCHEMA {CFG:SchemaName}; CREATE TABLE {CFG:TableBaseName};");

        var propertyClass = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var result = cache.GetTemplate(TestDatabaseType, TestTemplateType, propertyClass);

        result.Content.Should().Be("CREATE SCHEMA ray; CREATE TABLE Migration;");
        result.Content.Should().NotContain(ConfigurationConstants.ConfigurationVariablePrefix);
    }

    [Fact]
    public void GetTemplate_UnreplacedCfgPlaceholder_ThrowsConfigurationValidationException()
    {
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType,
            "SELECT {CFG:SchemaName}.{CFG:UnknownProp}");

        var propertyClass = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var act = () => cache.GetTemplate(TestDatabaseType, TestTemplateType, propertyClass);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage($"*{TestTemplateType}*")
            .WithMessage($"*{TestDatabaseType}*")
            .WithMessage($"*{ConfigurationConstants.ConfigurationVariablePrefix}*");
    }

    [Fact]
    public void GetTemplate_NoCfgPlaceholders_ReturnsTemplateUnchanged()
    {
        const string plainSql = "CREATE TABLE dbo.Users (Id INT PRIMARY KEY);";
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType, plainSql);

        var propertyClass = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var result = cache.GetTemplate(TestDatabaseType, TestTemplateType, propertyClass);

        result.Content.Should().Be(plainSql);
    }

    #endregion

    #region GetRepositoryTemplate

    [Fact]
    public void GetRepositoryTemplate_AllPlaceholdersResolved_ReturnsTemplate()
    {
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType,
            "CREATE SCHEMA {CFG:SchemaName}; CREATE TABLE {CFG:TableBaseName};");

        var repoOptions = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var result = cache.GetRepositoryTemplate(TestTemplateType, repoOptions);

        result.Content.Should().Be("CREATE SCHEMA ray; CREATE TABLE Migration;");
        result.Content.Should().NotContain(ConfigurationConstants.ConfigurationVariablePrefix);
    }

    [Fact]
    public void GetRepositoryTemplate_UnreplacedCfgPlaceholder_ThrowsConfigurationValidationException()
    {
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType,
            "SELECT {CFG:SchemaName}.{CFG:UnknownProp}");

        var repoOptions = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var act = () => cache.GetRepositoryTemplate(TestTemplateType, repoOptions);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage($"*{TestTemplateType}*")
            .WithMessage($"*{TestDatabaseType}*")
            .WithMessage($"*{ConfigurationConstants.ConfigurationVariablePrefix}*");
    }

    [Fact]
    public void GetRepositoryTemplate_NoCfgPlaceholders_ReturnsTemplateUnchanged()
    {
        const string plainSql = "SELECT COUNT(*) FROM ray.Migration;";
        var cache = CreateTemplateCacheWithContent(TestDatabaseType, TestTemplateType, plainSql);

        var repoOptions = new RepositoryOptions
        {
            DatabaseType = TestDatabaseType,
            SchemaName = "ray",
            TableBaseName = "Migration"
        };

        var result = cache.GetRepositoryTemplate(TestTemplateType, repoOptions);

        result.Content.Should().Be(plainSql);
    }

    #endregion
}
