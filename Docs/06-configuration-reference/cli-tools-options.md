# CLI Tools Options

CLI tools allow RayMigrator to execute migration SQL files via external command-line tools (e.g., `sqlcmd`, `psql`, `mysql`, `mariadb`, `sqlite3`) instead of the built-in DAL.

## Configuration

CLI tools are defined at the `RayMigrator` root level in the `CliTools` array, alongside `Repository`, `Products`, and `ProductDefaults`:

```json
{
  "RayMigrator": {
    "Repository": { ... },
    "ProductDefaults": { ... },
    "Products": [ ... ],
    "CliTools": [
      {
        "Alias": "sqlcmd-tool",
        "ExecutablePath": "sqlcmd",
        "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b",
        "InputMode": "File",
        "SuccessExitCodes": ["0"],
        "CliToolTimeoutInSeconds": 120
      }
    ]
  }
}
```

## CliToolOptions Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Alias` | string | Yes | - | Unique identifier (letters, numbers, underscores, hyphens; max 50 chars). Referenced by `UseCliToolAlias` on Products, TargetGroups, Targets, migsettings, and TOML headers. |
| `ExecutablePath` | string | Yes | - | Path to the CLI tool executable. Can be an absolute path or a command name found in the system PATH. |
| `ArgumentTemplate` | string | Yes | - | Command-line argument template with placeholders. `{FilePath}` is replaced with the migration file path (when `InputMode` is `File`). Custom placeholders (e.g., `{Server}`, `{User}`) are resolved from `CliToolParameters` on the Target. |
| `InputMode` | string | No | `File` | How the SQL file is passed to the CLI tool: `File` (as argument via `{FilePath}`) or `Stdin` (piped via standard input). Matching is case-insensitive: `Stdin`, `stdin` and `STDIN` are equivalent. |
| `SuccessExitCodes` | string[] | No | `["0"]` | Exit code whitelist. Supports single values (`"0"`), closed ranges (`"1..5"`), open-ended up (`"10.."`), and open-ended down (`"..-1"`). Any exit code not matched is treated as failure. |
| `CliToolTimeoutInSeconds` | int | No | `120` | Maximum time in seconds to wait for the CLI tool to complete. Minimum: 1. |

### SuccessExitCodes Range Notation

`SuccessExitCodes` is a string array (not an integer array). Each entry is an expression that matches one or more exit codes. Any exit code not matched by any expression is treated as failure.

| Expression | Example | Description |
|------------|---------|-------------|
| Single value | `"0"` | Matches exactly the given integer. |
| Closed range | `"1..5"` | Matches all integers from 1 to 5 (inclusive). |
| Open-ended up | `"10.."` | Matches any integer >= 10. |
| Open-ended down | `"..-1"` | Matches any integer <= -1. |

Examples:

```json
"SuccessExitCodes": ["0"]
"SuccessExitCodes": ["0", "2..4"]
"SuccessExitCodes": ["0", "10.."]
```

The parsed expressions are evaluated by `ExitCodeMatcher` in `Raycoon.RayMigrator.Core.Configuration.Options`. When `SuccessExitCodes` is null or empty, the default `["0"]` applies. Invalid expressions are caught at startup by `RayMigratorOptionsValidator` and reported as configuration errors.

### Alias Pattern

The `Alias` pattern for CLI tools is slightly different from Product/TargetGroup/Target aliases: it allows hyphens in addition to letters, numbers, and underscores. The regex is `^(?=.{1,50}$)[\p{L}\p{N}_\-]+$`.

### InputMode Values

| Value | Enum Value | Description |
|-------|------------|-------------|
| `File` | `1` | The file path is passed as a command-line argument via the `{FilePath}` placeholder in `ArgumentTemplate`. Used by tools like `sqlcmd` (`-i`), `psql` (`-f`), `sqlite3` (`-init`). |
| `Stdin` | `2` | The file content is piped to the process via standard input (`Process.StandardInput`). Used by tools like `mysql` and `mariadb` that read SQL from stdin. |

The `CliToolInputMode` enum also defines `Undefined = 0`, which falls back to `File` behavior at runtime.

## ArgumentTemplate Placeholders

The `ArgumentTemplate` supports two types of placeholders:

| Placeholder | Source | Description |
|-------------|--------|-------------|
| `{FilePath}` | Built-in | Replaced with the migration file path. Only used when `InputMode` is `File`. |
| `{CustomName}` | `CliToolParameters` | Resolved from the target's `CliToolParameters` dictionary. |

Custom placeholders are resolved by matching the key name (case-sensitive) in the target's `CliToolParameters` dictionary. For example, `{Server}` in the template will only match a `CliToolParameters` entry with key `"Server"`, not `"server"` or `"SERVER"`.

## UseCliToolAlias Inheritance

