# Target Group Options

Target groups organize related databases that receive the same migrations.

## Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Alias` | string | Yes | - | Unique identifier within product (Unicode letters, numbers, underscores; max 50 characters) |
| `DatabaseType` | string | Yes | - | Database type for all targets in this group |
| `TargetMigrationOrder` | string | No | Inherited from `TargetGroupDefaults` | Execution order: `Simultaneously` or `Successively` |
| `HashValidationScope` | string | No | Inherited from `TargetGroupDefaults` | Hash validation mode: `File`, `SqlBlocks`, or `Disabled` |
| `StopRollbackOnMissingRollbackFile` | bool? | No | Inherited from `TargetGroupDefaults` | When `RequireRollbackFile=false`, controls whether an error-recovery rollback chain stops (`true`) or continues (`false`) when a rollback file is missing. Overrides the Product-level value for this TargetGroup. No effect on Migrate-Down. |
| `UseCliToolAlias` | string | No | Inherited from `Product` | CLI tool alias for migration execution instead of the DAL. References a `CliTools[].Alias` defined at the `RayMigrator` root level. Can be overridden per Target. |
| `Targets` | array | Yes | - | Target database configurations |

All string properties support `{ENV:VARIABLE_NAME}` placeholders.

## Database Types

| Value | Description |
|-------|-------------|
| `SqlServer` | Microsoft SQL Server |
| `PostgreSQL` | PostgreSQL |
| `MariaDb` | MariaDB |
| `MySql` | MySQL |
| `Sqlite` | SQLite |

Additional database types can be added via external DAL plugins. All targets within a group must use the same database type.

## Target Migration Order

| Value | Description |
|-------|-------------|
| `Simultaneously` | Execute each migration on all targets before next migration |
| `Successively` | Complete all migrations on one target before moving to next |

### Simultaneously

```
Migration 1 → Target 1
Migration 1 → Target 2
Migration 2 → Target 1
Migration 2 → Target 2
```

### Successively (Recommended Default)

```
Migration 1 → Target 1
Migration 2 → Target 1
Migration 1 → Target 2
Migration 2 → Target 2
```

## Hash Validation Scope

This setting governs hash comparison behavior in **all commands**: `Migrate-Up`, `Baseline`, and `Validate-Hash`.

| Value | Validates | Use Case |
|-------|-----------|----------|
| `File` | Entire file hash | Strictest, any change detected |
| `SqlBlocks` | SQL content only (excludes TOML metadata) | Allow metadata changes (e.g., adding `UseCliToolAlias`) without triggering re-execution |
| `Disabled` | No validation | Development/legacy. **Warning**: With `Disabled`, hash comparison is completely skipped. Changed migration files will **never be re-executed** by `Migrate-Up`, and `Validate-Hash` will report all files as valid. Use only in development environments or for legacy migrations where source files are known to have changed after initial execution. |

## Target Group Defaults

