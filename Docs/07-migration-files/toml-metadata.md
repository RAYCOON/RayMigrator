# TOML Metadata

Migration behavior is controlled through TOML configuration embedded in SQL comments.

## Syntax

```sql
/*
[RayMigrator]
Parameter = Value
*/

-- SQL statements follow
```

## When to Use TOML Headers

The TOML header is entirely optional. A migration file without a header is valid and uses sensible defaults: it runs in all environments, applies to all targets, wraps execution in a transaction, and executes only once.

The simplest possible migration file is just SQL:

```sql
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL
);
```

Add a TOML header when you want to:

- Provide a **Description** that is stored in the repository and shown in logs.
- **Override a default**, such as disabling transactions (`UseTransaction = false`), re-executing on every run (`RunAlways = true`), restricting to specific environments, or changing the error-handling strategy.

If you do not need a description and the defaults are suitable, the file needs no header at all.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Description` | string | `""` | Human-readable description |
| `Environments` | array | not specified (= all) | Allowed environments. Use `["*"]` explicitly, omit entirely, or use `[]` (empty array) for all environments |
| `Targets` | array | not specified (= all) | Target databases (metadata only). Parsed and stored in the repository but **not** used for runtime target filtering. Use `["*"]` explicitly, omit entirely, or use `[]` (empty array) for all targets |
| `UseTransaction` | bool | `true` | Wrap in transaction |
| `RunAlways` | bool | `false` | Re-execute every run |
| `RequireRollbackFile` | bool? | inherits | Require a rollback file. When omitted, inherits from migsettings or product config (default: `true`) |
| `StopRollbackOnMissingRollbackFile` | bool? | (no effect) | Accepted by the parser (to avoid an "unknown key" error) but the value is immediately discarded and has no runtime effect. The effective rollback chain behavior is controlled entirely by the appsettings-level setting (`ProductDefaults`, `Product`, `TargetGroup`) and the CLI `--stop-rollback-on-missing-rollback-file` option. |
| `MigrationErrorAction` | string? | inherits | Error handling strategy per file. When omitted, inherits from migsettings or product config. Values: `Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore` |
| `RollbackErrorAction` | string? | inherits | Rollback error handling strategy. Only effective in **rollback** files (`.rollback.sql`). When set in a forward migration file, it is parsed but has no runtime effect. Values: `Terminate`, `Ignore` |
| `UseCliToolAlias` | string? | inherits | CLI tool alias for executing this file instead of the built-in DAL. References a `CliTools[].Alias` defined at the `RayMigrator` root level in `appsettings.json`. When omitted, inherits from migsettings, then from the Target/TargetGroup/Product/ProductDefaults configuration cascade. Null or empty means use the DAL (default behavior) |
| `TargetGroupMigrationOrder` | string[]? | not set | Recognized by the parser but **has no effect in migration file TOML**. Only meaningful in release-level `migsettings.txt` files. See [migsettings Files](migsettings-files.md) for details. |

Key parsing is **case-insensitive**. Enum values (`MigrationErrorAction`, `RollbackErrorAction`) are also case-insensitive and may optionally be quoted (`Rollback` or `"Rollback"`). String values (`Description`, `UseCliToolAlias`) may optionally be quoted. Lines starting with `#` are treated as comments and skipped. Unknown TOML keys cause a `MigrationFileParsingException`.

## Environment Filtering

### All Environments (Default)

Omit the `Environments` parameter entirely (recommended). This is the default — the file runs in all environments. If you prefer to be explicit, `["*"]` is an accepted alternative:

```sql
/*
[RayMigrator]
Environments = ["*"]
*/
```

### Specific Environments

```sql
/*
[RayMigrator]
Environments = ["Development", "Staging"]
*/
```

### Single Environment

```sql
/*
[RayMigrator]
Environments = ["Production"]
*/
```

## Target Filtering

