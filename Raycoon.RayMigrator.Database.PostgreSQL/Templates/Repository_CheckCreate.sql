/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "PostgreSQL"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"

[Description]
Function = """
Checks for repository existence and completeness. Creates RayMigrator
infrastructure on the target database if necessary. Returns the VersionId.
"""

Behaviour = """
- Return value >= 0: Success (logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- Creates schema if not exists
- Creates all 11 repository tables with master data
- Inserts new MigratorMeta record on first run or version change
"""

[ConfigPlaceholders]
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_') - MUST be lowercase for PostgreSQL (DAL-017)"

[Parameters]
RayMigratorVersion     = "VARCHAR(20) | REQUIRED | The RayMigrator application version (e.g., '3.0.0')"
RepositoryDatabaseType = "VARCHAR(20) | REQUIRED | The database type for the repository (e.g., 'PostgreSQL')"

[ReturnValues]
# Format: SELECT 'code,message'
Success_N           = "N (VersionId),RayMigrator repository already exists. Using VersionId [N]."
Success_N_Created   = "N (VersionId),RayMigrator repository-tables with master data and new VersionId [N] successfully created"
Success_N_NewVer    = "N (VersionId),RayMigrator repository already exists. New VersionId [N] created."
Error_-10_Incomplete        = "-10,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of [11]."
Error_-11_PartialNoVersion  = "-11,RayMigrator repository incomplete or corrupt. Repository contains [X] tables instead of the expected amount of [0]."
Error_-12_MultipleVersions  = "-12,Multiple [migrator_meta]-entries found for RepositoryVersion [...] RepositoryDatabaseType [...] RayMigratorVersion [...]."

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use NOW() for all audit timestamps (writes to TIMESTAMPTZ columns)"
Note4 = "RepositoryVersion constant MUST match Version in header"
Note5 = "Tables created: migrator_meta, product, environment, migration_run, migration_run_meta, migration_record, migration_record_history, migration_run_mode, migration_operation, migration_run_result, migration_status"
Note6 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
Note7 = "DAL-017: All identifiers (tables, columns, constraints, indexes) use unquoted snake_case per PostgreSQL community convention"
================================================================================
*/

DO $$
DECLARE
    v_repository_version VARCHAR(20) := '2026-04-18.1';
    v_version_id INT;
    v_version_id_string VARCHAR(10);
    v_number_of_rows INT;
    v_number_of_tables_found INT;
