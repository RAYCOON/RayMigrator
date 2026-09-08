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

    // Terminal state transitions per run (MigrationRecordHistory): the info run history is built from it, because
    // migrate-down and error-recovery rollbacks change records that belong to another run (#13)
    Repository_MigrationRecordHistory_Select,

    // Read-only lookups (no insert): used by run modes / commands that must not write to the repository (#7)
    Repository_Product_Select,
    Repository_Environment_Select,
}
