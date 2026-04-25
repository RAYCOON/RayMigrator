using System.Text.RegularExpressions;

namespace Raycoon.RayMigrator.Validation.Helpers;

/// <summary>
/// Extracts placeholder keys such as <c>{Server}</c> or <c>{FilePath}</c> from a
/// CLI tool's <c>ArgumentTemplate</c> string.
/// </summary>
public static class CliToolPlaceholderExtractor
{
    private static readonly Regex PlaceholderRegex =
        new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Keys that RayMigrator reserves for internal substitution. Users must not supply these
    /// via <c>CliToolParameters</c>; <see cref="RuleIds.RULE_3_9"/> flags collisions.
    /// </summary>
    public static IReadOnlySet<string> ReservedKeys { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FilePath" };

    /// <summary>
    /// Returns user-editable placeholder keys in <paramref name="argumentTemplate"/>.
    /// Excludes reserved keys (e.g. <c>FilePath</c>). Keys are deduplicated case-insensitively.
    /// </summary>
    public static List<string> ExtractParameterKeys(string? argumentTemplate)
    {
        if (string.IsNullOrWhiteSpace(argumentTemplate))
            return new List<string>();

        return PlaceholderRegex.Matches(argumentTemplate)
            .Select(m => m.Groups[1].Value)
            .Where(k => !ReservedKeys.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns every placeholder key, including reserved ones. Used by rules that must know
    /// whether a specific reserved key appears (e.g. RULE_3_1 checks for <c>{FilePath}</c>).
    /// </summary>
    public static List<string> ExtractAllPlaceholders(string? argumentTemplate)
    {
        if (string.IsNullOrWhiteSpace(argumentTemplate))
            return new List<string>();

        return PlaceholderRegex.Matches(argumentTemplate)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
