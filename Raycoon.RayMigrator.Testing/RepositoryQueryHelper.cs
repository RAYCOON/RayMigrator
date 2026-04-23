// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Testing;

/// <summary>
/// Provides direct SQL queries against the repository and target databases for test assertions.
/// </summary>
public class RepositoryQueryHelper
{
    private readonly string _databaseType;
    private readonly string _repositoryConnectionString;
    private readonly string _schemaName;
    private readonly DalSpecificProperties? _dalProps;

    private static readonly DalSettings QuerySettings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    public RepositoryQueryHelper(string databaseType, string repositoryConnectionString, string schemaName)
    {
        _databaseType = databaseType;
        _repositoryConnectionString = repositoryConnectionString;
        _schemaName = schemaName;

        if (DalFactory.TryGetDal(databaseType, repositoryConnectionString, out IDal? dal))
        {
            _dalProps = dal!.DalSpecificProperties;
        }
    }

    /// <summary>
    /// Counts rows in a table within the repository database.
    /// </summary>
    public int CountRows(string tableName)
    {
        return CountRows(_repositoryConnectionString, tableName);
    }

    /// <summary>
    /// Counts rows in a table using a specific connection string.
    /// Set useRepositorySchema=false for user-created tables (dbo/public schema).
    /// </summary>
    public int CountRows(string connectionString, string tableName, bool useRepositorySchema = true)
    {
        if (!DalFactory.TryGetDal(_databaseType, connectionString, out IDal? dal))
            return -1;

        string qualifiedTable = useRepositorySchema ? GetQualifiedTableName(tableName) : GetUnqualifiedTableName(tableName);
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1; // Table doesn't exist
        }
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    public bool TableExists(string tableName)
    {
        return TableExists(_repositoryConnectionString, tableName);
    }

