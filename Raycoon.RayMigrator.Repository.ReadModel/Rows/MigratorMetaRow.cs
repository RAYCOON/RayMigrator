namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>One RayMigrator version that has used the repository; every run references its row.</summary>
public sealed record MigratorMetaRow(int Id, string RayMigratorVersion, string RepositoryDatabaseType, DateTime CreatedAt);
