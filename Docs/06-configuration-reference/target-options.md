# Target Options

Targets represent individual database connections within a target group.

## Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Alias` | string | Yes | - | Unique identifier within target group (Unicode letters, numbers, underscores, max 50 chars) |
| `ConnectionString` | string | Yes | - | Database connection string |
| `DbCommandTimeoutInSeconds` | int | No | 20 | Command timeout (0 = infinite) |
| `DbCommandMaxRetries` | int | No | 0 | Retry attempts on failure |
| `DbCommandWaitTimeInMsBeforeRetry` | int | No | 250 | Wait time between retries |
| `UseCliToolAlias` | string | No | Inherited from `TargetGroup` | CLI tool alias for migration execution instead of the DAL. References a `CliTools[].Alias` defined at the `RayMigrator` root level. Can be overridden per migration file via migsettings or TOML. |
| `CliToolParameters` | Dictionary&lt;string, string&gt; | No | `null` | Key-value pairs for placeholder substitution in the CLI tool's `ArgumentTemplate`. Values support `{ENV:VAR}` replacement. Example: `{"Server": "localhost", "Database": "mydb", "Password": "{ENV:SA_PASSWORD}"}` |

All string properties support `{ENV:VARIABLE_NAME}` placeholders.

> **Note**: The effective default for `DbCommandWaitTimeInMsBeforeRetry` is **250** because `TargetDefaults` (which provides 250) is always applied by `PostConfigureOptions` at startup. The `TargetOptions` class itself has an annotation default of 500, but this only applies if `TargetDefaults` is completely absent from the configuration — which does not occur in practice since `ProductDefaults.TargetGroupDefaults.TargetDefaults` is always present.

## Target Defaults

Configure defaults for all targets. If a target property is not set, it inherits from `TargetDefaults`. If `TargetDefaults` is also not set, the annotation default on `TargetOptions` applies:

