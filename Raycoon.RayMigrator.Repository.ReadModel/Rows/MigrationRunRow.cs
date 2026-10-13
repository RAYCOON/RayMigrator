using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// One run of a command that writes the repository. The columns of schema version 2 (<see cref="MigrationOperationId"/>
/// onwards) are null for a run an engine before 0.17.0 wrote.
/// </summary>
public sealed record MigrationRunRow(
    int Id,
    int MigratorMetaId,
    int ProductId,
    int EnvironmentId,
    MigrationRunMode MigrationRunModeId,
    MigrationRunResult MigrationRunResultId,
    string? FromReleaseVersion,
    string? ToReleaseVersion,
    DateTime StartedAt,
    DateTime? FinishedAt,
    long? DurationInMs,
    MigrationOperation? MigrationOperationId,
    MigrationRunSource? MigrationRunSourceId,
    string? InitiatedBy,
    string? InitiatedBySubject,
    string? Host,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? LastHeartbeatAt);