> **Note**: The `Targets` parameter is currently **metadata only**. It is parsed and stored in the migration repository (as part of `FileUpConfigJson`) but is not used for runtime target filtering during execution. All targets in a target group receive every migration file regardless of the `Targets` value. This parameter is reserved for future use.

### All Targets (Default)

Omit the `Targets` parameter entirely (recommended). This is the default — the file applies to all targets. If you prefer to be explicit, `["*"]` is an accepted alternative:

```sql
/*
[RayMigrator]
Targets = ["*"]
*/
```

### Specific Targets

```sql
/*
[RayMigrator]
Targets = ["Primary", "Secondary"]
*/
```

### Single Target

```sql
/*
[RayMigrator]
Targets = ["Primary"]
*/
```

## Transaction Control

### With Transaction (Default)

Omit `UseTransaction` or the entire header — `true` is the default. If you want to be explicit:

```sql
/*
[RayMigrator]
UseTransaction = true
*/

CREATE TABLE Users (...);
INSERT INTO Users VALUES (...);
-- Both statements atomic
```

### Without Transaction

```sql
/*
[RayMigrator]
UseTransaction = false
Description = "Large data migration"
*/

-- Block 1
INSERT INTO Archive SELECT * FROM Data WHERE Id < 1000000;
GO

-- Block 2
INSERT INTO Archive SELECT * FROM Data WHERE Id >= 1000000;
GO
```

**Use Cases for `UseTransaction = false`**:
- Large data migrations
- DDL operations on MariaDB/MySQL (implicit commit)
- Operations requiring intermediate commits

> **Note**: `UseTransaction` has no effect when a CLI tool executes the migration (via `UseCliToolAlias`).
> The external tool (psql, sqlcmd, etc.) controls its own transaction behavior.
> If `UseTransaction` is explicitly set in the TOML header while `UseCliToolAlias` is also configured,
> a safety warning is logged during migrate-up.

## Run Always

### Normal Execution (Default)

Omit `RunAlways` — `false` is the default. The file executes once and is then recorded as migrated.

### Re-Execute Every Run

```sql
/*
[RayMigrator]
RunAlways = true
Description = "Refresh lookup data"
*/

TRUNCATE TABLE LookupData;
INSERT INTO LookupData VALUES (...);
```

**Use Cases for `RunAlways = true`**:
- Refresh lookup/reference data
- Environment-specific seed data
- Dynamic configuration updates

## Complete Examples

### Basic Migration (No Header)

The simplest migration file is just SQL. All defaults apply: all environments, all targets, transaction enabled, runs once.

```sql
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_Users_Email ON Users(Email);
```

### Basic Migration (With Description)

Add a header only to provide a description that is stored in the repository and shown in logs:

```sql
/*
[RayMigrator]
Description = "Create user authentication tables"
*/

CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_Users_Email ON Users(Email);
```

### Production-Only Migration

```sql
/*
[RayMigrator]
Description = "Enable audit logging for production"
Environments = ["Production"]
*/

ALTER TABLE Users ADD AuditEnabled BIT DEFAULT 1;
CREATE TABLE UserAuditLog (...);
```

### Multi-Target Migration (Metadata Only)

```sql
/*
[RayMigrator]
Description = "Insert data for primary and secondary only"
Targets = ["Primary", "Secondary"]
*/

INSERT INTO Configuration (Key, Value) VALUES ('Initialized', 'true');
```

> **Note**: The `Targets` filter is stored as metadata but not enforced at runtime. This migration will execute against all targets in the target group.

### Per-File Error Handling

```sql
/*
[RayMigrator]
Description = "Critical schema change — rollback on error"
MigrationErrorAction = Rollback
*/

ALTER TABLE Users ADD COLUMN Status INT NOT NULL DEFAULT 0;
```

### CLI Tool Execution

```sql
/*
[RayMigrator]
Description = "Run via external sqlcmd tool"
UseCliToolAlias = "sqlcmd-tool"
UseTransaction = false
*/

CREATE TABLE ExternalTable (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL
);
```

