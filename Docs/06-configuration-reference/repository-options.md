# Repository Options

The repository stores migration tracking data. Configuration is under `RayMigrator.Repository`.

## Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `DatabaseType` | string | Yes | - | Database type: `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite` |
| `ConnectionString` | string | Yes | - | Database connection string |
| `SchemaName` | string | Conditional | `"ray"` (ConfigWizard-scaffolded default; options class itself has no default) | Schema for repository tables (required for SqlServer, PostgreSQL; ignored for MariaDb, MySql, SQLite) |
| `TableBaseName` | string | No | `null` (no prefix) | Prefix for table names |
| `DbCommandTimeoutInSeconds` | int | No | 60 | Command timeout (0 = infinite) |
| `DbCommandMaxRetries` | int | No | 100 | Retry attempts on transient failure |
| `DbCommandWaitTimeInMsBeforeRetry` | int | No | 250 | Base wait time between retries (linear backoff: base * attempt) |

All string properties support `{ENV:VARIABLE_NAME}` placeholders.

Repository retry is active. `RepositoryExtensions.GetDalSettings()` builds a `DalSettings` object from these properties for every repository template call. With the default of `DbCommandMaxRetries = 100`, repository operations automatically retry on transient database errors. See [Resilience and Recovery](../02-core-concepts/resilience.md) for details on transient error codes and retry behavior.

When `TableBaseName` is `null` or omitted, repository tables have no prefix (e.g., `migrations.MigratorMeta`). When set (e.g., `"Ray"`), tables get the prefix (e.g., `migrations.RayMigratorMeta`). See [Table Naming](#table-naming) below.

## Database Types

| Value | Description |
|-------|-------------|
| `SqlServer` | Microsoft SQL Server |
| `PostgreSQL` | PostgreSQL |
| `MariaDb` | MariaDB |
| `MySql` | MySQL |
| `Sqlite` | SQLite |

## Schema Names

| Database | Default Schema | Notes |
|----------|----------------|-------|
| SQL Server | `dbo` | Can be any schema |
| PostgreSQL | `public` | Case-sensitive when quoted |
| MariaDB | (database name) | No separate schema concept |
| MySQL | (database name) | No separate schema concept |
| SQLite | (not used) | No schema support; `SchemaName` is ignored |

## Example Configurations

### Minimal

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:REPO_CONNECTION}",
      "SchemaName": "migrations"
    }
  }
}
```

### Full

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:REPO_CONNECTION}",
      "SchemaName": "migrations",
      "TableBaseName": "Ray",
      "DbCommandTimeoutInSeconds": 60,
      "DbCommandMaxRetries": 3,
      "DbCommandWaitTimeInMsBeforeRetry": 500
    }
  }
}
```

### PostgreSQL

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "PostgreSQL",
      "ConnectionString": "Host=localhost;Database=raymigrator;Username=postgres;Password={ENV:PG_PASSWORD}",
      "SchemaName": "migrations"
    }
  }
}
```

### MariaDB

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "MariaDb",
      "ConnectionString": "Server=localhost;Database=raymigrator;Uid=root;Pwd={ENV:MARIADB_PASSWORD}"
    }
  }
}
```

> **Note**: MariaDB does not support schemas. `SchemaName` can be omitted. If provided, it will be ignored and a warning is logged.

### MySQL

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "MySql",
      "ConnectionString": "Server=localhost;Port=3307;Database=raymigrator;Uid=root;Pwd={ENV:MYSQL_PASSWORD}"
    }
  }
}
```

> **Note**: MySQL does not support schemas. `SchemaName` can be omitted. If provided, it will be ignored and a warning is logged.

### SQLite

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "Sqlite",
      "ConnectionString": "Data Source={ENV:REPO_DB_PATH}"
    }
  }
}
```

> **Note**: SQLite does not support schemas. `SchemaName` can be omitted. If provided, it will be ignored and a warning is logged.

## Table Naming

With `SchemaName = "migrations"` and `TableBaseName = "Ray"`:

| Table | Full Name |
|-------|-----------|
| MigratorMeta | `migrations.RayMigratorMeta` |
| Product | `migrations.RayProduct` |
| MigrationRun | `migrations.RayMigrationRun` |
| MigrationRunMeta | `migrations.RayMigrationRunMeta` |
| MigrationRecord | `migrations.RayMigrationRecord` |
| MigrationRecordHistory | `migrations.RayMigrationRecordHistory` |
| MigrationRunMode | `migrations.RayMigrationRunMode` |
| MigrationOperation | `migrations.RayMigrationOperation` |
| MigrationRunResult | `migrations.RayMigrationRunResult` |
| MigrationStatus | `migrations.RayMigrationStatus` |

With `TableBaseName = ""` (empty):

| Table | Full Name |
|-------|-----------|
| MigratorMeta | `migrations.MigratorMeta` |
| Product | `migrations.Product` |
| MigrationRun | `migrations.MigrationRun` |
| MigrationRunMeta | `migrations.MigrationRunMeta` |
| MigrationRecord | `migrations.MigrationRecord` |
| MigrationRecordHistory | `migrations.MigrationRecordHistory` |
| MigrationRunMode | `migrations.MigrationRunMode` |
| MigrationOperation | `migrations.MigrationOperation` |
| MigrationRunResult | `migrations.MigrationRunResult` |
| MigrationStatus | `migrations.MigrationStatus` |

## Connection String Examples

### SQL Server

```
Server=localhost;Initial Catalog=RayMigrator;User Id=sa;Password=pass;TrustServerCertificate=true
```

### SQL Server (Integrated Security)

```
Server=localhost;Initial Catalog=RayMigrator;Integrated Security=true;TrustServerCertificate=true
```

### PostgreSQL

```
Host=localhost;Database=raymigrator;Username=postgres;Password=pass
```

### MariaDB

```
Server=localhost;Database=raymigrator;Uid=root;Pwd=pass
```

### MySQL

```
Server=localhost;Port=3307;Database=raymigrator;Uid=root;Pwd=pass
```

### SQLite

```
Data Source=./data/raymigrator.db
```

## Retry Configuration

Repository retries are active by default with `DbCommandMaxRetries = 100` and `DbCommandWaitTimeInMsBeforeRetry = 250`. This provides robust retry behavior for transient database errors during repository operations.

To customize retry settings:

```json
{
  "Repository": {
    "DbCommandMaxRetries": 3,
    "DbCommandWaitTimeInMsBeforeRetry": 500
  }
}
```

**Behavior**: On transient failure, waits `base * attempt` ms (linear backoff), then retries, up to the configured number of times. Set `DbCommandMaxRetries` to `0` to disable retries.

See [Resilience and Recovery](../02-core-concepts/resilience.md) for the complete list of transient error codes per database engine.

## Related Documentation

- [Repository Schema](../03-database-layer/repository-schema.md)
- [Logging Options](logging-options.md)
- [Environment Variables](environment-variables.md)
