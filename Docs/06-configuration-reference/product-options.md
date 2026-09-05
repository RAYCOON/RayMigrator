# Product Options

Products represent separate applications or systems with their own migration sets.

## Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Alias` | string | Yes | - | Unique product identifier (Unicode letters, numbers, underscores, max 50 chars) |
| `MigrationFilesRootDirectory` | string | Yes | - | Root path for migration files (must exist) |
| `MigrationErrorAction` | string | Yes* | - | Error handling mode (*inherited from `ProductDefaults`) |
| `RollbackErrorAction` | string | No | - | Error handling during rollback (inherited from `ProductDefaults`) |
| `MigrationFilesExtension` | string | No | - | Migration file extension (inherited from `ProductDefaults`) |
| `MigrationRollbackFilesPreExtension` | string | No | - | Rollback file naming (inherited from `ProductDefaults`) |
| `MigrationFilesEncoding` | string | No | - | File encoding (inherited from `ProductDefaults`) |
| `RequireRollbackFile` | bool? | No | - | Require a rollback file for every migration file (inherited from `ProductDefaults`) |
| `StopRollbackOnMissingRollbackFile` | bool? | No | `true` | When `RequireRollbackFile=false`, controls whether an error-recovery rollback chain stops (`true`) or continues (`false`) when a rollback file is missing. No effect on migrate-down. (inherited from `ProductDefaults`) |
| `UseCliToolAlias` | string | No | `null` | CLI tool alias for migration execution instead of the DAL (inherited from `ProductDefaults`). References a `CliTools[].Alias` defined at the `RayMigrator` root level. Can be overridden per TargetGroup or Target. |
| `TargetGroupMigrationOrder` | string | No | `null` (config array order) | Comma-separated list of target group aliases that sets the order in which target groups are executed. Only valid when the product has more than one target group. All aliases must be listed and case matches exactly. Applies to `migrate-up` and `baseline` only. Can be overridden per release via `migsettings` TOML or via the CLI `--target-group-migration-order` option. |
| `TargetGroups` | array | Yes | - | Target group configurations |

Properties marked with * are required after defaults have been applied. If not set on the product, they must be set in `ProductDefaults`.

All string properties support `{ENV:VARIABLE_NAME}` placeholders.

## MigrationErrorAction Values

| Value | Description |
|-------|-------------|
| `Terminate` | Stop immediately on error, no rollback |
| `Rollback` | Execute rollback scripts for all migrations performed in the current run |
| `RollbackErrorOnly` | Rollback only the failed migration using its associated rollback file |
| `RollbackRelease` | Rollback all migrations from the release that caused the error. Migrations from earlier releases remain intact. |
| `Ignore` | Ignore the error and continue execution. Failed SQL blocks are skipped, and the migration file is marked as Failed. The migration run continues with the next file. |

See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior descriptions, flow diagrams, and the full priority chain.

## RollbackErrorAction Values

Defines the behavior when a rollback operation itself encounters an error. Since a failed rollback cannot itself be rolled back, only `Terminate` and `Ignore` are meaningful.

| Value | Description |
|-------|-------------|
| `Terminate` | Stop the rollback chain immediately, no further rollbacks are performed (default) |
| `Ignore` | Ignore the error and continue. Failed SQL blocks are skipped, and the rollback file is marked as Failed. The rollback chain continues with the next file. |

See [Error Handling — Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling) for details.

## TargetGroupMigrationOrder

The `TargetGroupMigrationOrder` property lets you specify the order in which TargetGroups are executed during `migrate-up` and `baseline`. When omitted, the order defaults to the array order in the `TargetGroups` configuration.

### Validation Rules

- Only valid when the product has **more than one TargetGroup**. Setting it on a single-TargetGroup product is a configuration error.
- **All** TargetGroup aliases must be listed — partial lists are rejected with an error that shows the available aliases.
- Alias matching is **case-sensitive**. Providing a case-insensitive-only match produces a specific error with a hint for the correct casing.
- **No duplicates** are allowed.
- Comma-separated values; leading and trailing whitespace around each alias is trimmed.
- This setting does **not** apply to `migrate-down`, `validate-hash`, `update-hash`, `info`, or `fix`.

### Override Chain

The effective order is determined by the first source in this priority chain (highest wins):

1. CLI `--target-group-migration-order` / `-tgmo` option
2. Release-level `migsettings` TOML (`TargetGroupMigrationOrder = ["Frontend","Backend"]`)
3. Product-level `appsettings` JSON (`"TargetGroupMigrationOrder": "Frontend, Backend"`)
4. Config array order (default)