| Property | `TargetDefaults` Default | `TargetOptions` Annotation Fallback |
|----------|--------------------------|-------------------------------------|
| `DbCommandTimeoutInSeconds` | 20 | 20 |
| `DbCommandMaxRetries` | 0 | 0 |
| `DbCommandWaitTimeInMsBeforeRetry` | 250 | 500 |

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "TargetGroupDefaults": {
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

### Minimal

```json
{
  "Targets": [{
    "Alias": "MainDB",
    "ConnectionString": "{ENV:DB_CONNECTION}"
  }]
}
```

### Full

```json
{
  "Targets": [{
    "Alias": "Primary",
    "ConnectionString": "{ENV:PRIMARY_CONNECTION}",
    "DbCommandTimeoutInSeconds": 120,
    "DbCommandMaxRetries": 3,
    "DbCommandWaitTimeInMsBeforeRetry": 1000,
    "UseCliToolAlias": "sqlcmd-tool",
    "CliToolParameters": {
      "Server": "localhost",
      "User": "sa",
      "Password": "{ENV:SA_PASSWORD}",
      "Database": "MyApp"
    }
  }]
}
```

### Multiple Targets

```json
{
  "Targets": [
    {
      "Alias": "Primary",
      "ConnectionString": "Server=primary;Database=MyApp;...",
      "DbCommandTimeoutInSeconds": 60
    },
    {
      "Alias": "Secondary",
      "ConnectionString": "Server=secondary;Database=MyApp;...",
      "DbCommandTimeoutInSeconds": 60
    },
    {
      "Alias": "Reporting",
      "ConnectionString": "Server=reporting;Database=MyApp_Reporting;...",
      "DbCommandTimeoutInSeconds": 300,
      "DbCommandMaxRetries": 2
    }
  ]
}
```

## CLI Tool Integration

When `UseCliToolAlias` is set, migration SQL files are executed via the referenced external CLI tool instead of the built-in DAL. The `CliToolParameters` dictionary provides custom placeholder values that are substituted into the CLI tool's `ArgumentTemplate`.

### UseCliToolAlias Inheritance

`UseCliToolAlias` follows the same inheritance pattern as other settings, processed by `ProductDefaultsPostConfigureOptions`:

```
ProductDefaults.UseCliToolAlias
  → Product.UseCliToolAlias
    → TargetGroup.UseCliToolAlias
      → Target.UseCliToolAlias
        → migsettings.txt (directory-level override)
          → TOML metadata (per-file override)
```

At each level, an explicit value overrides the inherited one. `null` or empty means "inherit from parent". The final effective alias is resolved at execution time by `ResolveUseCliToolAlias()`: the file-level alias (from TOML/migsettings) takes priority over the Target-level alias.

### CliToolParameters Placeholders

Custom placeholders in the CLI tool's `ArgumentTemplate` (e.g., `{Server}`, `{User}`, `{Password}`, `{Database}`) are resolved from the target's `CliToolParameters` dictionary. The built-in `{FilePath}` placeholder is always replaced with the migration file path (when `InputMode` is `File`).

Values in `CliToolParameters` support `{ENV:VARIABLE_NAME}` replacement, which is resolved at configuration load time.

### Example with CLI Tool

```json
{
  "Targets": [{
    "Alias": "Primary",
    "ConnectionString": "{ENV:PRIMARY_CONNECTION}",
    "UseCliToolAlias": "sqlcmd-tool",
    "CliToolParameters": {
      "Server": "localhost",
      "User": "sa",
      "Password": "{ENV:SA_PASSWORD}",
      "Database": "MyApp"
    }
  }]
}
```

See [CLI Tools Options](cli-tools-options.md) for the `CliTools[]` definition format.

## Connection String Examples

### SQL Server

```json
{
  "ConnectionString": "Server=localhost;Initial Catalog=MyApp;User Id=sa;Password={ENV:SA_PASSWORD};TrustServerCertificate=true"
}
```

### SQL Server (Integrated Security)

```json
{
  "ConnectionString": "Server=localhost;Initial Catalog=MyApp;Integrated Security=true;TrustServerCertificate=true"
}
```

### PostgreSQL

```json
{
  "ConnectionString": "Host=localhost;Database=myapp;Username=postgres;Password={ENV:PG_PASSWORD}"
}
```

### MariaDB

```json
{
  "ConnectionString": "Server=localhost;Database=myapp;Uid=root;Pwd={ENV:MARIADB_PASSWORD}"
}
```

### MySQL

```json
{
  "ConnectionString": "Server=localhost;Port=3307;Database=myapp;Uid=root;Pwd={ENV:MYSQL_PASSWORD}"
}
```

### SQLite

```json
{
  "ConnectionString": "Data Source={ENV:DB_PATH}"
}
```

## Timeout Configuration

### Short Transactions (Default)

```json
{
  "DbCommandTimeoutInSeconds": 20
}
```

### Long-Running Migrations

```json
{
  "DbCommandTimeoutInSeconds": 600,
  "DbCommandMaxRetries": 0
}
```

### Unreliable Network

```json
{
  "DbCommandTimeoutInSeconds": 60,
  "DbCommandMaxRetries": 3,
  "DbCommandWaitTimeInMsBeforeRetry": 1000
}
```

### Infinite Timeout (Use with Caution)

```json
{
  "DbCommandTimeoutInSeconds": 0
}
```

## Target Alias in TOML

Target aliases can be referenced in migration file TOML headers. This value is stored in the repository as metadata but is **not used for runtime target filtering** — all targets in a target group receive every migration file regardless of this value:

```sql
/*
[RayMigrator]
Targets = ["Primary", "Secondary"]
*/
```

| TOML Value | Meaning (metadata only) |
|------------|------------------------|
| `["*"]` | Intended for all targets (default) |
| `["Primary"]` | Intended for Primary (informational only) |
| `["Primary", "Secondary"]` | Intended for both (informational only) |

> **Note:** The `Targets` TOML parameter is reserved for future runtime filtering. See [TOML Metadata — Target Filtering](../07-migration-files/toml-metadata.md#target-filtering) for details.

## Environment-Specific Targets

Override targets per environment:

**appsettings.json**:
```json
{
  "Targets": [
    { "Alias": "Primary", "ConnectionString": "{ENV:PRIMARY_DB}" },
    { "Alias": "Secondary", "ConnectionString": "{ENV:SECONDARY_DB}" }
  ]
}
```

**appsettings.Production.json**:
```json
{
  "Targets": [
    {
      "Alias": "Primary",
      "DbCommandTimeoutInSeconds": 300,
      "DbCommandMaxRetries": 5
    }
  ]
}
```

## Related Documentation

- [Target Group Options](target-group-options.md)
- [Product Options](product-options.md)
- [CLI Tools Options](cli-tools-options.md)
- [Execution Modes](../02-core-concepts/execution-modes.md)
- [TOML Metadata](../07-migration-files/toml-metadata.md)
