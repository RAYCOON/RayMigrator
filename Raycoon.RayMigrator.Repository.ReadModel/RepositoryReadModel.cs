using Raycoon.RayMigrator.Core.Templates;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// The contract a reader of the repository builds on: the schema version this package describes and the tables it
/// knows. The engine of the same release creates exactly this schema (<see cref="RepositorySchema.CurrentVersion"/>);
/// a reader compares <see cref="SchemaVersion"/> with the highest <c>Version</c> in <c>RepositorySchemaVersion</c>
/// before it trusts the names and types in this package.
/// </summary>
public static class RepositoryReadModel
{
    /// <summary>The repository schema version the record types and column names of this package describe.</summary>
    public const int SchemaVersion = RepositorySchema.CurrentVersion;

    /// <summary>Every table of the repository, in creation order.</summary>
    public static IReadOnlyList<RepositoryTable> Tables { get; } = Enum.GetValues<RepositoryTable>();
}
