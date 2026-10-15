using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Looks a product up by alias and explains a miss: the alias is case-sensitive, and a near miss or the list
/// of configured aliases is named in the message.
/// </summary>
public static class ProductAliasGuard
{
    /// <summary>
    /// Returns the product with <paramref name="productAlias"/>.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">No product carries that alias.</exception>
    public static ProductOptions GetProduct(RayMigratorOptions options, string productAlias)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredProducts = options.Products ?? [];
        var match = configuredProducts.FirstOrDefault(p => p.Alias == productAlias);
        if (match != null)
        {
            return match;
        }

        var availableAliases = configuredProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Alias))
            .Select(p => p.Alias!)
            .ToList();

        throw new ConfigurationValidationException(NotFoundMessage(productAlias, availableAliases));
    }

    /// <summary>
    /// The message for a product alias the configuration does not carry: a hint at the alias that differs only in
    /// case, otherwise the list of the configured aliases, or that there is none. The loader uses it for the aliases
    /// of the merged files before the run's product is isolated, <see cref="GetProduct"/> for the bound options.
    /// </summary>
    /// <param name="productAlias">The alias that was asked for.</param>
    /// <param name="availableAliases">The aliases the configuration names, in their order.</param>
    public static string NotFoundMessage(string productAlias, IReadOnlyList<string> availableAliases)
    {
        ArgumentNullException.ThrowIfNull(availableAliases);

        string message = $"Product alias [{productAlias}] not found in the loaded configuration.";

        var caseInsensitiveMatch = availableAliases
            .FirstOrDefault(a => string.Equals(a, productAlias, StringComparison.OrdinalIgnoreCase));

        if (caseInsensitiveMatch != null)
        {
            message += $"\nDid you mean [{caseInsensitiveMatch}]? The --product parameter is case-sensitive.";
        }
        else if (availableAliases.Count > 0)
        {
            message += $"\nAvailable product aliases: {string.Join(", ", availableAliases)}";
            message += "\nNote: The --product and --environment parameters are case-sensitive.";
        }
        else
        {
            message += "\nNo products are configured in the loaded configuration files.";
            message += "\nNote: The --product and --environment parameters are case-sensitive.";
        }

        return message;
    }
}