    /// <summary>
    /// Checks if a table exists in a specific database.
    /// Set useRepositorySchema=false for user-created tables (dbo/public schema).
    /// </summary>
    public bool TableExists(string connectionString, string tableName, bool useRepositorySchema = true)
    {
        if (!DalFactory.TryGetDal(_databaseType, connectionString, out IDal? dal))
            return false;

        string schemaForQuery = useRepositorySchema ? _schemaName : GetDefaultSchema();
        // PostgreSQL stores unquoted identifiers as lowercase.
        // - User tables (useRepositorySchema=false) are simple lowercase.
        // - Repository tables (useRepositorySchema=true) use snake_case after DAL-017.
        // MariaDB/MySQL repository tables use snake_case after DAL-018.
        string tableNameForQuery = _databaseType switch
        {
            "PostgreSQL" when useRepositorySchema  => ToSnakeCase(tableName),
            "PostgreSQL" when !useRepositorySchema => tableName.ToLowerInvariant(),
            "MariaDb" or "MySql" when useRepositorySchema => ToSnakeCase(tableName),
            _                                      => tableName
        };
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT CASE WHEN OBJECT_ID('{schemaForQuery}.{tableNameForQuery}', 'U') IS NOT NULL THEN 1 ELSE 0 END",
            "PostgreSQL" => $"SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{schemaForQuery}' AND table_name = '{tableNameForQuery}') THEN 1 ELSE 0 END",
            "MariaDb" or "MySql" => $"SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{tableNameForQuery}') THEN 1 ELSE 0 END",
            "Sqlite" => $"SELECT CASE WHEN EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableNameForQuery}') THEN 1 ELSE 0 END",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result) == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the count of MigrationRecordHistory records in the repository.
    /// </summary>
    public int CountMigrationHistory()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecordHistory");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the count of MigrationRecord entries with a specific MigrationStatus.
    /// </summary>
    public int CountMigrationsWithStatus(int statusId)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {QuoteColumn("MigrationStatusId")} = {statusId}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the MigrationRunResult (ResultId) from the latest MigrationRun.
    /// </summary>
    public int GetLatestMigrationRunResultId()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string col = QuoteColumn("MigrationRunResultId");
        string id = QuoteColumn("Id");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {qualifiedTable} ORDER BY {id} DESC",
            _ => $"SELECT {col} FROM {qualifiedTable} ORDER BY {id} DESC LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the count of MigrationLog entries.
    /// </summary>
    public int CountLogEntries()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationLog");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the count of MigrationLog entries at a specific log level.
    /// </summary>
    public int CountLogEntriesAtLevel(int logLevelId)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationLog");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {QuoteColumn("LogLevelId")} = {logLevelId}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the MigrationOperationId from the latest MigrationRun.
    /// </summary>
    public int GetLatestMigrationRunOperation()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string col = QuoteColumn("MigrationOperationId");
        string id = QuoteColumn("Id");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {qualifiedTable} ORDER BY {id} DESC",
            _ => $"SELECT {col} FROM {qualifiedTable} ORDER BY {id} DESC LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the FileUpConfigJson for a MigrationRecord entry matching the given filename.
    /// </summary>
    public string? GetMigrationConfigJson(string filename)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return null;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string col = QuoteColumn("FileUpConfigJson");
        string fileCol = QuoteColumn("Filename");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}'",
            _ => $"SELECT {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}' LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Counts MigrationRecord entries matching the given filename.
    /// Returns 0 if the file was filtered out (e.g., Environments mismatch).
    /// </summary>
    public int CountMigrationsByFilename(string filename)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string fileCol = QuoteColumn("Filename");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {fileCol} = '{filename}'";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Counts MigrationRecord entries belonging to a specific target group.
    /// </summary>
    public int CountMigrationsForTargetGroup(string targetGroupAlias)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string col = QuoteColumn("TargetGroupAlias");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {col} = '{targetGroupAlias}'";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Checks if a Product record exists with the given alias (case-insensitive via NameLower).
    /// </summary>
    public bool ProductExists(string productAlias)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return false;

        string qualifiedTable = GetQualifiedTableName("Product");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {QuoteColumn("NameLower")} = '{productAlias.ToLowerInvariant()}'";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an Environment record exists with the given name (case-insensitive via NameLower).
    /// </summary>
    public bool EnvironmentExists(string environmentName)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return false;

        string qualifiedTable = GetQualifiedTableName("Environment");
        string sql = $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {QuoteColumn("NameLower")} = '{environmentName.ToLowerInvariant()}'";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the ProductId for a given product alias from the Product table (case-insensitive via NameLower).
    /// </summary>
    public int GetProductId(string productAlias)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("Product");
        string sql = $"SELECT {QuoteColumn("Id")} FROM {qualifiedTable} WHERE {QuoteColumn("NameLower")} = '{productAlias.ToLowerInvariant()}'";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the MigrationRunSettingsJson from the latest MigrationRunMeta entry.
    /// </summary>
    public string? GetLatestMigrationRunSettingsJson()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return null;

        string metaTable = GetQualifiedTableName("MigrationRunMeta");
        string col = QuoteColumn("MigrationRunSettingsJson");
        string idCol = QuoteColumn("MigrationRunId");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {metaTable} ORDER BY {idCol} DESC",
            _ => $"SELECT {col} FROM {metaTable} ORDER BY {idCol} DESC LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Inserts a fake MigrationRun record with MigrationRunResultId=10 (Running) and FinishedAt=NULL.
    /// Used to simulate a running migration for guard tests.
    /// </summary>
    public void InsertRunningMigrationRun(int productId, string environment = "Docker")
    {
        int environmentId = EnsureEnvironmentExists(environment);
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            throw new InvalidOperationException($"Could not create DAL for {_databaseType}");

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string timestampSql = _databaseType switch
        {
            "SqlServer" => "SYSUTCDATETIME()",
            "PostgreSQL" => "(NOW() AT TIME ZONE 'UTC')",
            "MariaDb" or "MySql" => "UTC_TIMESTAMP()",
            "Sqlite" => "datetime('now')",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        string sql = _databaseType switch
        {
            "SqlServer" =>
                $"INSERT INTO {qualifiedTable} ([MigratorMetaId], [ProductId], [EnvironmentId], [MigrationRunModeId], [MigrationRunResultId], [StartedAt]) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            // DAL-017: PostgreSQL columns are unquoted snake_case.
            "PostgreSQL" =>
                $"INSERT INTO {qualifiedTable} (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id, started_at) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            // DAL-018: MariaDB/MySQL columns are snake_case.
            "MariaDb" or "MySql" =>
                $"INSERT INTO {qualifiedTable} (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id, started_at) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            "Sqlite" =>
                $"INSERT INTO {qualifiedTable} (MigratorMetaId, ProductId, EnvironmentId, MigrationRunModeId, MigrationRunResultId, StartedAt) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Inserts a fake MigrationRun record with MigrationRunResultId=10 (Running) and a StartedAt
    /// timestamp in the past, simulating an orphaned migration run that crashed without completing.
    /// </summary>
    public void InsertOrphanedMigrationRun(int productId, int minutesOld, string environment = "Docker")
    {
        int environmentId = EnsureEnvironmentExists(environment);
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            throw new InvalidOperationException($"Could not create DAL for {_databaseType}");

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string timestampSql = _databaseType switch
        {
            "SqlServer" => $"DATEADD(MINUTE, -{minutesOld}, SYSUTCDATETIME())",
            "PostgreSQL" => $"(NOW() AT TIME ZONE 'UTC') - INTERVAL '{minutesOld} minutes'",
            "MariaDb" or "MySql" => $"UTC_TIMESTAMP() - INTERVAL {minutesOld} MINUTE",
            "Sqlite" => $"datetime('now', '-{minutesOld} minutes')",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        string sql = _databaseType switch
        {
            "SqlServer" =>
                $"INSERT INTO {qualifiedTable} ([MigratorMetaId], [ProductId], [EnvironmentId], [MigrationRunModeId], [MigrationRunResultId], [StartedAt]) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            // DAL-017: PostgreSQL columns are unquoted snake_case.
            "PostgreSQL" =>
                $"INSERT INTO {qualifiedTable} (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id, started_at) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            // DAL-018: MariaDB/MySQL columns are snake_case.
            "MariaDb" or "MySql" =>
                $"INSERT INTO {qualifiedTable} (migrator_meta_id, product_id, environment_id, migration_run_mode_id, migration_run_result_id, started_at) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            "Sqlite" =>
                $"INSERT INTO {qualifiedTable} (MigratorMetaId, ProductId, EnvironmentId, MigrationRunModeId, MigrationRunResultId, StartedAt) " +
                $"VALUES (1, {productId}, {environmentId}, 100, 10, {timestampSql})",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Ensures a row exists in the Environment lookup table for the given name and returns its Id.
    /// Used by the orphan/running-guard test helpers to materialize the EnvironmentId FK value.
    /// </summary>
    private int EnsureEnvironmentExists(string environment)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            throw new InvalidOperationException($"Could not create DAL for {_databaseType}");

        string qualifiedTable = GetQualifiedTableName("Environment");
        string lower = environment.ToLowerInvariant();
        string timestampSql = _databaseType switch
        {
            "SqlServer" => "SYSUTCDATETIME()",
            "PostgreSQL" => "(NOW() AT TIME ZONE 'UTC')",
            "MariaDb" or "MySql" => "UTC_TIMESTAMP()",
            "Sqlite" => "datetime('now')",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        string selectSql = _databaseType switch
        {
            "SqlServer" => $"SELECT [Id] FROM {qualifiedTable} WHERE [NameLower] = '{lower}'",
            "PostgreSQL" or "MariaDb" or "MySql" => $"SELECT id FROM {qualifiedTable} WHERE name_lower = '{lower}'",
            "Sqlite" => $"SELECT \"Id\" FROM {qualifiedTable} WHERE \"NameLower\" = '{lower}'",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        var existing = dal!.ExecuteScalarAsync(selectSql, QuerySettings, null).GetAwaiter().GetResult();
        if (existing != null && int.TryParse(existing.ToString(), out int existingId) && existingId > 0)
            return existingId;

        string insertSql = _databaseType switch
        {
            "SqlServer" =>
                $"INSERT INTO {qualifiedTable} ([Name], [NameLower], [CreatedAt]) VALUES ('{environment}', '{lower}', {timestampSql}); SELECT CAST(SCOPE_IDENTITY() AS INT);",
            "PostgreSQL" =>
                $"INSERT INTO {qualifiedTable} (name, name_lower, created_at) VALUES ('{environment}', '{lower}', {timestampSql}) RETURNING id;",
            "MariaDb" or "MySql" =>
                $"INSERT INTO {qualifiedTable} (name, name_lower, created_at) VALUES ('{environment}', '{lower}', {timestampSql}); SELECT LAST_INSERT_ID();",
            "Sqlite" =>
                $"INSERT INTO {qualifiedTable} (\"Name\", \"NameLower\", \"CreatedAt\") VALUES ('{environment}', '{lower}', {timestampSql}); SELECT last_insert_rowid();",
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };

        var inserted = dal.ExecuteScalarAsync(insertSql, QuerySettings, null).GetAwaiter().GetResult();
        return Convert.ToInt32(inserted);
    }

    /// <summary>
    /// Gets the MigrationStatusId for a MigrationRecord entry matching the given filename.
    /// Returns -1 if the record is not found.
    /// </summary>
    public int GetMigrationStatusByFilename(string filename)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string col = QuoteColumn("MigrationStatusId");
        string fileCol = QuoteColumn("Filename");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}'",
            _ => $"SELECT {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}' LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the MigrationStatusId for a specific filename and target alias.
    /// Used for multi-target scenarios where the same file may have different statuses per target.
    /// Returns -1 if no record found.
    /// </summary>
    public int GetMigrationStatusByFilenameAndTarget(string filename, string targetAlias)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string col = QuoteColumn("MigrationStatusId");
        string fileCol = QuoteColumn("Filename");
        string targetCol = QuoteColumn("TargetAlias");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}' AND {targetCol} = '{targetAlias}'",
            _ => $"SELECT {col} FROM {qualifiedTable} WHERE {fileCol} = '{filename}' AND {targetCol} = '{targetAlias}' LIMIT 1"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the count of MigrationRun entries.
    /// </summary>
    public int CountMigrationRuns(int? migrationRunResultId = null)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string sql = migrationRunResultId.HasValue
            ? $"SELECT COUNT(*) FROM {qualifiedTable} WHERE {QuoteColumn("MigrationRunResultId")} = {migrationRunResultId.Value}"
            : $"SELECT COUNT(*) FROM {qualifiedTable}";

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return Convert.ToInt32(result);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the MigrationRunResultId for a specific MigrationRun by index (1-based, oldest first).
    /// Useful for multi-run tests (e.g., RunAlways second run).
    /// </summary>
    public int GetMigrationRunResultByIndex(int runIndex)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return -1;

        string qualifiedTable = GetQualifiedTableName("MigrationRun");
        string col = QuoteColumn("MigrationRunResultId");
        string id = QuoteColumn("Id");
        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT {col} FROM {qualifiedTable} ORDER BY {id} ASC OFFSET {runIndex - 1} ROWS FETCH NEXT 1 ROWS ONLY",
            _ => $"SELECT {col} FROM {qualifiedTable} ORDER BY {id} ASC LIMIT 1 OFFSET {runIndex - 1}"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Returns the distinct TargetGroupAlias values from MigrationRecord entries ordered by the
    /// minimum Id within each group. This reflects the insertion order of the first record
    /// per TargetGroup, which corresponds to execution order during MigrateUp and Baseline.
    /// </summary>
    public List<string> GetTargetGroupAliasesInExecutionOrder()
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return new List<string>();

        string qualifiedTable = GetQualifiedTableName("MigrationRecord");
        string tgCol = QuoteColumn("TargetGroupAlias");
        string idCol = QuoteColumn("Id");

        // Build a pipe-delimited concat of TargetGroupAlias ordered by the first Id of each group.
        // Each engine uses its own aggregation dialect.
        string sql = _databaseType switch
        {
            "SqlServer" =>
                $"SELECT STRING_AGG({tgCol}, '|') WITHIN GROUP (ORDER BY min_id) " +
                $"FROM (SELECT {tgCol}, MIN({idCol}) AS min_id " +
                $"FROM {qualifiedTable} GROUP BY {tgCol}) sub",
            "PostgreSQL" =>
                $"SELECT string_agg({tgCol}, '|' ORDER BY min_id) " +
                $"FROM (SELECT {tgCol}, MIN({idCol}) AS min_id " +
                $"FROM {qualifiedTable} GROUP BY {tgCol}) sub",
            "MariaDb" or "MySql" =>
                $"SELECT GROUP_CONCAT({tgCol} ORDER BY min_id SEPARATOR '|') " +
                $"FROM (SELECT {tgCol}, MIN({idCol}) AS min_id " +
                $"FROM {qualifiedTable} GROUP BY {tgCol}) sub",
            "Sqlite" =>
                // SQLite has no built-in ordered aggregation; use a subquery that picks the first
                // alias per Id to produce the ordered sequence and then collapse to distinct.
                $"SELECT group_concat(alias, '|') " +
                $"FROM (SELECT DISTINCT {tgCol} AS alias, MIN({idCol}) AS min_id " +
                $"FROM {qualifiedTable} GROUP BY {tgCol} ORDER BY min_id ASC)",
            _ =>
                $"SELECT GROUP_CONCAT({tgCol} ORDER BY min_id SEPARATOR '|') " +
                $"FROM (SELECT {tgCol}, MIN({idCol}) AS min_id " +
                $"FROM {qualifiedTable} GROUP BY {tgCol}) sub"
        };

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            string? raw = result as string ?? result?.ToString();
            if (string.IsNullOrEmpty(raw))
                return new List<string>();

            return raw.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private string GetQualifiedTableName(string tableName)
    {
        return _databaseType switch
        {
            "SqlServer" => $"[{_schemaName}].[{tableName}]",
            // DAL-017: PostgreSQL uses unquoted snake_case identifiers for repository tables.
            "PostgreSQL" => $"{_schemaName}.{ToSnakeCase(tableName)}",
            // DAL-018: MariaDB/MySQL use snake_case identifiers for repository tables.
            // Note: no explicit schema qualifier — the connection's current DATABASE() acts as the schema.
            "MariaDb" or "MySql" => $"`{ToSnakeCase(tableName)}`",
            "Sqlite" => tableName,
            _ => tableName
        };
    }

    private string GetUnqualifiedTableName(string tableName)
    {
        return _databaseType switch
        {
            "SqlServer" => $"[{tableName}]",
            "PostgreSQL" => $"public.{tableName.ToLowerInvariant()}",
            "MariaDb" or "MySql" => $"`{tableName}`",
            "Sqlite" => tableName,
            _ => tableName
        };
    }

    private string QuoteColumn(string columnName)
    {
        // DAL-017: PostgreSQL repository columns are now unquoted snake_case identifiers.
        // DAL-018: MariaDB/MySQL repository columns are backticked snake_case identifiers.
        // Other engines keep their native quoting style (brackets, backticks, etc.).
        if (_databaseType == "PostgreSQL")
            return ToSnakeCase(columnName);

        if (_databaseType is "MariaDb" or "MySql")
            return $"`{ToSnakeCase(columnName)}`";

        if (_dalProps != null && !string.IsNullOrEmpty(_dalProps.IdentifierQuoteStart))
            return $"{_dalProps.IdentifierQuoteStart}{columnName}{_dalProps.IdentifierQuoteEnd}";
        return columnName;
    }

    /// <summary>
    /// Mechanical PascalCase to snake_case conversion that honours the "RayMigrator" product-name
    /// exception: the brand token <c>RayMigrator</c> is treated as a single word (-&gt; raymigrator)
    /// rather than being split into <c>ray_migrator</c>. All other PascalCase identifiers are
    /// split mechanically at every lowercase-to-uppercase boundary. Matches the naming convention
    /// used by the PostgreSQL repository templates after DAL-017 and (by reuse) DAL-018.
    /// </summary>
    /// <example>
    /// MigrationRecord             -&gt; migration_record
    /// FileUpBlocksTotal           -&gt; file_up_blocks_total
    /// RayMigratorVersion          -&gt; raymigrator_version
    /// CreatedByRayMigratorVersion -&gt; created_by_raymigrator_version
    /// </example>
    // Exposed as internal so the PG + MariaDB/MySQL identifier-casing unit tests can exercise the
    // RayMigrator-exception conversion rule directly without reaching the full DAL stack.
    internal static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        // Apply the RayMigrator product-name exception by collapsing the PascalCase token
        // "RayMigrator" to a sentinel "Raymigrator" (only first letter uppercase) before the
        // mechanical lower-to-upper split runs. The mechanical rule will then treat the whole
        // token as a single word and produce "raymigrator".
        string normalized = pascalCase.Replace("RayMigrator", "Raymigrator", StringComparison.Ordinal);

        var sb = new System.Text.StringBuilder(normalized.Length + 8);
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(normalized[i - 1]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets a full MigrationRecord entry by filename.
    /// Returns null if no record is found.
    /// </summary>
    public MigrationRecordDto? GetMigrationRecordByFilename(string filename)
    {
        string table = GetQualifiedTableName("MigrationRecord");
        string cols = BuildMigrationRecordConcat();
        string where = $"WHERE {QuoteColumn("Filename")} = '{EscapeSql(filename)}'";

        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {cols} FROM {table} {where}",
            _ => $"SELECT {cols} FROM {table} {where} LIMIT 1"
        };

        string? result = QueryScalarString(sql);
        return result != null ? ParseMigrationRecord(result) : null;
    }

    /// <summary>
    /// Gets a full MigrationRecord entry by filename and target alias.
    /// Used for multi-target scenarios where the same file may exist for multiple targets.
    /// Returns null if no record is found.
    /// </summary>
    public MigrationRecordDto? GetMigrationRecordByFilenameAndTarget(string filename, string targetAlias)
    {
        string table = GetQualifiedTableName("MigrationRecord");
        string cols = BuildMigrationRecordConcat();
        string where = $"WHERE {QuoteColumn("Filename")} = '{EscapeSql(filename)}' AND {QuoteColumn("TargetAlias")} = '{EscapeSql(targetAlias)}'";

        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT TOP 1 {cols} FROM {table} {where}",
            _ => $"SELECT {cols} FROM {table} {where} LIMIT 1"
        };

        string? result = QueryScalarString(sql);
        return result != null ? ParseMigrationRecord(result) : null;
    }

    /// <summary>
    /// Gets a full MigrationRun record by 1-based index (oldest first).
    /// Returns null if no record is found at the given index.
    /// </summary>
    public MigrationRunRecordDto? GetMigrationRunByIndex(int runIndex)
    {
        string table = GetQualifiedTableName("MigrationRun");
        string cols = BuildMigrationRunRecordConcat();
        string id = QuoteColumn("Id");

        string sql = _databaseType switch
        {
            "SqlServer" => $"SELECT {cols} FROM {table} ORDER BY {id} ASC OFFSET {runIndex - 1} ROWS FETCH NEXT 1 ROWS ONLY",
            _ => $"SELECT {cols} FROM {table} ORDER BY {id} ASC LIMIT 1 OFFSET {runIndex - 1}"
        };

        string? result = QueryScalarString(sql);
        return result != null ? ParseMigrationRunRecord(result) : null;
    }

    private string? QueryScalarString(string sql)
    {
        if (!DalFactory.TryGetDal(_databaseType, _repositoryConnectionString, out IDal? dal))
            return null;

        try
        {
            object? result = dal!.ExecuteScalarAsync(sql, QuerySettings, null).GetAwaiter().GetResult();
            return result as string ?? result?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds an engine-specific CONCAT expression that returns all Migration DTO columns as a pipe-delimited string.
    /// NULL columns are converted to empty strings via COALESCE/ISNULL.
    /// </summary>
    private string BuildMigrationRecordConcat()
    {
        // Column order must match ParseMigrationRecord and MigrationRecordDto constructor
        string[] columns =
        [
            "Id", "ProductId", "MigrationRunId", "MigrationRunModeId",
            "MigrationOperationId", "MigrationStatusId",
            "EnvironmentId", "ReleaseVersion",
            "TargetGroupAlias", "TargetAlias",
            "Filename", "FileOrderId",
            "FileUpBlocksMigrated", "FileUpBlocksTotal",
            "MigrateDownFileExists",
            "FileDownBlocksMigrated", "FileDownBlocksTotal"
        ];

        return BuildConcatExpression(columns);
    }

    /// <summary>
    /// Builds an engine-specific CONCAT expression that returns all MigrationRun DTO columns as a pipe-delimited string.
    /// </summary>
    private string BuildMigrationRunRecordConcat()
    {
        string[] columns =
        [
            "Id", "ProductId", "MigrationRunModeId", "MigrationRunResultId",
            "EnvironmentId", "FromReleaseVersion", "ToReleaseVersion"
        ];

        return BuildConcatExpression(columns);
    }

    private string BuildConcatExpression(string[] columns)
    {
        return _databaseType switch
        {
            "SqlServer" => BuildSqlServerConcat(columns),
            "PostgreSQL" => BuildPostgreSqlConcat(columns),
            "Sqlite" => BuildSqliteConcat(columns),
            "MariaDb" or "MySql" => BuildMariaDbMySqlConcat(columns),
            _ => throw new NotSupportedException($"Unsupported database type: {_databaseType}")
        };
    }

    private string BuildSqlServerConcat(string[] columns)
    {
        // CONCAT handles NULL as empty string in SQL Server
        var parts = new List<string>();
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                parts.Add("'|'");
            parts.Add($"CAST(ISNULL(CAST([{columns[i]}] AS NVARCHAR(MAX)), '') AS NVARCHAR(MAX))");
        }
        return $"CONCAT({string.Join(", ", parts)})";
    }

    private string BuildPostgreSqlConcat(string[] columns)
    {
        // DAL-017: PostgreSQL columns use unquoted snake_case identifiers.
        var parts = new List<string>();
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                parts.Add("'|'");
            parts.Add($"COALESCE({ToSnakeCase(columns[i])}::text, '')");
        }
        return string.Join(" || ", parts);
    }

    private string BuildSqliteConcat(string[] columns)
    {
        var parts = new List<string>();
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                parts.Add("'|'");
            parts.Add($"COALESCE(CAST(\"{columns[i]}\" AS TEXT), '')");
        }
        return string.Join(" || ", parts);
    }

    private string BuildMariaDbMySqlConcat(string[] columns)
    {
        // DAL-018: MariaDB/MySQL columns use snake_case identifiers.
        var parts = new List<string>();
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                parts.Add("'|'");
            parts.Add($"COALESCE(CAST({ToSnakeCase(columns[i])} AS CHAR), '')");
        }
        return $"CONCAT({string.Join(", ", parts)})";
    }

    private static MigrationRecordDto ParseMigrationRecord(string delimited)
    {
        string[] parts = delimited.Split('|');
        if (parts.Length != 17)
            throw new FormatException($"Expected 17 pipe-delimited values for MigrationRecordDto but got {parts.Length}: '{delimited}'");

        return new MigrationRecordDto(
            Id: int.Parse(parts[0]),
            ProductId: int.Parse(parts[1]),
            MigrationRunId: int.Parse(parts[2]),
            MigrationRunModeId: int.Parse(parts[3]),
            MigrationOperationId: int.Parse(parts[4]),
            MigrationStatusId: int.Parse(parts[5]),
            EnvironmentId: int.Parse(parts[6]),
            ReleaseVersion: parts[7],
            TargetGroupAlias: parts[8],
            TargetAlias: parts[9],
            Filename: parts[10],
            FileOrderId: int.Parse(parts[11]),
            FileUpBlocksMigrated: int.Parse(parts[12]),
            FileUpBlocksTotal: int.Parse(parts[13]),
            MigrateDownFileExists: ParseBoolFromDb(parts[14]),
            FileDownBlocksMigrated: ParseNullableInt(parts[15]),
            FileDownBlocksTotal: ParseNullableInt(parts[16])
        );
    }

    private static MigrationRunRecordDto ParseMigrationRunRecord(string delimited)
    {
        string[] parts = delimited.Split('|');
        if (parts.Length != 7)
            throw new FormatException($"Expected 7 pipe-delimited values for MigrationRunRecordDto but got {parts.Length}: '{delimited}'");

        return new MigrationRunRecordDto(
            Id: int.Parse(parts[0]),
            ProductId: int.Parse(parts[1]),
            MigrationRunModeId: int.Parse(parts[2]),
            MigrationRunResultId: int.Parse(parts[3]),
            EnvironmentId: int.Parse(parts[4]),
            FromReleaseVersion: string.IsNullOrEmpty(parts[5]) ? null : parts[5],
            ToReleaseVersion: string.IsNullOrEmpty(parts[6]) ? null : parts[6]
        );
    }

    private static bool ParseBoolFromDb(string value)
    {
        // BIT columns may be cast as "0"/"1", "True"/"False", or "true"/"false"
        return value is "1" or "True" or "true" or "t";
    }

    private static int? ParseNullableInt(string value)
    {
        return string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    private string GetDefaultSchema()
    {
        if (_dalProps != null && !string.IsNullOrEmpty(_dalProps.DefaultSchema))
            return _dalProps.DefaultSchema;
        return _databaseType == "Sqlite" ? "" : _schemaName;
    }
}

/// <summary>
/// Full record from the MigrationRecord repository table.
/// </summary>
public record MigrationRecordDto(
    int Id, int ProductId, int MigrationRunId, int MigrationRunModeId,
    int MigrationOperationId, int MigrationStatusId,
    int EnvironmentId, string ReleaseVersion,
    string TargetGroupAlias, string TargetAlias,
    string Filename, int FileOrderId,
    int FileUpBlocksMigrated, int FileUpBlocksTotal,
    bool MigrateDownFileExists,
    int? FileDownBlocksMigrated, int? FileDownBlocksTotal);

/// <summary>
/// Full record from the MigrationRun repository table.
/// </summary>
public record MigrationRunRecordDto(
    int Id, int ProductId, int MigrationRunModeId, int MigrationRunResultId,
    int EnvironmentId, string? FromReleaseVersion, string? ToReleaseVersion);
