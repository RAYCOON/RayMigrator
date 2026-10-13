namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// The tables of a RayMigrator repository at <see cref="RepositoryReadModel.SchemaVersion"/>, the log tables included,
/// because database logging goes into the repository by default. <see cref="RepositoryNaming"/> turns a table into
/// the name an engine uses, <see cref="RepositoryRows"/> names the record type of its rows.
/// </summary>
public enum RepositoryTable
{
    RepositorySchemaVersion,
    MigratorMeta,
    Product,
    Environment,
    MigrationRun,
    MigrationRunMeta,
    MigrationRecord,
    MigrationRecordHistory,
    MigrationRunMode,
    MigrationOperation,
    MigrationRunResult,
    MigrationStatus,
    MigrationRunSource,
    MigrationEvent,
    MigrationLog,
}
