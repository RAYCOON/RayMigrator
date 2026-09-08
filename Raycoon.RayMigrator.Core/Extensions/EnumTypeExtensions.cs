using System.Collections.Concurrent;
using System.Reflection;

namespace Raycoon.RayMigrator.Core.Extensions;

public static class EnumTypeExtensions
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, string>> AliasCache = new();

    public static string[] AllowedValues(this Type enumType, bool ignoreFirstValue = true)
    {
        if (enumType == null)
            throw new ArgumentNullException(nameof(enumType));

        if (!enumType.IsEnum)
            throw new ArgumentException("Internal error: Provided type is no Enum.", nameof(enumType));

        var allowedValues = Enum.GetNames(enumType);

        if (ignoreFirstValue)
            allowedValues = allowedValues.Skip(1).ToArray();

        return allowedValues;
    }

    /// <summary>
    /// The former member names declared with <see cref="EnumAliasAttribute"/>, mapped (case-insensitively) to the
    /// current member name. Empty for enums without aliases (#19).
    /// </summary>
    public static IReadOnlyDictionary<string, string> Aliases(this Type enumType)
    {
        if (enumType == null)
            throw new ArgumentNullException(nameof(enumType));

        if (!enumType.IsEnum)
            throw new ArgumentException("Internal error: Provided type is no Enum.", nameof(enumType));

        return AliasCache.GetOrAdd(enumType, static type =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .SelectMany(field => field.GetCustomAttributes<EnumAliasAttribute>().Select(a => (a.Alias, Member: field.Name)))
                .ToDictionary(x => x.Alias, x => x.Member, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves <paramref name="raw"/> to a member name of <paramref name="enumType"/>: the member names after the
    /// <c>Undefined</c> sentinel and their aliases, compared case-insensitively. Returns <c>null</c> when nothing matches.
    /// </summary>
    public static string? ResolveMemberName(this Type enumType, string? raw)
    {
        if (raw is null)
            return null;

        var match = enumType.AllowedValues().FirstOrDefault(name => name.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        return enumType.Aliases().TryGetValue(raw, out var member) ? member : null;
    }
}
