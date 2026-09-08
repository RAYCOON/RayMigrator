namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Describes the side effects a command is allowed to have. Derived from the command (and, for
/// <see cref="MigrationCommand.MigrateUp"/> / <see cref="MigrationCommand.MigrateDown"/>, from the
/// user-selected <see cref="MigrationRunMode"/>) by <c>MigrationCommandExtensions.GetProfile</c>.
/// Every consumer that has to decide "may this command connect / write / log?" asks the profile
/// instead of interpreting the run mode itself (#6).
/// </summary>
/// <param name="ExecutesMigrations">The command executes migration files and honours <see cref="MigrationRunMode"/>. Only migrate-up and migrate-down.</param>
/// <param name="ConnectsToTargets">The start-up connection check opens a connection to every target database. A command that never touches a target does not need one to be reachable.</param>
/// <param name="ReadsRepository">The command reads migration records from the repository.</param>
/// <param name="WritesRepository">The command writes to the repository: records, runs, hash updates, fixes, and the product/environment bookkeeping rows.</param>
/// <param name="WritesDatabaseLog">The command leaves an audit trail in the DatabaseLogging tables.</param>
public readonly record struct CommandProfile(
    bool ExecutesMigrations,
    bool ConnectsToTargets,
    bool ReadsRepository,
    bool WritesRepository,
    bool WritesDatabaseLog);
