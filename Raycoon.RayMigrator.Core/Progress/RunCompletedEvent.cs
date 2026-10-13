using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Raised once when a migrate-up, migrate-down or baseline run finished.
/// </summary>
/// <param name="ProductAlias">The product of the run.</param>
/// <param name="Environment">The environment of the run.</param>
/// <param name="Command">The command that ran.</param>
/// <param name="MigrationRunId">The repository id of the run row, or 0 when none was written.</param>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Result">The persisted run result for migrate-up and migrate-down; null for baseline.</param>
/// <param name="TotalFiles">The entries of the result: files, or file and target pairs, as the operation counts them.</param>
/// <param name="SucceededFiles">The entries that succeeded.</param>
/// <param name="FailedFiles">The entries that failed.</param>
/// <param name="Duration">How long the run took.</param>
/// <param name="ErrorMessage">The error of a failed run.</param>
public sealed record RunCompletedEvent(
    string ProductAlias,
    string Environment,
    MigrationCommand Command,
    int MigrationRunId,
    bool Success,
    MigrationRunResult? Result,
    int TotalFiles,
    int SucceededFiles,
    int FailedFiles,
    TimeSpan Duration,
    string? ErrorMessage);
