using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Shared.Configuration;
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
/// <remarks>
/// A run is prepared in the scope of its product (ADR-026): the CLI reduces the merged files to the product before
/// it binds them (<c>ConfigurationProductScope</c> in <c>Raycoon.RayMigrator.Shared</c>), and
/// <see cref="IMigrationContextFactory.Create"/> calls <see cref="IsolateProduct"/> before <see cref="Prepare"/>.
/// A host that binds a document of its own and hands the section as written to <see cref="Prepare"/> isolates the
/// document first, so that the lists of the section pair with the bound options.
/// </remarks>
public static class RayMigratorOptionsPreparer
{
    /// <summary>
    /// Reduces <paramref name="options"/> to the scope of one product: every element of
    /// <see cref="RayMigratorOptions.Products"/> whose alias is not <paramref name="productAlias"/> (compared like the
    /// configuration merger compares aliases, without regard to case; an alias written as a <c>{ENV:...}</c>
    /// placeholder is resolved for the comparison) is removed from the list in place; elements that repeat the
    /// alias stay for the rule catalog. Options without products, or whose products carry no alias, are left alone,
    /// so that the data annotations report the missing section as they do for the CLI. The caller's list is
    /// changed; <see cref="IMigrationContextFactory.Create"/> works on a copy of the options.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// <paramref name="productAlias"/> is blank, or the options name products but none carries it; the message names
    /// the aliases they carry, like <see cref="ProductAliasGuard.GetProduct"/> names them for bound options.
    /// </exception>
    public static void IsolateProduct(RayMigratorOptions options, string productAlias)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(productAlias))
        {
            throw new ConfigurationValidationException("The product alias of the run is missing. Name the product the run is for.");
        }

        if (options.Products is null)
        {
            return;
        }

        var availableAliases = options.Products
            .Select(ResolvedAlias)
            .Where(alias => alias is not null)
            .Select(alias => alias!)
            .ToList();
        if (availableAliases.Count == 0)
        {
            return;
        }

        if (!availableAliases.Any(alias => AliasComparer.AliasEquals(alias, productAlias)))
        {
            throw new ConfigurationValidationException(ProductAliasGuard.NotFoundMessage(productAlias, availableAliases));
        }

        options.Products.RemoveAll(p => !AliasComparer.AliasEquals(ResolvedAlias(p), productAlias));
    }

    /// <summary>The alias of a product for the comparison: resolved when it is a placeholder, null when it is blank.</summary>
    private static string? ResolvedAlias(ProductOptions product) =>
        string.IsNullOrWhiteSpace(product.Alias) ? null : EnvironmentVariableReplacer.ResolveForComparison(product.Alias);

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