The `UseCliToolAlias` value must match a `CliTools[].Alias` defined in `appsettings.json`. When set, the migration file is executed by the external CLI tool instead of the built-in DAL. See [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) for `CliTools` configuration.

> **Note**: When `UseCliToolAlias` is set, `UseTransaction` has no effect — the CLI tool controls
> transaction behavior independently. If `UseTransaction` is explicitly set alongside `UseCliToolAlias`,
> a safety warning is logged. The example above includes `UseTransaction = false` for documentation
> purposes; it can be omitted.

### Seed Data (Run Always)

```sql
/*
[RayMigrator]
Description = "Refresh country lookup data"
RunAlways = true
*/

DELETE FROM Countries;
INSERT INTO Countries (Code, Name) VALUES
    ('US', 'United States'),
    ('DE', 'Germany'),
    ('UK', 'United Kingdom');
```

### Large Migration (No Transaction)

```sql
/*
[RayMigrator]
Description = "Archive historical data"
UseTransaction = false
Environments = ["Production"]
*/

-- Batch 1
INSERT INTO OrdersArchive SELECT * FROM Orders WHERE OrderDate < '2020-01-01';
GO

-- Batch 2
DELETE FROM Orders WHERE OrderDate < '2020-01-01';
GO
```

## Environment Variables in SQL Content

The SQL content (below the TOML header) supports `{ENV:VARIABLE_NAME}` placeholders. These are replaced at execution time, directly before the SQL is sent to the database. Hashes are computed on the original file content (with placeholders intact), so changing an environment variable value does not invalidate hash validation.

```sql
/*
[RayMigrator]
Description = "Seed default admin"
*/

INSERT INTO Users (Username, Email)
VALUES ('{ENV:DEFAULT_ADMIN}', '{ENV:DEFAULT_ADMIN_EMAIL}');
```

For details and best practices, see [Environment Variables](../06-configuration-reference/environment-variables.md#environment-variables-in-sql-migration-files).

## TOML Syntax Notes

### Strings

```toml
Description = "My migration"
```

Only double quotes are stripped by the parser. Single quotes are kept as literal characters in the value.

### Arrays

```toml
Environments = ["Dev", "Prod"]
Targets = ["*"]
```

### Booleans

```toml
UseTransaction = true
RunAlways = false
```

## Inheritance via migsettings Files

Default settings can be provided through `migsettings.txt` files at each directory level. These are merged following an inheritance hierarchy, with migration file TOML having the highest priority:

```
migsettings.txt (product root)
  └→ migsettings.{Env}.txt (product root, environment-specific)
      └→ Release/migsettings.txt (release-level)
          └→ Release/migsettings.{Env}.txt (release-level, environment-specific)
              └→ TargetGroup/migsettings.txt (target group)
                  └→ TargetGroup/migsettings.{Env}.txt (target group, environment-specific)
                      └→ Migration file TOML (highest priority)
```

See [migsettings Files](migsettings-files.md) for details on syntax, merge behavior, and examples.

### UseCliToolAlias Inheritance

`UseCliToolAlias` has a wider inheritance chain than other TOML parameters, because it can also be set at the configuration level (in `appsettings.json`). The full resolution order (lowest to highest priority):

```
ProductDefaults.UseCliToolAlias
  └→ Product.UseCliToolAlias
      └→ TargetGroup.UseCliToolAlias
          └→ Target.UseCliToolAlias
              └→ migsettings hierarchy (same as above)
                  └→ Migration file TOML (highest priority)
```

The configuration cascade (`ProductDefaults` through `Target`) is resolved at startup via `ProductDefaultsPostConfigureOptions`. At runtime, file-level `UseCliToolAlias` (from TOML or migsettings) takes precedence over the Target-level value. If no level sets `UseCliToolAlias`, the built-in DAL is used.

## Related Documentation

- [migsettings Files](migsettings-files.md)
- [File Naming](file-naming.md)
- [Environment-Specific](environment-specific.md)
- [Hash Validation](../02-core-concepts/hash-validation.md)
