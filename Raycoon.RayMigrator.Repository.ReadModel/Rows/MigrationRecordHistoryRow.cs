using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// A terminal state of a migration record (Migrated, NotMigrated or Failed), copied when the record reached it, with the
/// run that caused the transition.
/// </summary>
public sealed record MigrationRecordHistoryRow(
    int Id,
    int MigrationRecordId,
    int ProductId,
    int EnvironmentId,
    int MigrationRunId,
    MigrationRunMode MigrationRunModeId,
    MigrationOperation MigrationOperationId,
    MigrationStatus MigrationStatusId,
    string ReleaseVersion,
    string TargetGroupAlias,
    string TargetAlias,
    string Filename,
    int FileOrderId,
    string FileUpHash,
    string? FileUpConfigHash,
    string FileUpBlocksHash,
    int FileUpBlocksMigrated,
    int FileUpBlocksTotal,
    string? FileUpConfigJson,
    bool MigrateDownFileExists,
    string? FileDownHash,
    string? FileDownConfigHash,
    string? FileDownBlocksHash,
    int? FileDownBlocksMigrated,
    int? FileDownBlocksTotal,
    string? FileDownConfigJson,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    long? DurationInMs,
    DateTime HistorizedAt);
