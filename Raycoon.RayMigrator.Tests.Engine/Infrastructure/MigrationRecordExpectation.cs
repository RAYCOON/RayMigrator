
namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Expected values for a single Migration record in the repository.
/// Only non-null fields are asserted, allowing partial matching.
/// </summary>
public record MigrationRecordExpectation
{
    public int? MigrationStatusId { get; init; }
    public int? MigrationOperationId { get; init; }
    public int? EnvironmentId { get; init; }
    public string? ReleaseVersion { get; init; }
    public string? TargetGroupAlias { get; init; }
    public string? TargetAlias { get; init; }
    public int? FileOrderId { get; init; }
    public int? FileUpBlocksMigrated { get; init; }
    public int? FileUpBlocksTotal { get; init; }
    public bool? MigrateDownFileExists { get; init; }
    public int? FileDownBlocksMigrated { get; init; }
    public int? FileDownBlocksTotal { get; init; }
}
