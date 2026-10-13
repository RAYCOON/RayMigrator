using System.Globalization;
using System.Reflection;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// Builds a row record from one result row of an ADO.NET reader, given as the column values by column name as the
/// provider reports them. On MySQL and MariaDB the reader's session must run in UTC (<c>SET time_zone = '+00:00'</c>);
/// see <see cref="ToUtc"/>. The mapper looks every constructor parameter up under its engine column name (case does
/// not matter) and converts the provider's value to the parameter type: integers of any width to the declared one,
/// lookup ids to their enums, SQLite text and unspecified-kind timestamps to UTC <see cref="DateTime"/>, 0/1 to
/// bool, <see cref="DBNull"/> to null.
/// </summary>
public static class RepositoryRowMapper
{
    /// <summary>Maps one row to <typeparamref name="T"/>.</summary>
    /// <exception cref="KeyNotFoundException">A column of the record is not in the row.</exception>
    /// <exception cref="InvalidCastException">A value cannot be converted to the parameter type, or is null for a value that cannot be.</exception>
    public static T Map<T>(IReadOnlyDictionary<string, object?> row, RepositoryNaming naming) =>
        (T)Map(typeof(T), row, naming);

    /// <summary>Maps one row to the record type of <paramref name="table"/>.</summary>
    public static object Map(RepositoryTable table, IReadOnlyDictionary<string, object?> row, RepositoryNaming naming) =>
        Map(RepositoryRows.TypeOf(table), row, naming);

    /// <summary>Maps one row to <paramref name="rowType"/>, a record of <see cref="RepositoryRows"/>.</summary>
    public static object Map(Type rowType, IReadOnlyDictionary<string, object?> row, RepositoryNaming naming)
    {
        ArgumentNullException.ThrowIfNull(rowType);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(naming);

        var constructor = RepositoryRows.ConstructorOf(rowType);
        var parameters = constructor.GetParameters();
        var arguments = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            string column = naming.ColumnName(parameter.Name!);
            if (!TryGetValue(row, column, out object? raw))
            {
                throw new KeyNotFoundException(
                    $"Column [{column}] for {rowType.Name}.{parameter.Name} is not in the row; the row has [{string.Join(", ", row.Keys)}].");
            }

            arguments[i] = Convert(raw, parameter, rowType);
        }

        return constructor.Invoke(arguments);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> row, string column, out object? value)
    {
        if (row.TryGetValue(column, out value))
            return true;

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, column, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object? Convert(object? raw, ParameterInfo parameter, Type rowType)
    {
        Type targetType = parameter.ParameterType;
        Type? underlying = Nullable.GetUnderlyingType(targetType);
        Type type = underlying ?? targetType;

        if (raw is null || raw is DBNull)
        {
            if (underlying != null || !type.IsValueType)
                return null;

            throw new InvalidCastException($"{rowType.Name}.{parameter.Name} is {type.Name} and cannot be null.");
        }

        try
        {
            return ConvertValue(raw, type);
        }
        catch (Exception ex) when (ex is not InvalidCastException)
        {
            throw new InvalidCastException($"{rowType.Name}.{parameter.Name}: cannot convert [{raw}] ({raw.GetType().Name}) to {type.Name}.", ex);
        }
    }

    /// <summary>Converts a provider value to <paramref name="type"/>, which is never nullable here.</summary>
    internal static object ConvertValue(object raw, Type type)
    {
        if (type.IsEnum)
            return Enum.ToObject(type, System.Convert.ToInt64(raw, CultureInfo.InvariantCulture));

        if (type == typeof(DateTime))
            return ToUtc(raw);

        if (type == typeof(bool))
        {
            return raw switch
            {
                bool b => b,
                string s => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
                _ => System.Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0,
            };
        }

        if (type == typeof(string))
            return raw as string ?? System.Convert.ToString(raw, CultureInfo.InvariantCulture)!;

        return System.Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The repository stores UTC timestamps on every engine. A value without a kind (SQL Server datetime2, MySQL
    /// and MariaDB timestamp) is UTC, a local one is converted, SQLite text is parsed as UTC. The MySQL and MariaDB
    /// columns are <c>TIMESTAMP</c>, which the server converts to the session time zone on read: a reader's session
    /// must run in UTC (<c>SET time_zone = '+00:00'</c>, as the engine DAL does after every open), or every
    /// timestamp is off by the session's offset.
    /// </summary>
    internal static DateTime ToUtc(object raw) => raw switch
    {
        DateTime { Kind: DateTimeKind.Utc } d => d,
        DateTime { Kind: DateTimeKind.Local } d => d.ToUniversalTime(),
        DateTime d => DateTime.SpecifyKind(d, DateTimeKind.Utc),
        DateTimeOffset o => o.UtcDateTime,
        string s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        _ => throw new InvalidCastException($"Cannot read a timestamp from [{raw}] ({raw.GetType().Name})."),
    };
}