Default values for `TargetMigrationOrder`, `HashValidationScope`, and `StopRollbackOnMissingRollbackFile` are inherited from `ProductDefaults.TargetGroupDefaults` when not explicitly set on a target group. This inheritance is performed by `ProductDefaultsPostConfigureOptions` during configuration post-processing. Similarly, `TargetDefaults` values (`DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, `DbCommandWaitTimeInMsBeforeRetry`) are inherited by individual targets.

`UseCliToolAlias` follows a separate inheritance chain: `ProductDefaults` -> `Product` -> `TargetGroup` -> `Target`. It is not part of `TargetGroupDefaults` but is inherited directly from the parent `Product` level by `ProductDefaultsPostConfigureOptions`.

Configure defaults for all target groups:

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "TargetGroupDefaults": {
        "TargetMigrationOrder": "Successively",
        "HashValidationScope": "File",
        "StopRollbackOnMissingRollbackFile": true,
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 0,
          "DbCommandWaitTimeInMsBeforeRetry": 250
        }
      }
    }
  }
}
```

## Example Configurations

### Single Target Group

```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "DatabaseType": "SqlServer",
    "TargetMigrationOrder": "Simultaneously",
    "HashValidationScope": "File",
    "Targets": [
      { "Alias": "Primary", "ConnectionString": "..." },
      { "Alias": "Secondary", "ConnectionString": "..." }
    ]
  }]
}
```

### Multiple Target Groups

```json
{
  "TargetGroups": [
    {
      "Alias": "Backend",
      "DatabaseType": "SqlServer",
      "TargetMigrationOrder": "Simultaneously",
      "Targets": [
        { "Alias": "Backend1", "ConnectionString": "..." },
        { "Alias": "Backend2", "ConnectionString": "..." }
      ]
    },
    {
      "Alias": "Frontend",
      "DatabaseType": "SqlServer",
      "TargetMigrationOrder": "Successively",
      "Targets": [
        { "Alias": "Frontend", "ConnectionString": "..." }
      ]
    },
    {
      "Alias": "Analytics",
      "DatabaseType": "PostgreSQL",
      "HashValidationScope": "SqlBlocks",
      "Targets": [
        { "Alias": "DataWarehouse", "ConnectionString": "..." }
      ]
    }
  ]
}
```

### Multi-Database Product

```json
{
  "Products": [{
    "Alias": "HybridApp",
    "MigrationFilesRootDirectory": "/migrations/HybridApp",
    "TargetGroups": [
      {
        "Alias": "SqlServerTables",
        "DatabaseType": "SqlServer",
        "Targets": [
          { "Alias": "Primary", "ConnectionString": "{ENV:SQL_CONNECTION}" }
        ]
      },
      {
        "Alias": "PostgreSQLAnalytics",
        "DatabaseType": "PostgreSQL",
        "Targets": [
          { "Alias": "Analytics", "ConnectionString": "{ENV:PG_CONNECTION}" }
        ]
      },
      {
        "Alias": "MariaDBLegacy",
        "DatabaseType": "MariaDb",
        "Targets": [
          { "Alias": "Legacy", "ConnectionString": "{ENV:MARIADB_CONNECTION}" }
        ]
      },
      {
        "Alias": "MySQLCache",
        "DatabaseType": "MySql",
        "Targets": [
          { "Alias": "Cache", "ConnectionString": "{ENV:MYSQL_CONNECTION}" }
        ]
      }
    ]
  }]
}
```

## Directory Mapping

### Multiple Target Groups

When a product has more than one target group, each release directory must contain a subdirectory for every target group. The directory name must exactly match `TargetGroup.Alias` (case-sensitive):

```
MigrationFilesRootDirectory/
├── Release 1.0/
│   ├── Backend/           # Matches TargetGroup.Alias = "Backend"
│   │   └── 001_Create.sql
│   └── Frontend/          # Matches TargetGroup.Alias = "Frontend"
│       └── 001_Create.sql
```

### Single Target Group

When a product has exactly one target group, two layouts are supported per release:

- **Traditional**: A subdirectory matching the target group alias must be present. The alias match is case-sensitive — a directory whose name differs only in case causes a `ConfigurationValidationException`.
- **Flat**: Migration files placed directly under the release directory, with no target group subdirectory. The single configured target group alias is assigned automatically.

A product may mix layouts across releases (some releases flat, others traditional). Placing files in both locations within the same release is an error.

```
MigrationFilesRootDirectory/
├── Release 1.0/           # Flat layout: files directly under release
│   ├── 001_Create.sql
│   └── 002_Insert.sql
└── Release 1.1/           # Traditional layout: subdirectory present
    └── Backend/
        └── 001_AddColumn.sql
```

See [Directory Structure](../07-migration-files/directory-structure.md) for full details and examples.

## Related Documentation

- [Target Options](target-options.md)
- [CLI Tools Options](cli-tools-options.md)
- [Execution Modes](../02-core-concepts/execution-modes.md)
- [Hash Validation](../02-core-concepts/hash-validation.md)
- [Product Options](product-options.md)
