# Configuration Guide

This chapter continues the BookStore tutorial by showing how to configure RayMigrator for different environments, databases, and deployment scenarios.

By the end of this chapter you will understand:

- How configuration files are loaded and merged
- How to split configuration across environments
- Every section of the RayMigrator configuration
- How the inheritance chain works for defaults
- How to use environment variables for secrets

---

## Configuration File Hierarchy

In Standalone mode, RayMigrator loads configuration from up to four JSON files, merged in a specific order. Each subsequent file overrides values from the previous one.

| Order | File | Purpose |
|-------|------|---------|
| 1 | `appsettings.json` | Base configuration shared across all environments |
| 2 | `appsettings.{Environment}.json` | Environment-specific overrides |
| 3 | `appsettings.{Product}.json` | Product-specific overrides |
| 4 | `appsettings.{Product}.{Environment}.json` | Product + environment overrides |

**All files are optional.** If a file does not exist, it is silently skipped. Properties defined in later files override those from earlier files; properties not redefined are inherited.

By default, RayMigrator looks for these files in the current working directory. Use the `--config-dir` (`-cd`) global CLI option to override the directory:

```bash
raymigrator migrate-up -p BookStore -env Production -rm migrate --config-dir /etc/raymigrator
```

The `{ENV:VARIABLE_NAME}` placeholder syntax is supported in the `--config-dir` value. See [Global Options](../08-cli-reference/global-options.md#--config-dir--cd) for full details.

The `{Environment}` value comes from the `--environment` (or `-env`) CLI option:

```bash
raymigrator migrate-up --product BookStore --environment Production
```

This would load, in order:

1. `appsettings.json`
2. `appsettings.Production.json`
3. `appsettings.BookStore.json`
4. `appsettings.BookStore.Production.json`

> **Tip:** Use the base `appsettings.json` for structure and defaults. Put connection strings and logging levels into environment-specific files.

---

## Tutorial: Split Config for Dev/Prod

Let us split the BookStore configuration across three files so that development and production use different connection strings and logging levels.

### Base Configuration — `appsettings.json`

This file defines the overall structure, product layout, and sensible defaults:

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "SqlServer",
      "SchemaName": "ray",
      "DbCommandTimeoutInSeconds": 60
    },
    "ProductDefaults": {
      "MigrationErrorAction": "Terminate",
      "RollbackErrorAction": "Terminate",
      "MigrationFilesExtension": "sql",
      "MigrationRollbackFilesPreExtension": "rollback",
      "MigrationFilesEncoding": "UTF-8",
      "RequireRollbackFile": true,
      "TargetGroupDefaults": {
        "TargetMigrationOrder": "Successively",
        "HashValidationScope": "File",
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 0,
          "DbCommandWaitTimeInMsBeforeRetry": 250
        }
      }
    },
    "Products": [
      {
        "Alias": "BookStore",
        "MigrationFilesRootDirectory": "./Migrations/BookStore",
        "TargetGroups": [
          {
            "Alias": "Backend",
            "DatabaseType": "SqlServer",
            "Targets": [
              {
                "Alias": "MainDB",
                "ConnectionString": ""
              }
            ]
          }
        ]
      }
    ]
  }
}
```

### Development Overrides — `appsettings.Development.json`

Development uses a local SQL Server with verbose logging:

```json
{
  "RayMigrator": {
    "Repository": {
      "ConnectionString": "Server=localhost;Database=BookStore_Repo;User Id=sa;Password=DevPass123;TrustServerCertificate=True"
    },
    "Products": [
      {
        "Alias": "BookStore",
        "TargetGroups": [
          {
            "Alias": "Backend",
            "Targets": [
              {
                "Alias": "MainDB",
                "ConnectionString": "Server=localhost;Database=BookStore;User Id=sa;Password=DevPass123;TrustServerCertificate=True"
              }
            ]
          }
        ]
      }
    ],
    "Serilog": {
      "MinimumLevel": {
        "Default": "Debug"
      },
      "WriteTo": [
        { "Name": "Console" }
      ]
    }
  }
}
```

### Production Overrides — `appsettings.Production.json`

Production uses environment variables for all secrets and minimal logging:

```json
{
  "RayMigrator": {
    "Repository": {
      "ConnectionString": "{ENV:BOOKSTORE_REPO_CONNECTION}"
    },
    "Products": [
      {
        "Alias": "BookStore",
        "TargetGroups": [
          {
            "Alias": "Backend",
            "Targets": [
              {
                "Alias": "MainDB",
                "ConnectionString": "{ENV:BOOKSTORE_DB_CONNECTION}"
              }
            ]
          }
        ]
      }
    ],
    "Serilog": {
      "MinimumLevel": {
        "Default": "Warning"
      },
      "WriteTo": [
        { "Name": "Console" }
      ]
    }
  }
}
```

---

## Repository Section

The `Repository` section configures the database where RayMigrator stores its tracking tables (migration history, run logs, and state).

```json
{
  "Repository": {
    "DatabaseType": "SqlServer",
    "ConnectionString": "{ENV:REPO_CONNECTION}",
    "SchemaName": "ray",
    "TableBaseName": "",
    "DbCommandTimeoutInSeconds": 60,
    "DbCommandMaxRetries": 100,
    "DbCommandWaitTimeInMsBeforeRetry": 250
  }
}
```

The key properties are `DatabaseType`, `ConnectionString`, and `SchemaName`. The optional `TableBaseName` property allows prefixing all repository table names (useful when sharing a schema with other applications). For the full property table, see [Repository Options](../06-configuration-reference/repository-options.md).

> **Note:** The repository database can be a different engine than your migration targets. For example, you can track migrations in a central PostgreSQL repository while migrating SQL Server databases.

---

## Product Configuration

Each product represents an independent set of migrations. Products are defined in the `Products` array. The required properties are `Alias`, `MigrationFilesRootDirectory`, and `TargetGroups`. For the full property table, see [Product Options](../06-configuration-reference/product-options.md).

> **Tip:** The `Alias` is what you pass to `--product` on the command line. Keep it short and meaningful: `BookStore`, `UserService`, `PaymentGateway`.

---

## Inheritance Chain

RayMigrator uses a cascading defaults system. Properties set at a higher level apply to all children unless explicitly overridden.

```
ProductDefaults
└── Product (overrides ProductDefaults)
    └── TargetGroupDefaults
        └── TargetGroup (overrides TargetGroupDefaults)
            └── TargetDefaults
                └── Target (overrides TargetDefaults)
