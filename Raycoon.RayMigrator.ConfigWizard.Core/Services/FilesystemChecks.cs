
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Wizard-only checks that require filesystem access. Gated by <see cref="ValidationCapability.Filesystem"/>
/// so they never run in Blazor WASM.
/// </summary>
internal static class FilesystemChecks
{
    public static void ValidateMigrationFilesRootDirectories(ConfigurationModel model, WizardValidationResult result)
    {
        foreach (var product in model.Products)
        {
            ValidateProductDirectory(product, $"Products > {product.Alias}", result);
        }
    }

    public static void ValidateProductDirectory(ProductModel product, string prefix, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(product.MigrationFilesRootDirectory))
            return;

        if (!Directory.Exists(product.MigrationFilesRootDirectory))
        {
            result.AddWarning(
                $"{prefix} > MigrationFilesRootDirectory",
                $"Directory '{product.MigrationFilesRootDirectory}' does not exist.");
        }
    }
}
