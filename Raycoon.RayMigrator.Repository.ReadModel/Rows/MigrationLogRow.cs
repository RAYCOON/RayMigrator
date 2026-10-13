using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>One structured log entry of a run, with the run, record, file and block it belongs to where known.</summary>
public sealed record MigrationLogRow(
    long Id,
    LogLevel LogLevelId,
    int? MigrationEventId,
    MigrationRunMode? RunModeId,
    int? ProductId,
    int? EnvironmentId,
    int? MigrationRunId,
    int? MigrationRecordId,
    string? ReleaseVersion,
    string? TargetGroupAlias,
    string? TargetAlias,
    string? Filename,
    int? FileOrderId,
    int? FileBlockId,
    string? Message,
    DateTime CreatedAt);