```

**How it works:**

1. `ProductDefaults` defines baseline values for all products (including `MigrationErrorAction`, `RollbackErrorAction`, `MigrationFilesExtension`, `MigrationRollbackFilesPreExtension`, `MigrationFilesEncoding`, `RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, and `UseCliToolAlias`).
2. A specific `Product` can override any of those values.
3. Within a product, `TargetGroupDefaults` defines baseline values for all target groups (including `TargetMigrationOrder`, `HashValidationScope`, and `StopRollbackOnMissingRollbackFile`).
4. A specific `TargetGroup` can override any of those values.
5. Within a target group, `TargetDefaults` defines baseline values for all targets (including `DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, and `DbCommandWaitTimeInMsBeforeRetry`).
6. A specific `Target` can override any of those values.

**MigrationErrorAction** controls what happens when a migration fails:
- **Terminate** (default) — Stop immediately, no rollback.
- **Rollback** — Roll back all migrations performed by the current run.
- **RollbackErrorOnly** — Roll back only the file that caused the error.
- **RollbackRelease** — Roll back all migrations from the release that caused the error. Earlier releases remain intact.
- **Ignore** — Skip the error and continue with the next file.

**RollbackErrorAction** controls what happens when a rollback operation itself fails:
- **Terminate** (default) — Stop the rollback chain immediately.
- **Ignore** — Skip the error and continue with the next rollback file.

**Example:** If `ProductDefaults.RequireRollbackFile = true` but a specific product sets `RequireRollbackFile = false`, that product does not require rollback files while all other products still do.

**Example:** If `ProductDefaults.MigrationErrorAction = "Terminate"` but a specific product sets `"MigrationErrorAction": "Rollback"`, that product will rollback on error while all other products still terminate.

This reduces repetition. You only need to specify a value at the most specific level where it differs from the default.

---

## TargetGroup Configuration

A target group represents a logical grouping of database targets that share migration files. In the traditional layout, the target group `Alias` must match a subdirectory name in each release directory. When a product has exactly one target group, the flat layout is also supported: migration files can be placed directly under the release directory without a target group subdirectory. For the full property table and layout details, see [Target Group Options](../06-configuration-reference/target-group-options.md) and [Directory Structure](../07-migration-files/directory-structure.md).

**TargetMigrationOrder** controls iteration order for multi-target groups:
- **Successively** (default) — All files on one target before the next target. Safer default.
- **Simultaneously** — Each file on all targets before the next file. Keeps targets in sync.

**HashValidationScope** controls integrity checking granularity:
- **File** (default) — Hash entire file content. Strictest.
- **SqlBlocks** — Hash SQL content only (allows TOML metadata changes).
- **Disabled** — Skip validation entirely.

---

## Target Configuration

A target represents a single database instance that receives migrations. The required properties are `Alias` and `ConnectionString`. For the full property table, see [Target Options](../06-configuration-reference/target-options.md).

**Example with multiple targets:**

```json
{
  "TargetGroups": [
    {
      "Alias": "Backend",
      "DatabaseType": "SqlServer",
      "TargetMigrationOrder": "Simultaneously",
      "Targets": [
        {
          "Alias": "Primary",
          "ConnectionString": "{ENV:DB_PRIMARY}"
        },
        {
          "Alias": "Secondary",
          "ConnectionString": "{ENV:DB_SECONDARY}",
          "DbCommandTimeoutInSeconds": 60,
          "DbCommandMaxRetries": 3
        }
      ]
    }
  ]
}
```

---

## Environment Variables

RayMigrator supports environment variable substitution in any string configuration value using the `{ENV:VARIABLE_NAME}` syntax.

```json
{
  "ConnectionString": "{ENV:BOOKSTORE_DB_CONNECTION}",
  "SchemaName": "{ENV:SCHEMA_NAME}"
}
```

**Rules:**

- Variables are resolved at application startup from OS environment variables.
- The placeholder is replaced with the literal value of the environment variable.
- If the referenced environment variable does not exist or is not set, the placeholder is replaced with `null`. Subsequent configuration validation (e.g., for connection strings) then fails with a validation error, causing the application to terminate with an `ApplicationStartupException`. All validation errors are collected and reported together before aborting. See [Environment Variables](../06-configuration-reference/environment-variables.md) for full details.
- Placeholders can appear anywhere in a string value: `"Server={ENV:DB_HOST};Database=BookStore"`.
- Placeholders are case-sensitive: `{ENV:db_host}` and `{ENV:DB_HOST}` reference different variables.

> **Security:** Never hardcode passwords or secrets in configuration files. Always use `{ENV:}` placeholders for sensitive values like connection strings, API keys, and credentials. This prevents secrets from being committed to source control.

---

## Serilog Logging Configuration

RayMigrator uses Serilog for structured logging. The `Serilog` section configures console output, log levels, and optional sinks.

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ]
  }
}
```

| Level | Use |
|-------|-----|
| `Debug` | Detailed diagnostic output, SQL statements, file processing details |
| `Information` | Migration progress, start/end of operations |
| `Warning` | Non-fatal issues (hash mismatches on RunAlways files, missing rollback files) |
| `Error` | Migration failures, connection errors |
| `Fatal` | Unrecoverable errors |

> **Tip:** Use `Debug` during development to see exactly which files are processed and which SQL is executed. Switch to `Information` or `Warning` in production.

---

## Database Logging (Optional)

In addition to console logging, RayMigrator can write structured log entries to a database table. This is useful for auditing and centralized log collection.

```json
{
  "DatabaseLogging": {
    "DatabaseType": "SqlServer",
    "ConnectionString": "{ENV:LOG_CONNECTION}",
    "SchemaName": "ray",
    "TableBaseName": "",
    "MinimumLevel": "Information",
    "DbCommandTimeoutInSeconds": 20
  }
}
```

For the full property table, see [Logging Options](../06-configuration-reference/logging-options.md).

> **Note:** The database logging target can be the same database as the repository or a completely separate instance. RayMigrator creates the necessary log table automatically.

---

## Full Annotated Example

Below is a complete `appsettings.json` for the BookStore tutorial with annotations explaining each section.

```jsonc
{
  "RayMigrator": {

    // --- Repository: Where RayMigrator tracks migration state ---
    "Repository": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:BOOKSTORE_REPO_CONNECTION}",
      "SchemaName": "ray",
      "TableBaseName": "",
      "DbCommandTimeoutInSeconds": 60,
      "DbCommandMaxRetries": 100,
      "DbCommandWaitTimeInMsBeforeRetry": 250
    },

    // --- ProductDefaults: Baseline for all products ---
    "ProductDefaults": {
      "MigrationErrorAction": "Terminate",
      "RollbackErrorAction": "Terminate",
      "MigrationFilesExtension": "sql",
      "MigrationRollbackFilesPreExtension": "rollback",
      "MigrationFilesEncoding": "UTF-8",
      "RequireRollbackFile": true,
      "TargetGroupDefaults": {
        "TargetMigrationOrder": "Successively",
        "HashValidationScope": "File",
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 0,
          "DbCommandWaitTimeInMsBeforeRetry": 250
        }
      }
    },

    // --- Products: Define each product's migrations ---
    "Products": [
      {
        "Alias": "BookStore",
        "MigrationFilesRootDirectory": "./Migrations/BookStore",
        "TargetGroups": [
          {
            // TargetGroup Alias must match directory name under each release
            "Alias": "Backend",
            "DatabaseType": "SqlServer",
            "Targets": [
              {
                "Alias": "MainDB",
                "ConnectionString": "{ENV:BOOKSTORE_DB_CONNECTION}"
              }
            ]
          },
          {
            "Alias": "Reporting",
            "DatabaseType": "PostgreSQL",
            "TargetMigrationOrder": "Simultaneously",
            "Targets": [
              {
                "Alias": "ReportDB",
                "ConnectionString": "{ENV:BOOKSTORE_REPORT_CONNECTION}",
                "DbCommandTimeoutInSeconds": 120
              }
            ]
          }
        ]
      }
    ],

    // --- Serilog: Logging configuration ---
    "Serilog": {
      "MinimumLevel": {
        "Default": "Information",
        "Override": {
          "Microsoft": "Warning"
        }
      },
      "WriteTo": [
        { "Name": "Console" }
      ]
    },

    // --- CliTools: Optional external CLI tool execution ---
    // "CliTools": [{ ... }]
    // See: Docs/06-configuration-reference/cli-tools-options.md

    // --- DatabaseLogging: Optional persistent log storage ---
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:BOOKSTORE_LOG_CONNECTION}",
      "SchemaName": "ray",
      "TableBaseName": "",
      "MinimumLevel": "Information",
      "DbCommandTimeoutInSeconds": 20
    }
  }
}
```

---

## Validation

RayMigrator validates configuration at startup and reports clear error messages. Common validation errors include:

| Error | Cause | Fix |
|-------|-------|-----|
| `Alias is required` | Product, TargetGroup, or Target missing `Alias` | Add an `Alias` value |
| `Only letters, numbers and underscores with a maximum length of 50 characters are allowed.` | Invalid characters in Alias | Use only letters (Unicode), numbers, and `_` (max 50 characters) |
| `MigrationFilesRootDirectory does not exist` | Path does not exist on disk | Create the directory or fix the path |
| `Unknown DatabaseType` | Unsupported database engine | Use `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, or `Sqlite` |
| `ConnectionString is required` | Missing connection string | Add a `ConnectionString` or `{ENV:}` placeholder |
| `ConnectionString is empty after variable substitution` | `{ENV:VAR}` references a variable that does not exist, is empty, or is whitespace-only (startup terminates with `ApplicationStartupException`) | Set the environment variable to a non-empty value or fix the placeholder |
| `Duplicate Alias` | Two products, target groups, or targets share the same Alias | Rename one of the duplicates |

> **Tip:** Run with `--run-mode simulate` to validate your configuration without executing any migrations. This catches configuration errors before they reach your database.

---

## Next Steps

Now that the BookStore is configured for multiple environments, the next chapter covers how to write migration files, including TOML metadata, naming conventions, and rollback files.

See [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md) for the full configuration reference with every available property.

**Next:** [Chapter 05 — Writing Migration Files](05-migration-files.md)
