using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Prepares a <see cref="RayMigratorOptions"/> object that a host built in memory the way the CLI prepares the
/// options it binds from the <c>appsettings*.json</c> files: <c>{ENV:VARIABLE}</c> placeholders are resolved,
/// the product defaults are merged down, every enum-typed value is checked, the data annotations run (they also
/// write the documented default values back into null properties), and the rule catalog is applied. The object
/// is changed in place. Preparing an object twice is harmless.
/// </summary>
public static class RayMigratorOptionsPreparer
{
    /// <summary>
    /// Prepares <paramref name="options"/> for a run. See the class summary for the steps.
    /// </summary>
    /// <param name="options">The options to prepare. Changed in place.</param>
    /// <param name="sectionAsWritten">
    /// The configuration as written, before placeholders were resolved, when the host has one. RULE_7_3 then
    /// inspects the connection strings as written, so a credential that enters through a placeholder is not
    /// reported as hardcoded (#30). Without one, the values that contained placeholders are checked as they were
    /// written and everything else as bound.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    /// A placeholder names an environment variable that is not set or empty, an enum-typed value is not an
    /// allowed member, a data annotation fails, or the rule catalog reports an error. The message lists every
    /// finding of the failing step.
    /// </exception>
    public static OptionsPreparationResult Prepare(RayMigratorOptions options, IConfigurationSection? sectionAsWritten = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var replaced = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(options);
        var missing = replaced
            .Where(r => string.IsNullOrWhiteSpace(r.EnvironmentVariableValue))
            .Select(r => $"[{r.EnvironmentVariableName}] at [{r.Path}]")
            .Distinct()
            .ToList();

        if (missing.Count > 0)
        {
            throw new ConfigurationValidationException(
                "The configuration references environment variables that do not exist, are empty or contain whitespace only: " +
                string.Join(", ", missing));
        }

        SensitiveDataMasker.RegisterSensitiveValues(replaced.Select(r => r.EnvironmentVariableValue));

        // MergeDefaults, then the enum probe that reports every unparseable value at once.
        new ProductDefaultsPostConfigureOptions().PostConfigure(null, options);

        ThrowIfFailed(new DataAnnotationValidateOptions<RayMigratorOptions>(null).Validate(null, options), "Configuration validation failed");
        ThrowIfFailed(new RayMigratorOptionsValidator(sectionAsWritten ?? AsWrittenSection(replaced)).Validate(null, options), "Configuration validation failed");

        SensitiveDataMasker.RegisterSensitiveData(options);

        return new OptionsPreparationResult(replaced);
    }

    /// <summary>
    /// The values that contained placeholders, as they were written, so that RULE_7_3 sees <c>{ENV:...}</c> and not
    /// the resolved credential (#30). Every other key falls back to the bound value. Null when nothing was replaced.
    /// </summary>
    internal static IConfigurationSection? AsWrittenSection(IEnumerable<EnvironmentVariableWithMetadata> replaced)
    {
        var values = replaced
            .GroupBy(r => r.Path)
            .ToDictionary(g => g.Key, g => g.First().ConfigurationValue);
        if (values.Count == 0)
        {
            return null;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(InternalConstants.RayMigratorSectionName);
    }

    private static void ThrowIfFailed(ValidateOptionsResult result, string prefix)
    {
        if (!result.Failed)
        {
            return;
        }

        throw new ConfigurationValidationException($"{prefix}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Failures ?? [])}");
    }
}
