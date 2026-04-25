namespace Raycoon.RayMigrator.Core.Templates;

public enum TemplateType
{
    Undefined,

    Repository_Product_CheckInsert,
    Repository_Environment_CheckInsert,

    DatabaseLogging_CheckCreate,
    DatabaseLogging_Insert,

    Repository_CheckCreate,
    Repository_Drop,

    Repository_MigrationRun_Insert,
    Repository_MigrationRun_Update,
    Repository_MigrationRun_Select,
    Repository_MigrationRun_SelectOrphaned,
    Repository_MigrationRun_FixOrphaned,

    Repository_MigrationRecord_Insert,
    Repository_MigrationRecord_Update,
    Repository_MigrationRecord_UpdateHash,
    Repository_MigrationRecord_UpdateRollback,
    Repository_MigrationRecord_Select,
    Repository_MigrationRecord_GetInterrupted,
    Repository_MigrationRecord_FixOrphaned,
}