BEGIN
    SELECT COUNT(*) INTO v_number_of_tables_found
    FROM information_schema.tables
    WHERE table_schema = '{CFG:SchemaName}'
      AND table_name IN (
          '{CFG:TableBaseName}migrator_meta',
          '{CFG:TableBaseName}product',
          '{CFG:TableBaseName}migration_run',
          '{CFG:TableBaseName}migration_run_meta',
          '{CFG:TableBaseName}migration_record',
          '{CFG:TableBaseName}migration_record_history',
          '{CFG:TableBaseName}migration_run_mode',
          '{CFG:TableBaseName}migration_operation',
          '{CFG:TableBaseName}migration_run_result',
          '{CFG:TableBaseName}migration_status',
          '{CFG:TableBaseName}environment'
      );

    -- Check for migrator_meta table existence (repository exists)
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = '{CFG:SchemaName}'
          AND table_name = '{CFG:TableBaseName}migrator_meta'
    ) THEN
        -- Check for repository completeness
        IF v_number_of_tables_found != 11 THEN
            RAISE EXCEPTION '-10,RayMigrator repository incomplete or corrupt. Repository contains [%] tables instead of [11].', v_number_of_tables_found;
        END IF;

        -- Try to get VersionId
        SELECT id INTO v_version_id
        FROM {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
        WHERE repository_version = v_repository_version
          AND repository_database_type = @RepositoryDatabaseType
          AND created_by_raymigrator_version = @RayMigratorVersion;

        GET DIAGNOSTICS v_number_of_rows = ROW_COUNT;

        IF v_number_of_rows = 1 THEN
            v_version_id_string := CAST(v_version_id AS VARCHAR(10));
            RAISE NOTICE '%,RayMigrator repository already exists. Using VersionId [%].', v_version_id_string, v_version_id_string;
            RETURN;
        ELSIF v_number_of_rows = 0 THEN
            INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
                (repository_version, repository_database_type, created_by_raymigrator_version, created_at)
            VALUES
                (v_repository_version, @RepositoryDatabaseType, @RayMigratorVersion, NOW())
            RETURNING id INTO v_version_id;

            v_version_id_string := CAST(v_version_id AS VARCHAR(10));
            RAISE NOTICE '%,RayMigrator repository already exists. New VersionId [%] created.', v_version_id_string, v_version_id_string;
            RETURN;
        ELSE
            RAISE EXCEPTION '-12,Multiple [migrator_meta]-entries found for RepositoryVersion [%] RepositoryDatabaseType [%] RayMigratorVersion [%].',
                COALESCE(v_repository_version, 'NULL'), COALESCE(@RepositoryDatabaseType, 'NULL'), COALESCE(@RayMigratorVersion, 'NULL');
        END IF;
    END IF;

    -- No migrator_meta table found. Check for stale tables
    IF v_number_of_tables_found != 0 THEN
        RAISE EXCEPTION '-11,RayMigrator repository incomplete or corrupt. Repository contains [%] tables instead of the expected amount of [0].', v_number_of_tables_found;
    END IF;

    -- Create schema if not exists
    CREATE SCHEMA IF NOT EXISTS {CFG:SchemaName};

    -- Create repository tables
    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_operation (
        id                     SMALLINT       NOT NULL,
        name                   TEXT           NOT NULL,
        description            TEXT               NULL,
        CONSTRAINT pk_migration_operation PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result (
        id                     SMALLINT       NOT NULL,
        name                   TEXT           NOT NULL,
        description            TEXT               NULL,
        CONSTRAINT pk_migration_run_result PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode (
        id                     SMALLINT       NOT NULL,
        name                   TEXT           NOT NULL,
        description            TEXT               NULL,
        CONSTRAINT pk_migration_run_mode PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_status (
        id                     SMALLINT       NOT NULL,
        name                   TEXT           NOT NULL,
        description            TEXT               NULL,
        CONSTRAINT pk_migration_status PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta (
        id                             INT        GENERATED ALWAYS AS IDENTITY NOT NULL,
        repository_version             TEXT       NOT NULL,
        repository_database_type       TEXT       NOT NULL,
        created_by_raymigrator_version TEXT      NOT NULL,
        created_at                     TIMESTAMPTZ NOT NULL,
        CONSTRAINT pk_migrator_meta PRIMARY KEY (id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}product (
        id                     INT            GENERATED ALWAYS AS IDENTITY NOT NULL,
        name                   TEXT           NOT NULL,
        name_lower             TEXT           NOT NULL,
        created_at             TIMESTAMPTZ    NOT NULL,
        CONSTRAINT pk_product PRIMARY KEY (id)
    );

    CREATE UNIQUE INDEX uix_{CFG:TableBaseName}product_name_lower ON {CFG:SchemaName}.{CFG:TableBaseName}product (name_lower);

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}environment (
        id                     INT            GENERATED ALWAYS AS IDENTITY NOT NULL,
        name                   TEXT           NOT NULL,
        name_lower             TEXT           NOT NULL,
        created_at             TIMESTAMPTZ    NOT NULL,
        CONSTRAINT pk_environment PRIMARY KEY (id)
    );

    CREATE UNIQUE INDEX uix_{CFG:TableBaseName}environment_name_lower ON {CFG:SchemaName}.{CFG:TableBaseName}environment (name_lower);

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run (
        id                     INT            GENERATED ALWAYS AS IDENTITY NOT NULL,
        migrator_meta_id       INT            NOT NULL,
        product_id             INT            NOT NULL,
        environment_id         INT            NOT NULL,
        migration_run_mode_id  SMALLINT       NOT NULL,
        migration_run_result_id SMALLINT      NOT NULL,
        from_release_version   TEXT               NULL,
        to_release_version     TEXT               NULL,
        started_at             TIMESTAMPTZ    NOT NULL,
        finished_at            TIMESTAMPTZ        NULL,
        duration_in_ms         BIGINT             NULL,
        CONSTRAINT pk_migration_run PRIMARY KEY (id)
    );

    CREATE INDEX ix_{CFG:TableBaseName}migration_run_migrator_meta_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (migrator_meta_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_run_product_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (product_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_run_environment_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (environment_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_run_migration_run_mode_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (migration_run_mode_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_run_migration_run_result_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_run (migration_run_result_id);

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_meta (
        migration_run_id             INT            NOT NULL,
        migration_run_settings_json  TEXT               NULL,
        description                  TEXT               NULL,
        CONSTRAINT pk_migration_run_meta PRIMARY KEY (migration_run_id)
    );

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record (
        id                        INT            GENERATED ALWAYS AS IDENTITY NOT NULL,
        product_id                INT            NOT NULL,
        environment_id            INT            NOT NULL,
        migration_run_id          INT            NOT NULL,
        migration_run_mode_id     SMALLINT       NOT NULL,
        migration_operation_id    SMALLINT       NOT NULL,
        migration_status_id       SMALLINT       NOT NULL,
        release_version           TEXT           NOT NULL,
        target_group_alias        TEXT           NOT NULL,
        target_alias              TEXT           NOT NULL,
        filename                  TEXT           NOT NULL,
        file_order_id             INT            NOT NULL,
        file_up_hash              TEXT           NOT NULL,
        file_up_config_hash       TEXT               NULL,
        file_up_blocks_hash       TEXT           NOT NULL,
        file_up_blocks_migrated   INT            NOT NULL,
        file_up_blocks_total      INT            NOT NULL,
        file_up_config_json       TEXT               NULL,
        migrate_down_file_exists  BOOLEAN        NOT NULL,
        file_down_hash            TEXT               NULL,
        file_down_config_hash     TEXT               NULL,
        file_down_blocks_hash     TEXT               NULL,
        file_down_blocks_migrated INT                NULL,
        file_down_blocks_total    INT                NULL,
        file_down_config_json     TEXT               NULL,
        started_at                TIMESTAMPTZ        NULL,
        finished_at               TIMESTAMPTZ        NULL,
        duration_in_ms            BIGINT             NULL,
        CONSTRAINT pk_migration_record PRIMARY KEY (id)
    );

    CREATE INDEX ix_{CFG:TableBaseName}migration_record_product_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (product_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_record_environment_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (environment_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_record_migration_run_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (migration_run_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_record_migration_run_mode_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (migration_run_mode_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_record_migration_operation_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (migration_operation_id);
    CREATE INDEX ix_{CFG:TableBaseName}migration_record_migration_status_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record (migration_status_id);

    CREATE TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history (
        id                        INT            GENERATED ALWAYS AS IDENTITY NOT NULL,
        migration_record_id       INT            NOT NULL,
        product_id                INT            NOT NULL,
        environment_id            INT            NOT NULL,
        migration_run_id          INT            NOT NULL,
        migration_run_mode_id     SMALLINT       NOT NULL,
        migration_operation_id    SMALLINT       NOT NULL,
        migration_status_id       SMALLINT       NOT NULL,
        release_version           TEXT           NOT NULL,
        target_group_alias        TEXT           NOT NULL,
        target_alias              TEXT           NOT NULL,
        filename                  TEXT           NOT NULL,
        file_order_id             INT            NOT NULL,
        file_up_hash              TEXT           NOT NULL,
        file_up_config_hash       TEXT               NULL,
        file_up_blocks_hash       TEXT           NOT NULL,
        file_up_blocks_migrated   INT            NOT NULL,
        file_up_blocks_total      INT            NOT NULL,
        file_up_config_json       TEXT               NULL,
        migrate_down_file_exists  BOOLEAN        NOT NULL,
        file_down_hash            TEXT               NULL,
        file_down_config_hash     TEXT               NULL,
        file_down_blocks_hash     TEXT               NULL,
        file_down_blocks_migrated INT                NULL,
        file_down_blocks_total    INT                NULL,
        file_down_config_json     TEXT               NULL,
        started_at                TIMESTAMPTZ        NULL,
        finished_at               TIMESTAMPTZ        NULL,
        duration_in_ms            BIGINT             NULL,
        historized_at             TIMESTAMPTZ    NOT NULL  DEFAULT NOW(),
        CONSTRAINT pk_migration_record_history PRIMARY KEY (id)
    );

    CREATE INDEX ix_{CFG:TableBaseName}migration_record_history_migration_record_id
        ON {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history (migration_record_id);

    -- Foreign keys
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_product FOREIGN KEY (product_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}product(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_environment FOREIGN KEY (environment_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}environment(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_migration_run_mode FOREIGN KEY (migration_run_mode_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_migration_operation FOREIGN KEY (migration_operation_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_operation(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record ADD CONSTRAINT fk_migration_record_migration_status FOREIGN KEY (migration_status_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_status(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history ADD CONSTRAINT fk_migration_record_history_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history ADD CONSTRAINT fk_migration_record_history_product FOREIGN KEY (product_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}product(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history ADD CONSTRAINT fk_migration_record_history_environment FOREIGN KEY (environment_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}environment(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_product FOREIGN KEY (product_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}product(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_environment FOREIGN KEY (environment_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}environment(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_migration_run_result FOREIGN KEY (migration_run_result_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_migration_run_mode FOREIGN KEY (migration_run_mode_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run ADD CONSTRAINT fk_migration_run_migrator_meta FOREIGN KEY (migrator_meta_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta(id) ON DELETE NO ACTION ON UPDATE NO ACTION;
    ALTER TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_meta ADD CONSTRAINT fk_migration_run_meta_migration_run FOREIGN KEY (migration_run_id) REFERENCES {CFG:SchemaName}.{CFG:TableBaseName}migration_run(id) ON DELETE NO ACTION ON UPDATE NO ACTION;

    -- Table and column comments (equivalent to SQL Server extended properties)
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_operation IS 'Rollback (MigrateDown) = 5, MigrateDown = 50, MigrateUp = 100';
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result IS 'Running = 10, Error = 90, Ok = 100';
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode IS 'Validate = 10, Simulate = 20, Migrate = 100';
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_status IS 'Pending = 10 (Initial insert), Executing = 20 (Block-level execution in progress), Failed = 30 (Migration failed), NotMigrated = 50 (Not yet performed / RolledBack / Skipped / Ignored), Migrated = 100 (MigrateUp successful)';
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record IS 'Represents all migration-files found at time of last migration attempt';
    COMMENT ON TABLE {CFG:SchemaName}.{CFG:TableBaseName}migration_record_history IS 'Represents all migration-files found at time of last migration attempt';

    -- Master data
    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run_mode (id, name, description)
    VALUES
        (10, 'Validate', 'Validates configuration and all migration files. Does NOT perform actual migration against target databases.'),
        (20, 'Simulate', 'Validates configuration and all migration files. Simulates the entire migration process. Does NOT perform actual migrations against target databases.'),
        (100, 'Migrate', 'Validates configuration and all migration files. Performs actual migrations against target databases.');

    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_operation (id, name, description)
    VALUES
        (5, 'Rollback', 'Performing Rollback of current MigrationRun'),
        (50, 'MigrateDown', 'Performing Down-Migration'),
        (100, 'MigrateUp', 'Performing Up-Migration');

    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_run_result (id, name, description)
    VALUES
        (10, 'Running', 'Migration process is currently running'),
        (90, 'Error', 'Migration(s) stopped due to error(s)'),
        (100, 'Ok', 'Migration(s) successfully executed');

    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migration_status (id, name, description)
    VALUES
        (10, 'Pending', 'Initial insert before migration execution begins'),
        (20, 'Executing', 'Block-level execution in progress'),
        (30, 'Failed', 'Migration failed with error'),
        (50, 'NotMigrated', 'Migration not yet performed, RolledBack, Skipped or Ignored'),
        (100, 'Migrated', 'MigrateUp successful');

    -- Create VersionId
    INSERT INTO {CFG:SchemaName}.{CFG:TableBaseName}migrator_meta
        (repository_version, repository_database_type, created_by_raymigrator_version, created_at)
    VALUES
        (v_repository_version, @RepositoryDatabaseType, @RayMigratorVersion, NOW())
    RETURNING id INTO v_version_id;

    v_version_id_string := CAST(v_version_id AS VARCHAR(10));
    RAISE NOTICE '%,RayMigrator repository-tables with master data and new VersionId [%] successfully created', v_version_id_string, v_version_id_string;
END $$;