### Configuration Example

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "MigrationFilesRootDirectory": "/migrations/MyProduct",
    "TargetGroupMigrationOrder": "Frontend, Backend",
    "TargetGroups": [
      { "Alias": "Backend", "DatabaseType": "SqlServer", "Targets": [...] },
      { "Alias": "Frontend", "DatabaseType": "PostgreSQL", "Targets": [...] }
    ]
  }]
}
```

In this example, despite `Backend` appearing first in the `TargetGroups` array, `Frontend` will be executed first because of the explicit `TargetGroupMigrationOrder` override.

## File Naming

### Migration Files

Pattern: `{Sequence}_{Description}.{Extension}`

Examples:
- `001_CreateTable.sql`
- `002_InsertData.sql`

### Rollback Files

Pattern: `{Sequence}_{Description}.{RollbackExtension}.{Extension}`

With default `rollback`:
- `001_CreateTable.rollback.sql`

With custom `down`:
- `001_CreateTable.down.sql`

### Extension Validation

Both `MigrationFilesExtension` and `MigrationRollbackFilesPreExtension` are validated against the pattern `^[a-zA-Z_]+$`: only ASCII letters and underscores are allowed. Digits and hyphens are not accepted.

## Product Defaults

Configure defaults for all products. The `ProductDefaults` section is **required** in the configuration:

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "MigrationErrorAction": "Terminate",
      "RollbackErrorAction": "Terminate",
      "MigrationFilesExtension": "sql",
      "MigrationRollbackFilesPreExtension": "rollback",
      "MigrationFilesEncoding": "UTF-8",
      "RequireRollbackFile": true,
      "StopRollbackOnMissingRollbackFile": true,
      "UseCliToolAlias": null,

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

Individual products inherit from defaults unless overridden. During startup, `ProductDefaultsPostConfigureOptions` (an `IPostConfigureOptions<RayMigratorOptions>` implementation) copies default values to product/target-group/target properties that have not been explicitly set. This includes the `UseCliToolAlias` inheritance chain: `ProductDefaults` -> `Product` -> `TargetGroup` -> `Target`.

## Example Configurations

### Minimal

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "MigrationFilesRootDirectory": "{ENV:MigrationFilesRootDirectory}",
    "TargetGroups": [...]
  }]
}
```

### Full

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "MigrationFilesRootDirectory": "/app/migrations/MyProduct",
    "MigrationErrorAction": "Rollback",
    "RollbackErrorAction": "Terminate",
    "MigrationFilesExtension": "sql",
    "MigrationRollbackFilesPreExtension": "rollback",
    "MigrationFilesEncoding": "UTF-8",
    "RequireRollbackFile": true,
    "StopRollbackOnMissingRollbackFile": true,
    "UseCliToolAlias": "sqlcmd-tool",
    "TargetGroupMigrationOrder": "Frontend, Backend",
    "TargetGroups": [
      {
        "Alias": "Backend",
        "DatabaseType": "SqlServer",
        "Targets": [...]
      }
    ]
  }]
}
```

### Multiple Products

```json
{
  "Products": [
    {
      "Alias": "WebApp",
      "MigrationFilesRootDirectory": "/migrations/webapp",
      "TargetGroups": [...]
    },
    {
      "Alias": "API",
      "MigrationFilesRootDirectory": "/migrations/api",
      "TargetGroups": [...]
    },
    {
      "Alias": "Analytics",
      "MigrationFilesRootDirectory": "/migrations/analytics",
      "MigrationErrorAction": "Terminate",
      "TargetGroups": [...]
    }
  ]
}
```

## File Encoding

Any valid .NET encoding name accepted by `System.Text.Encoding.GetEncoding()` can be used. Some encodings (e.g. `windows-1252`) require `System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` on .NET Core.

Common values:

| Value | Description |
|-------|-------------|
| `UTF-8` | UTF-8 (default, recommended) |
| `ASCII` | ASCII |
| `Unicode` | UTF-16 |
| `iso-8859-1` | Latin-1 Western European |

## Directory Structure

For product with `Alias = "MyProduct"` and `MigrationFilesRootDirectory = "/migrations/MyProduct"`:

```
/migrations/MyProduct/
├── migsettings.txt
├── Release 1.0/
│   └── Backend/
│       ├── 001_CreateTable.sql
│       └── 001_CreateTable.rollback.sql
└── Release 1.1/
    └── Backend/
        └── 001_AddColumn.sql
```

## Related Documentation

- [Target Group Options](target-group-options.md)
- [Target Options](target-options.md)
- [CLI Tools Options](cli-tools-options.md)
- [Error Handling](../02-core-concepts/error-handling.md)
- [Directory Structure](../07-migration-files/directory-structure.md)
