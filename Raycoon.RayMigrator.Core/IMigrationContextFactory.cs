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
    /// Reduces a copy of <paramref name="options"/> to the product of the console options
    /// (<see cref="RayMigratorOptionsPreparer.IsolateProduct"/>: the other products are left out, so a fault in one
    /// of them does not reach this run), prepares the copy with <see cref="RayMigratorOptionsPreparer"/>
    /// (placeholders, defaults, enum check, data annotations, rule catalog) and creates the context from it. The
    /// caller's options keep every product; the copy shares the sections and the product objects, into which the
    /// preparer writes the resolved values and defaults as before, and the context carries the copy. For options a
    /// host built in memory, for example from a configuration store; a host that creates one context per product
    /// from the same options can do so in turn. A host that binds a document and wants the rule catalog to see the
    /// connection strings as written isolates the document with <c>ConfigurationProductScope</c> of
    /// <c>Raycoon.RayMigrator.Shared</c> before it binds.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// The product alias of the console options is blank or unknown (before anything is prepared), the options do
    /// not pass preparation, a configured DatabaseType has no data access layer, or a schema name is missing where
    /// the database requires one. A schema name on a database without schemas is only logged as a warning.
    /// </exception>
    MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);

    /// <summary>
    /// Creates the context for options that the options framework already bound, merged and validated
    /// (the CLI pipeline, whose loader applied the product scope before binding). The options are taken as they
    /// are: no product scope, no preparation; the context carries the given object. Everything after the
    /// preparation is as in <see cref="Create"/>.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// The product alias is unknown, a configured DatabaseType has no data access layer, or a schema name is
    /// missing where the database requires one.
    /// </exception>
    MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);
}
