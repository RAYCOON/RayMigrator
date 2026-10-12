using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Raised once per migrate-up, migrate-down or baseline run, after the run row exists.
/// </summary>
/// <param name="ProductAlias">The product of the run.</param>
/// <param name="Environment">The environment of the run.</param>
/// <param name="Command">The command that runs.</param>
/// <param name="RunMode">Migrate, Simulate or Validate.</param>
/// <param name="MigrationRunId">The repository id of the run row, or 0 when the run mode writes no run row.</param>
/// <param name="FromRelease">The highest Migrated release when the run started; null for a first run.</param>
/// <param name="TargetRelease">The release the run aims at, or null for "everything".</param>
/// <param name="StartedAtUtc">When the run started.</param>
public sealed record RunStartedEvent(
    string ProductAlias,
    string Environment,
    MigrationCommand Command,
    MigrationRunMode RunMode,
    int MigrationRunId,
    string? FromRelease,
    string? TargetRelease,
    DateTime StartedAtUtc);
