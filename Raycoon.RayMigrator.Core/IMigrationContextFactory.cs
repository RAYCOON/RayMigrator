using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Creates the <see cref="MigrationContext"/> of one run and makes it the current context. This is the entry
/// point for every host that runs the engine in-process, the CLI pipeline included.
/// </summary>
/// <remarks>
/// Both methods build the context, fill its <see cref="MigrationContext.DalSpecificPropertiesDictionary"/> for the
/// repository, the targets of the product and the database logging, check the schema names against the DAL
/// capabilities, and then set the <see cref="IMigrationContextAccessor"/> and <see cref="MigrationLoggingContext"/>
/// of the calling async flow. The command in the console options is stamped into the MigrationRun settings
/// snapshot and drives the command's side effects (see <see cref="Configuration.CommandProfile"/>), so it must
/// be the command that is actually executed (#6). The run mode is only a choice for migrate-up and migrate-down;
/// every other command runs as <see cref="Configuration.Enums.MigrationRunMode.Migrate"/>. One context serves
/// one operation: its <see cref="MigrationState"/> changes during the run.
/// </remarks>
public interface IMigrationContextFactory
{
    /// <summary>
    /// Prepares <paramref name="options"/> with <see cref="RayMigratorOptionsPreparer"/> (placeholders, defaults,
    /// enum check, data annotations, rule catalog) and creates the context. For options a host built in memory,
    /// for example from a configuration store.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// The options do not pass preparation, the product alias is unknown, a configured DatabaseType has no
    /// data access layer, or a schema name is set for a database that does not support schemas.
    /// </exception>
    MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);

    /// <summary>
    /// Creates the context for options that the options framework already bound, merged and validated
    /// (the CLI pipeline). Everything else is as in <see cref="Create"/>.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// The product alias is unknown, a configured DatabaseType has no data access layer, or a schema name is set
    /// for a database that does not support schemas.
    /// </exception>
    MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);
}