`UseCliToolAlias` can be set at multiple levels. The full inheritance chain, from lowest to highest priority:

```
ProductDefaults.UseCliToolAlias          (appsettings)
  → Product.UseCliToolAlias              (appsettings)
    → TargetGroup.UseCliToolAlias        (appsettings)
      → Target.UseCliToolAlias           (appsettings)
        → migsettings hierarchy      (directory-level override)
          → Migration file TOML      (per-file override, highest priority)
```

The appsettings portion of the chain (ProductDefaults through Target) is processed at startup by `ProductDefaultsPostConfigureOptions`. At each level, if the property is `null` or whitespace, the parent level's value is inherited. Explicitly set values are never overwritten.

At runtime, the file-level alias (from migsettings or TOML metadata) takes priority over the Target-level value resolved from appsettings. If no level sets `UseCliToolAlias`, the built-in DAL is used.

See [Settings and Inheritance Overview — UseCliToolAlias](settings-inheritance-overview.md#useclitoolalias) and [Rollback Files — UseCliToolAlias in Rollback Files](../07-migration-files/rollback-files.md#useclitoolalias-in-rollback-files) for additional details on how rollback files resolve the alias.

## Cross-Reference Validation

At startup, `RayMigratorOptionsValidator` validates that:

1. **CLI tool aliases are unique** -- duplicate `Alias` values within `CliTools[]` produce a validation error. Duplicate detection is case-insensitive: `"sqlcmd"` and `"SQLCMD"` are considered duplicates.
2. **UseCliToolAlias references exist** -- every `UseCliToolAlias` value across all Products, TargetGroups, and Targets must match an existing `CliTools[].Alias`. This validation runs after `PostConfigure` inheritance, so inherited aliases are also checked. Alias matching is case-insensitive.

## Example Configurations

### SQL Server with sqlcmd

```json
{
  "CliTools": [{
    "Alias": "sqlcmd",
    "ExecutablePath": "sqlcmd",
    "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b",
    "InputMode": "File",
    "SuccessExitCodes": ["0"],
    "CliToolTimeoutInSeconds": 120
  }]
}
```

### PostgreSQL with psql

```json
{
  "CliTools": [{
    "Alias": "psql",
    "ExecutablePath": "psql",
    "ArgumentTemplate": "-h {Host} -U {User} -d {Database} -f {FilePath}",
    "InputMode": "File",
    "SuccessExitCodes": ["0"]
  }]
}
```

### MySQL with mysql (Stdin mode)

```json
{
  "CliTools": [{
    "Alias": "mysql-cli",
    "ExecutablePath": "mysql",
    "ArgumentTemplate": "-h {Host} -u {User} -p{Password} {Database}",
    "InputMode": "Stdin",
    "SuccessExitCodes": ["0"]
  }]
}
```

### Multiple Tools

```json
{
  "CliTools": [
    {
      "Alias": "sqlcmd",
      "ExecutablePath": "sqlcmd",
      "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b",
      "InputMode": "File"
    },
    {
      "Alias": "psql",
      "ExecutablePath": "psql",
      "ArgumentTemplate": "-h {Host} -U {User} -d {Database} -f {FilePath}",
      "InputMode": "File"
    }
  ]
}
```

### Full Configuration with UseCliToolAlias

```json
{
  "RayMigrator": {
    "CliTools": [{
      "Alias": "sqlcmd",
      "ExecutablePath": "/opt/mssql-tools18/bin/sqlcmd",
      "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b -C",
      "InputMode": "File",
      "SuccessExitCodes": ["0"],
      "CliToolTimeoutInSeconds": 300
    }],
    "ProductDefaults": {
      "UseCliToolAlias": "sqlcmd",
      "TargetGroupDefaults": {
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20
        }
      }
    },
    "Products": [{
      "Alias": "MyProduct",
      "MigrationFilesRootDirectory": "/migrations",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "SqlServer",
        "Targets": [{
          "Alias": "Primary",
          "ConnectionString": "{ENV:PRIMARY_CONNECTION}",
          "CliToolParameters": {
            "Server": "localhost",
            "User": "sa",
            "Password": "{ENV:SA_PASSWORD}",
            "Database": "MyApp"
          }
        }]
      }]
    }]
  }
}
```

In this example, `UseCliToolAlias` is set at `ProductDefaults` and inherited by all Products, TargetGroups, and Targets. Each target provides its own `CliToolParameters` for placeholder substitution.

## Executing via Docker Containers

When the CLI tools (sqlcmd, psql, mysql, mariadb) are installed inside Docker containers rather than on the host, you can use `docker exec` as the executable to bridge the host-to-container boundary.

### Stdin Mode (Recommended for Docker)

With `InputMode: Stdin`, RayMigrator reads the migration file on the host and pipes the content to the CLI tool inside the container via `docker exec -i`:

```json
{
  "RayMigrator": {
    "CliTools": [{
      "Alias": "psql-docker",
      "ExecutablePath": "docker",
      "ArgumentTemplate": "exec -i my_postgres_container psql --set ON_ERROR_STOP=1 -U {User} -d {Database}",
      "InputMode": "Stdin",
      "CliToolTimeoutInSeconds": 30
    }],
    "Products": [{
      "Alias": "MyProduct",
      "MigrationFilesRootDirectory": "/migrations",
      "UseCliToolAlias": "psql-docker",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "PostgreSQL",
        "Targets": [{
          "Alias": "Primary",
          "ConnectionString": "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=secret",
          "CliToolParameters": {
            "User": "postgres",
            "Database": "mydb"
          }
        }]
      }]
    }]
  }
}
```

**How it works:** RayMigrator reads the migration file from disk, then pipes its content to `docker exec -i my_postgres_container psql ...` via standard input. The `-i` flag on `docker exec` keeps stdin open so the piped content reaches `psql` inside the container.

### File Mode via Bash Wrapper

With `InputMode: File`, the `{FilePath}` placeholder refers to a path on the host, which is not accessible from inside the container. To bridge this, use `/bin/bash` as the executable with a wrapper command that reads the host file and pipes it into the container:

```json
{
  "CliTools": [{
    "Alias": "psql-docker-file",
    "ExecutablePath": "/bin/bash",
    "ArgumentTemplate": "-c \"cat '{FilePath}' | docker exec -i my_postgres_container psql --set ON_ERROR_STOP=1 -U {User} -d {Database}\"",
    "InputMode": "File",
    "CliToolTimeoutInSeconds": 30
  }]
}
```

From RayMigrator's perspective this is File mode (stdin is not redirected by the executor). The bash wrapper internally reads the file and pipes it into the container.

### Docker Configuration Examples per Database

#### PostgreSQL (Stdin)

```json
{
  "CliTools": [{
    "Alias": "psql-docker",
    "ExecutablePath": "docker",
    "ArgumentTemplate": "exec -i rm_db_postgresql psql --set ON_ERROR_STOP=1 -U {User} -d {Database}",
    "InputMode": "Stdin"
  }]
}
```

**Important:** The `--set ON_ERROR_STOP=1` flag is required. Without it, `psql` exits with code 0 even when SQL errors occur.

#### MariaDB (Stdin)

```json
{
  "CliTools": [{
    "Alias": "mariadb-docker",
    "ExecutablePath": "docker",
    "ArgumentTemplate": "exec -i rm_db_mariadb mariadb -u {User} -p{Password} {Database}",
    "InputMode": "Stdin"
  }]
}
```

#### MySQL (Stdin)

```json
{
  "CliTools": [{
    "Alias": "mysql-docker",
    "ExecutablePath": "docker",
    "ArgumentTemplate": "exec -i rm_db_mysql mysql -u {User} -p{Password} {Database}",
    "InputMode": "Stdin"
  }]
}
```

**Note:** `mysql` prints "Using a password on the command line interface can be insecure" to stderr. This is a warning, not an error -- the migration still succeeds.

#### SQL Server (Stdin)

```json
{
  "CliTools": [{
    "Alias": "sqlcmd-docker",
    "ExecutablePath": "docker",
    "ArgumentTemplate": "exec -i rm_db_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P {Password} -C -d {Database} -b",
    "InputMode": "Stdin"
  }]
}
```

**Important:** The `-b` flag aborts batch execution on error. The `-C` flag trusts the server certificate.

### Considerations

- **Stdin mode is preferred** for Docker scenarios because it does not require the migration file to be accessible from inside the container.
- **File mode** requires a bash wrapper or a Docker volume mount to make host files available inside the container.
- **Container names** must match the running Docker containers exactly.
- **Exit codes**: `docker exec` forwards the exit code of the command inside the container, so `SuccessExitCodes` works as expected.
- **Timeout**: Set `CliToolTimeoutInSeconds` appropriately for your environment. Docker overhead adds latency compared to local CLI tools.

## Related Documentation

- [Target Options](target-options.md) -- `UseCliToolAlias` and `CliToolParameters` on targets
- [Product Options](product-options.md) -- `UseCliToolAlias` on products and product defaults
- [Target Group Options](target-group-options.md) -- `UseCliToolAlias` on target groups
- [Settings and Inheritance Overview](settings-inheritance-overview.md) -- Complete settings map
- [migsettings Files](../07-migration-files/migsettings-files.md) -- `UseCliToolAlias` in directory-level settings
- [TOML Metadata](../07-migration-files/toml-metadata.md) -- `UseCliToolAlias` in per-file metadata
