namespace Raycoon.RayMigrator.Core.Extensions;

public static class EnumTypeExtensions
{
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
    /// Resolves <paramref name="raw"/> to a member name of <paramref name="enumType"/>: the member names after the
    /// <c>Undefined</c> sentinel, compared case-insensitively. Returns <c>null</c> when nothing matches. Former
    /// member names are not accepted; the enums carry no aliases (#19).
    /// </summary>
    public static string? ResolveMemberName(this Type enumType, string? raw)
    {
        if (raw is null)
            return null;

        return enumType.AllowedValues().FirstOrDefault(name => name.Equals(raw, StringComparison.OrdinalIgnoreCase));
    }
}
