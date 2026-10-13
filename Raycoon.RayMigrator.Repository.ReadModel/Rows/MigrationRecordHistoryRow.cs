using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>A former state of a migration record, copied before the record changed.</summary>
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
