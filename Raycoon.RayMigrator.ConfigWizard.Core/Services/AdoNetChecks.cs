
using System.Data.Common;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Wizard-only checks that rely on <see cref="DbConnectionStringBuilder"/> parsing.
/// Gated by <see cref="ValidationCapability.AdoNetParsing"/> so they never run in WASM
/// (ADO.NET builder is not available on browser targets).
/// </summary>
internal static class AdoNetChecks
{
    public static void ValidateConnectionStringSyntax(ConfigurationModel model, WizardValidationResult result)
    {
        CheckConnection(model.Repository.ConnectionString, "Repository > ConnectionString", result);

        if (model.DatabaseLogging is { } dbLog)
            CheckConnection(dbLog.ConnectionString, "DatabaseLogging > ConnectionString", result);

        foreach (var product in model.Products)
        {
            foreach (var tg in product.TargetGroups)
            {
                foreach (var target in tg.Targets)
                {
                    var path = $"Products > {product.Alias} > TargetGroups > {tg.Alias} > Targets > {target.Alias} > ConnectionString";
                    CheckConnection(target.ConnectionString, path, result);
                }
            }
        }
    }

    public static void CheckConnection(string? connectionString, string path, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        if (connectionString.Contains("{ENV:", StringComparison.Ordinal)) return;

        try
        {
            _ = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            result.AddError(path, "Invalid connection string syntax.");
        }
    }
}
