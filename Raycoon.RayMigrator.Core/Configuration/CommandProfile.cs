using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// Describes the side effects a command is allowed to have. Derived from the command (and, for
/// <see cref="MigrationCommand.MigrateUp"/> / <see cref="MigrationCommand.MigrateDown"/>, from the
/// user-selected <see cref="MigrationRunMode"/>) by <c>MigrationCommandExtensions.GetProfile</c>.
/// Every consumer that has to decide "may this command connect / write / log?" asks the profile
/// instead of interpreting the run mode itself (#6). Only fields with a consumer are part of the profile:
/// <c>ConnectsToTargets</c> steers the start-up connection check, <c>WritesRepository</c> the product and
/// environment bookkeeping, <c>WritesDatabaseLog</c> the DatabaseLogging sink (#19).
/// </summary>
/// <param name="ConnectsToTargets">The start-up connection check opens a connection to every target database. A command that never touches a target does not need one to be reachable.</param>
/// <param name="WritesRepository">The command writes to the repository: records, runs, hash updates, fixes, and the product/environment bookkeeping rows.</param>
/// <param name="WritesDatabaseLog">The command leaves an audit trail in the DatabaseLogging tables.</param>
public readonly record struct CommandProfile(
    bool ConnectsToTargets,
    bool WritesRepository,
    bool WritesDatabaseLog);
