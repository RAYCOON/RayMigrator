using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Database logging goes into the repository unless the configuration says otherwise: without a
/// <c>DatabaseLogging</c> section, or with one that leaves values out, the missing values come from
/// <c>Repository</c> (database type, connection string, schema, table prefix) and <c>MinimumLevel</c> is
/// <c>Information</c>. <c>"DatabaseLogging": { "Enabled": false }</c> switches database logging off; a section with
/// its own <c>DatabaseType</c> that differs from the repository's keeps a separate log database and must name its
/// own connection string.
/// </summary>
public static class DatabaseLoggingDefaults
{
    /// <summary>The level database logging uses when the configuration names none.</summary>
    public const string DefaultMinimumLevel = "Information";

    /// <summary>
    /// Fills <see cref="RayMigratorOptions.DatabaseLogging"/> as described in the class summary. Runs before the data
    /// annotations and the rule catalog, so the filled section is validated like a written one. Idempotent.
    /// </summary>
    public static void Apply(RayMigratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.DatabaseLogging ??= new DatabaseLoggingOptions();
        var logging = options.DatabaseLogging;
        if (logging.Enabled == false)
            return;

        var repository = options.Repository;
        if (repository != null)
        {
            // No prefix is the empty string: the templates substitute it and refuse a missing value.
            repository.TableBaseName ??= string.Empty;
            logging.DatabaseType ??= repository.DatabaseType;

            // The repository's connection, schema and prefix fit only a log database of the repository's type.
            bool sameDatabaseType = string.Equals(logging.DatabaseType, repository.DatabaseType, StringComparison.OrdinalIgnoreCase);
            if (sameDatabaseType)
            {
                logging.ConnectionString ??= repository.ConnectionString;
                logging.SchemaName ??= repository.SchemaName;
                logging.TableBaseName ??= repository.TableBaseName;
            }
        }

        if (string.IsNullOrWhiteSpace(logging.MinimumLevel))
            logging.MinimumLevel = DefaultMinimumLevel;
    }

    /// <summary>
    /// Whether database logging is on: a <c>DatabaseLogging</c> section exists (after <see cref="Apply"/> it always
    /// does) and does not say <c>Enabled: false</c>.
    /// </summary>
    public static bool IsDatabaseLoggingEnabled(this RayMigratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.DatabaseLogging is { Enabled: not false };
    }
}
