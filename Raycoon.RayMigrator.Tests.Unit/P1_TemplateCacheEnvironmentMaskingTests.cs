using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the values a TemplateCache substituted for {ENV:...} placeholders are masked in every run that
/// takes a template from the cache, not only in the run whose masking scope was active while it loaded.
/// </summary>
public class TemplateCacheEnvironmentMaskingTests
{
    private const string DatabaseType = "SqlServer";
    private const TemplateType Type = TemplateType.Repository_CheckCreate;
    private const string VariableName = "RM_TEST_TEMPLATE_CACHE_MASKING_38";
    private const string Secret = "tmpl-env-s3cret-38";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Builds a cache without the file system: the field initializers are set by hand, and the template goes
    /// through the same ENV replacement as a template file does while the cache loads.
    /// </summary>
    private static TemplateCache LoadCache(string rawContent)
    {
        var cache = (TemplateCache)RuntimeHelpers.GetUninitializedObject(typeof(TemplateCache));
        typeof(TemplateCache).GetField("_logger", Private)!.SetValue(cache, NullLogger<TemplateCache>.Instance);
        typeof(TemplateCache).GetField("_environmentValues", Private)!.SetValue(cache, new HashSet<string>(StringComparer.Ordinal));

        var content = (string)typeof(TemplateCache).GetMethod("ProcessEnvironmentVariables", Private)!
            .Invoke(cache, [rawContent, DatabaseType, $"{Type}.sql"])!;

        var templates = new Dictionary<string, Dictionary<TemplateType, Template>>
        {
            [DatabaseType] = new()
            {
                [Type] = new Template { TemplateType = Type, DatabaseType = DatabaseType, Filename = $"{Type}.sql", Content = content }
            }
        };
        typeof(TemplateCache).GetField("_templateDictionary", Private)!.SetValue(cache, templates);
        return cache;
    }

    [Fact]
    public void RunScopesAfterTheLoad_TakingATemplate_MaskTheEnvironmentValues()
    {
        Environment.SetEnvironmentVariable(VariableName, Secret);
        try
        {
            TemplateCache cache;
            using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
                cache = LoadCache($"SELECT '{{ENV:{VariableName}}}'");

            var repository = new RepositoryOptions { DatabaseType = DatabaseType };

            using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
            {
                var content = cache.GetTemplate(DatabaseType, Type, repository).Content;
                content.Should().Contain(Secret);
                SensitiveDataMasker.Mask(content).Should().NotContain(Secret);
            }

            using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
                SensitiveDataMasker.Mask(cache.GetRepositoryTemplate(Type, repository).Content).Should().NotContain(Secret);

            using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
                SensitiveDataMasker.Mask(cache.GetTemplateContent(DatabaseType, Type, repository)).Should().NotContain(Secret);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, null);
        }
    }
}
