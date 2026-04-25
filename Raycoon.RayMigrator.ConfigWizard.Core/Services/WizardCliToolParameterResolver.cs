
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.Validation.Helpers;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Wizard-side helper that resolves the list of parameter keys expected by a CLI tool,
/// with fallback to <see cref="CliToolPresetProvider"/> for unknown aliases.
/// Placeholder extraction itself lives in
/// <see cref="CliToolPlaceholderExtractor"/> (zero-deps library).
/// </summary>
public static class WizardCliToolParameterResolver
{
    public static List<string> ResolveParameterKeys(string? cliToolAlias, IReadOnlyList<CliToolModel> cliTools)
    {
        if (string.IsNullOrWhiteSpace(cliToolAlias))
            return new List<string>();

        var tool = cliTools.FirstOrDefault(t =>
            string.Equals(t.Alias, cliToolAlias, StringComparison.OrdinalIgnoreCase));

        if (tool != null)
        {
            var keys = CliToolPlaceholderExtractor.ExtractParameterKeys(tool.ArgumentTemplate);
            if (keys.Count > 0)
                return keys;
        }

        var preset = CliToolPresetProvider.GetPresetByAlias(cliToolAlias);
        if (preset != null)
            return new List<string>(preset.ExpectedParameterKeys);

        return new List<string>();
    }
}
