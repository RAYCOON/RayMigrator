/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_3"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-16.1"

[Description]
Function = """
Schema version 3 (ADR-024): adds the run result Cancelled = 70 to the migration_run_result lookup
table, the indexes the readers of the repository filter by (runs per product, environment and
start, history rows per run and per time, records per product and environment), and writes
row 3 of the repository_schema_version table.
"""

Behaviour = """
- Return value 3: schema version 3 is applied (created in this run or already present)
- Return value < 0: Error (logged at Error level, command aborted)
- Idempotent: every row and index is created only when missing; one transaction (the DO block), serialised with an advisory lock
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase (DAL-017)"

[Parameters]
RayMigratorVersion = "TEXT | REQUIRED | The RayMigrator version applying the step (e.g., '0.20.0')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_3  = "3,RayMigrator repository schema version [3] applied: migration_run_result cancelled and the reader indexes"

[ModificationNotes]
Note1 = "Result format: 'code,message' via RAISE NOTICE - DO NOT change this format; the code must equal the step number"
Note2 = "No commas allowed in messages"
Note3 = "The lookup row and the indexes are data and access paths only: rows of schema version 2 stay as they are"
Note4 = "The longest index name has 47 characters plus the prefix: with the longest prefix RULE_4_4 allows (16 bytes) it uses the 63 bytes PostgreSQL keeps"
Note5 = "A later schema version is a new Repository_Upgrade_{n} template; this file never changes again"
Note6 = "DAL-017: All identifiers use unquoted snake_case"
================================================================================
*/

DO $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtext('RayMigrator_{CFG:SchemaName}_{CFG:TableBaseName}'));

    IF NOT EXISTS (SELECT 1 FROM {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version WHERE version = 3) THEN

        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result (id, name, description)
        VALUES (70, 'Cancelled', 'Migration(s) stopped on request before completion')
        ON CONFLICT (id) DO NOTHING;

        COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result IS 'Running = 10, PartialSuccess = 50, Cancelled = 70, Recovered = 80, Error = 90, Ok = 100';

        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_run_product_environment_started_at
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (product_id, environment_id, started_at);
        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_record_history_migration_run_id
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history (migration_run_id);
        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_record_history_historized_at
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history (historized_at);
        CREATE INDEX IF NOT EXISTS ix_{CFG:TableBaseName}migration_record_product_environment
            ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (product_id, environment_id);

        INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}repository_schema_version (version, applied_at, raymigrator_version)
        VALUES (3, NOW(), @RayMigratorVersion)
        ON CONFLICT (version) DO NOTHING;

    END IF;

    RAISE NOTICE '3,RayMigrator repository schema version [3] applied: migration_run_result cancelled and the reader indexes';
END $$;
