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
}
