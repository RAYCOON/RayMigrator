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

        throw new ConfigurationValidationException(message);
    }
}
