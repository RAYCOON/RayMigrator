using System.Text.RegularExpressions;

namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// Replaces <c>@Name</c> placeholders in SQL text with formatted parameter values, for DALs whose providers
/// cannot bind parameters inside the scripts the templates use (multi-statement scripts, session variables,
/// PostgreSQL <c>DO</c> blocks). The text is scanned once: a placeholder is only matched in the template text,
/// never inside a value that was substituted before it, and it is matched as a whole identifier, so
/// <c>@Product</c> leaves <c>@ProductId</c> alone. An identifier that is not in the parameter list stays as it
/// is, which keeps the session variables (<c>@v_count</c>) of the MySQL and MariaDB templates intact.
/// </summary>
public static partial class DalParameterSubstitution
{
    /// <summary>
    /// Substitutes every <c>@Name</c> placeholder whose name is in <paramref name="parameters"/>.
    /// </summary>
    /// <param name="sqlCode">The SQL text with placeholders.</param>
    /// <param name="parameters">The parameters; names are compared case-sensitively.</param>
    /// <param name="formatValue">Formats one parameter as a SQL literal of the provider.</param>
    /// <returns>The SQL text with every known placeholder replaced exactly once.</returns>
    public static string Substitute(string sqlCode, DalParameterList parameters, Func<DalParameter, string> formatValue)
    {
        ArgumentNullException.ThrowIfNull(sqlCode);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(formatValue);

        return PlaceholderRegex().Replace(sqlCode, match =>
            parameters.TryGetValue(match.Groups["name"].Value, out var parameter)
                ? formatValue(parameter!)
                : match.Value);
    }

    // A placeholder is an @ that does not follow an identifier character or another @ (@@sql_mode, a@b.c),
    // followed by a whole identifier.
    [GeneratedRegex(@"(?<![\w@])@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
