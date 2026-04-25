using System.Text.Json;
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Generates an appsettings.{env}.json skeleton that overrides connection strings
/// with {ENV:} placeholders for environment-specific deployment.
/// </summary>
public static class EnvironmentSkeletonGenerator
{
    /// <summary>
    /// Generates a minimal JSON skeleton containing only the connection string overrides.
    /// </summary>
    public static string Generate(ConfigurationModel model, bool indented = true)
    {
        var ray = new JsonObject();

        // Repository connection string override
        ray["Repository"] = new JsonObject
        {
            ["ConnectionString"] = ToEnvPlaceholder(model.Repository.ConnectionString, "REPO_CONNECTION_STRING")
        };

        // DatabaseLogging connection string override
        if (model.DatabaseLogging != null)
        {
            ray["DatabaseLogging"] = new JsonObject
            {
                ["ConnectionString"] = ToEnvPlaceholder(model.DatabaseLogging.ConnectionString, "DBLOG_CONNECTION_STRING")
            };
        }

        // Products: only connection string overrides per target
        if (model.Products.Count > 0)
        {
            var products = new JsonArray();
            foreach (var product in model.Products)
            {
                var prodObj = new JsonObject
                {
                    ["Alias"] = product.Alias
                };

                var tgArray = new JsonArray();
                foreach (var tg in product.TargetGroups)
                {
                    var tgObj = new JsonObject
                    {
                        ["Alias"] = tg.Alias
                    };

                    var targetsArr = new JsonArray();
                    foreach (var target in tg.Targets)
                    {
                        string envVarName = $"{SanitizeForEnv(product.Alias)}_{SanitizeForEnv(tg.Alias)}_{SanitizeForEnv(target.Alias)}_CONNECTION_STRING";
                        targetsArr.Add(new JsonObject
                        {
                            ["Alias"] = target.Alias,
                            ["ConnectionString"] = ToEnvPlaceholder(target.ConnectionString, envVarName)
                        });
                    }
                    tgObj["Targets"] = targetsArr;
                    tgArray.Add(tgObj);
                }
                prodObj["TargetGroups"] = tgArray;
                products.Add(prodObj);
            }
            ray["Products"] = products;
        }

        var root = new JsonObject { ["RayMigrator"] = ray };
        var options = new JsonSerializerOptions { WriteIndented = indented };
        return root.ToJsonString(options);
    }

    /// <summary>
    /// If the value already contains an {ENV:} placeholder, keep it as-is.
    /// Otherwise, wrap it in a generated placeholder name.
    /// </summary>
    private static string ToEnvPlaceholder(string currentValue, string fallbackEnvName)
    {
        if (!string.IsNullOrEmpty(currentValue) && currentValue.Contains("{ENV:"))
            return currentValue;

        return $"{{ENV:{fallbackEnvName}}}";
    }

    private static string SanitizeForEnv(string alias)
    {
        return alias.ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('-', '_')
            .Replace('.', '_');
    }
}
