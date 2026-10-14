# Logging Options

RayMigrator supports both file/console logging (Serilog) and optional database logging. These two systems work together through a custom Serilog sink (`RayMigratorDatabaseSink`) that bridges the Serilog pipeline to the database logging infrastructure.

## Database Logging

Structured logging of every run into a database, so that RayMigrator Studio and operators can read what a run did next to its repository rows.

Database logging is **on by default and goes into the repository**: without a `DatabaseLogging` section, or with one that leaves values out, the missing values come from `Repository` (database type, connection string, schema, table prefix) and `MinimumLevel` is `Information`. The log tables `MigrationLog` and `MigrationEvent` are created in the repository schema on the first run. `"DatabaseLogging": { "Enabled": false }` switches database logging off. A section with its own `DatabaseType` that differs from the repository's describes a separate log database and must name its own `ConnectionString`.

> **Command restriction**: The DatabaseLogging sink (DB sink) only writes log entries for commands that change state: `migrate-up` / `migrate-down` in `Migrate` mode, `update-hash`, `baseline` and `fix` in `Migrate` mode. For `info`, `validate-hash`, `fix` in `Simulate` mode and `migrate-up` / `migrate-down` in `Validate` or `Simulate` mode the sink is silent and no log entries are written (`CommandProfile.WritesDatabaseLog`, #6); the log tables themselves are created by `DatabaseLogging_CheckCreate` in every run mode as soon as database logging is enabled, which it is by default, so even `info` on a fresh repository creates `MigrationLog` and `MigrationEvent`. Console and file logging (Serilog) always writes for all commands.

### Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Enabled` | bool | No | `true` | `false` switches database logging off |
| `DatabaseType` | string | No | `Repository.DatabaseType` | Database type (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite) |
| `ConnectionString` | string | Conditional | `Repository.ConnectionString` | Connection string; required when `DatabaseType` differs from the repository's |
| `SchemaName` | string | Conditional | `Repository.SchemaName` | Schema for logging tables (SqlServer, PostgreSQL; ignored for MariaDb, MySql, SQLite) |
| `TableBaseName` | string | No | `Repository.TableBaseName` | Table prefix; at most 16 characters (`RULE_4_4`) |
| `MinimumLevel` | string | No | Information | Minimum log level |
| `DbCommandTimeoutInSeconds` | int | No | 20 | Command timeout |

> **SQLite**: the log rows are written on their own connection while a migration runs. When the repository file is also a target, every target block holds the file's write lock and the log writer waits in the busy timeout; give the repository its own file (as for the heartbeat, see [Repository options](repository-options.md)).

The defaults from `Repository` apply only while `DatabaseType` is the repository's (left out or equal); `DatabaseLoggingDefaults` in `Raycoon.RayMigrator.Core.Configuration.Validation` fills the section before the data annotations and the rule catalog run, so a filled section is validated like a written one.

### Log Levels

| Value | Description |
|-------|-------------|
| `Trace` | Most verbose |
| `Debug` | Debug information |
| `Information` | General information |
| `Warning` | Warnings |
| `Error` | Errors |
| `Critical` | Critical errors |
| `None` | Disables logging |

> **Note**: `DatabaseLogging.MinimumLevel` uses `Microsoft.Extensions.Logging.LogLevel` values (Trace, Debug, Information, Warning, Error, Critical, None). The `Serilog` section uses Serilog's own level names (Verbose, Debug, Information, Warning, Error, Fatal). These are different enum systems -- do not mix them.

### Example Configuration

The default needs no section at all. To change only the level while the log stays in the repository:

```json
{
  "RayMigrator": {
    "DatabaseLogging": {
      "MinimumLevel": "Debug"
    }
  }
}
```

To switch database logging off:

```json
{
  "RayMigrator": {
    "DatabaseLogging": {
      "Enabled": false
    }
  }
}
```

A fully written section:

```json
{
  "RayMigrator": {
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:LOG_CONNECTION}",
      "SchemaName": "logs",
      "TableBaseName": "",
      "MinimumLevel": "Information",
      "DbCommandTimeoutInSeconds": 20
    }
  }
}
```

### Separate Logging Database

```json
{
  "Repository": {
    "ConnectionString": "{ENV:REPO_CONNECTION}"
  },
  "DatabaseLogging": {
    "DatabaseType": "SqlServer",
    "ConnectionString": "{ENV:LOG_CONNECTION}",
    "SchemaName": "logs"
  }
}
```

### Same Database as Repository, Other Schema

The default puts the log tables into the repository schema. To keep them in the repository database but in a schema of their own, name only the schema:

```json
{
  "Repository": {
    "ConnectionString": "{ENV:REPO_CONNECTION}",
    "SchemaName": "migrations"
  },
  "DatabaseLogging": {
    "SchemaName": "logs"
  }
}
```

### Architecture

Database logging uses a multi-component pipeline:

1. **`RayMigratorDatabaseSink`** -- A custom Serilog `ILogEventSink` that intercepts all Serilog log events. It extracts enriched migration-context properties from each log event and delegates to `DatabaseLogWriter` for persistence. The sink is created early during startup with a deferred writer (set later once the DAL is initialized).

2. **`MigrationContextEnricher`** -- A Serilog `ILogEventEnricher` that reads `MigrationLoggingContext.Current` (an `AsyncLocal<MigrationContext>` ambient context) and adds migration-specific properties to every log event:
   - `Environment`, `MigrationRunId`, `TargetGroupAlias`, `TargetAlias`, `MigrationFilename`, `MigrationFileId`, `MigrationBlockId` (for Serilog console/file output)
   - `RunModeId`, `ProductId`, `EnvironmentId`, `MigrationRecordId`, `ReleaseVersion`, `FileName`, `FileOrderId`, `FileBlockId` (additional properties for the database sink)

3. **`DatabaseLogWriter`** -- A service that enqueues log entries for asynchronous writing via `DatabaseLoggerQueue`. It validates the minimum log level and converts Serilog log events into DAL parameter lists for SQL template execution.

4. **`DatabaseLoggerQueue`** -- A `BlockingCollection<Action>` background queue that processes log writes on a background thread (via `Task.Factory.StartNew` with `LongRunning`). Errors during database writes are caught and written to `Console.Error.WriteLine` to avoid cascading failures.

5. **`MigrationLoggingContext`** -- A static class with an `AsyncLocal<MigrationContext?>` field. Set once when the `MigrationContext` becomes available; the enricher reads it automatically for every log event.

### Shutdown Flush Behavior

At shutdown, RayMigrator calls `Flush()` on the `DatabaseLogWriter`, which signals the queue to stop accepting new entries and waits for all pending entries to be processed:

```csharp
dbLogWriter.Flush();
await host.StopAsync();
```

`DatabaseLogWriter.Flush()` delegates to `DatabaseLoggerQueue.Flush()`, which calls `BlockingCollection.CompleteAdding()` and then waits for the background processing task to finish. After the host stops, `Log.CloseAndFlushAsync()` is called to flush all Serilog sinks (console, file, etc.).

A host that runs several migrations in one process (RayMigrator Studio) calls `DatabaseLogWriter.FlushAsync(timeout)` at the end of a run instead: it waits until every queued entry is written and leaves the queue open for the next run.

## Serilog Configuration

Console and file logging via Serilog. The `Serilog` section must be placed **inside** the `RayMigrator` section.

RayMigrator reads Serilog configuration from the `RayMigrator` section (not the root) using `ReadFrom.Configuration(rayMigratorSection)`. Two enrichers are always registered programmatically regardless of configuration:
- `Enrich.FromLogContext()` -- enables Serilog's `LogContext` push properties
- `Enrich.With(new MigrationContextEnricher())` -- adds migration-specific properties to all log events

### Serilog NuGet Packages

The Console project includes the following Serilog packages:

| Package | Version | Purpose |
|---------|---------|---------|
| `Serilog` | 4.4.0 | Core library |
| `Serilog.Enrichers.Environment` | 3.0.1 | `WithMachineName`, `WithEnvironmentUserName` |
| `Serilog.Enrichers.Thread` | 4.0.0 | `WithThreadId` |
| `Serilog.Extensions.Hosting` | 10.0.0 | `.UseSerilog()` host integration |
| `Serilog.Settings.Configuration` | 10.0.1 | JSON configuration binding |
| `Serilog.Sinks.Console` | 6.1.1 | Console output |
| `Serilog.Sinks.File` | 7.0.0 | File output with rolling |
| `Raycoon.Serilog.Sinks.SQLite` | 1.2.2 | SQLite file-based Serilog sink |

Package versions are pinned centrally in `Directory.Packages.props` (Central Package Management); the table reflects that file at the time of writing.

> **Note**: `Raycoon.Serilog.Sinks.SQLite` is a Serilog file sink that writes log events to a local SQLite file. This is separate from the `DatabaseLogging` feature which writes structured migration logs to a migration database via DAL and SQL templates.

### Basic Configuration

```json
{
  "RayMigrator": {
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
}
```

### Full Configuration

```json
{
  "RayMigrator": {
    "Serilog": {
      "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File", "Raycoon.Serilog.Sinks.SQLite"],
      "MinimumLevel": {
        "Default": "Debug",
        "Override": {
          "Microsoft": "Warning",
          "System": "Warning"
        }
      },
      "Enrich": [
        "WithMachineName",
        "WithThreadId",
        "WithEnvironmentUserName",
        "FromLogContext"
      ],
      "WriteTo": [
        {
          "Name": "Console",
          "Args": {
            "restrictedToMinimumLevel": "Information",
            "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
            "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Message:lj}{NewLine}{Exception}"
          }
        },
        {
          "Name": "File",
          "Args": {
            "restrictedToMinimumLevel": "Debug",
            "path": "/tmp/RayMigratorLog.txt",
            "rollingInterval": "Day",
            "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] <{ThreadId}> [{SourceContext:l}] {Message:lj} - (RunId:{MigrationRunId} BlockId:{MigrationBlockId}) {NewLine}{Exception}"
          }
        },
        {
          "Name": "SQLite",
          "Args": {
            "restrictedToMinimumLevel": "Debug",
            "databasePath": "/tmp/RayMigratorLog.db",
            "tableName": "Logs",
            "batchSizeLimit": 1,
            "autoCreateDatabase": true
          }
        }
      ]
    }
  }
}
```

### Per-Sink Minimum Level

Each sink can define its own `restrictedToMinimumLevel` independently of the global `MinimumLevel.Default`. This allows different verbosity for different outputs:

```json
{
  "WriteTo": [
    {
      "Name": "Console",
      "Args": { "restrictedToMinimumLevel": "Information" }
    },
    {
      "Name": "File",
      "Args": { "restrictedToMinimumLevel": "Debug" }
    }
  ]
}
```

> **Note**: The global `MinimumLevel.Default` acts as a floor -- events below that level are discarded before reaching any sink. Set the global level to the most verbose sink level, then restrict individual sinks as needed.

### Migration Context Properties in Output Templates

The `MigrationContextEnricher` adds the following properties to every log event, which can be used in Serilog output templates:

| Property | Description |
|----------|-------------|
| `{Environment}` | Target environment name |
| `{MigrationRunId}` | Current migration run ID |
| `{TargetGroupAlias}` | Target group alias |
| `{TargetAlias}` | Target database alias |
| `{MigrationFilename}` | Migration filename with relative path |
| `{MigrationFileId}` | File execution order ID |
| `{MigrationBlockId}` | SQL block index within the file |

Example output template using enriched properties:

```
"{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] <{ThreadId}> [{SourceContext:l}] {Message:lj} - (RunId:{MigrationRunId} BlockId:{MigrationBlockId}) {NewLine}{Exception}"
```

### Environment-Specific Logging

**appsettings.Development.json** (inside RayMigrator section):
```json
{
  "RayMigrator": {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Debug"
      }
    }
  }
}
```

**appsettings.Production.json** (inside RayMigrator section):
```json
{
  "RayMigrator": {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Warning"
      }
    }
  }
}
```

### Bootstrap Serilog Setup

Serilog is configured early during startup, before the full DI container is built. The initialization sequence is:

1. All configuration files (up to 4 levels) are loaded and merged into the `RayMigrator` configuration section, and `{ENV:...}` placeholders are replaced
2. Unless the `DatabaseLogging` section says `Enabled: false`, a `RayMigratorDatabaseSink` is created with the configured minimum level, `Information` when none is written (deferred -- no writer yet)
3. `LoggerConfiguration` reads from the `RayMigrator` section, registers enrichers, and optionally adds the database sink
4. `Log.Logger` is set as the global Serilog logger
5. The DI host is built with `.UseSerilog()` integration
6. After DI resolution, `MigrationLoggingContext.Current` is set so the enricher can add migration properties to all subsequent log events
7. The `DatabaseLogWriter` is initialized with its DAL and SQL templates
8. The deferred `RayMigratorDatabaseSink` receives the writer via `SetWriter()` -- database logging starts

This deferred pattern ensures that console/file logging works from the very start, while database logging activates only after the DAL infrastructure is ready.

Serilog configuration is read from the fully merged `RayMigrator` configuration section. Because the entire configuration hierarchy is loaded before Serilog is initialized, Serilog settings from product-specific or environment-specific files (levels 3 and 4 of the hierarchy) are also applied.

## Log Output Format

### Console Output

Default output template (when no `outputTemplate` is specified):
```
2025-01-29 10:30:45 [INF] Starting migrate-up for RayMigratorTests
2025-01-29 10:30:45 [INF] Repository version: 1
2025-01-29 10:30:46 [INF] Executing: 001_CreateTable.sql
2025-01-29 10:30:46 [INF] Migration completed: 1/1 migrations
```

With the 4-character level format (`{Level:u4}`) used in the test configurations:
```
2025-01-29 10:30:45 [INFO] Starting migrate-up for RayMigratorTests
2025-01-29 10:30:45 [INFO] Repository version: 1
2025-01-29 10:30:46 [INFO] Executing: 001_CreateTable.sql
2025-01-29 10:30:46 [INFO] Migration completed: 1/1 migrations
```

### File Output (with enriched properties)

Using the output template from the test configurations:
```
2025-01-29 10:30:45 [INFO] <12> [Raycoon.RayMigrator.Services.MigrationService] Starting migrate-up - (RunId:1 BlockId:0)
2025-01-29 10:30:45 [DBUG] <12> [Raycoon.RayMigrator.Services.MigrationService] Loading configuration - (RunId:1 BlockId:0)
2025-01-29 10:30:45 [INFO] <12> [Raycoon.RayMigrator.Services.MigrationService] Repository version: 1 - (RunId:1 BlockId:0)
```

## Sensitive Data Logging

Control via `--reveal-sensitive-data` / `-rsd` command-line option.

RayMigrator uses a `SensitiveDataMasker` that is initialized once during startup based on this flag. It maintains a thread-safe set of registered sensitive values (connection strings, schema names, environment variable values) and replaces them with `*** HIDDEN ***` in log output.

**Default (false)**:
```
[INF] Repository Connection: *** HIDDEN ***
```

**With --reveal-sensitive-data true**:
```
[INF] Repository Connection: Server=localhost;Password=secret123
```

At the `Verbose` (Trace) log level, the full resolved configuration is logged. Without `--reveal-sensitive-data`, all registered sensitive values are masked in this output.

## Best Practices

1. **Production**: Use `Warning` or `Error` for `MinimumLevel.Default`; use per-sink `restrictedToMinimumLevel` for file sinks that need more detail
2. **Development**: Use `Debug` level
3. **Database logging**: The repository is the default home of the log; move it to a separate database when log growth or retention must not touch the repository
4. **File logging**: Enable rolling with retention limit (`retainedFileCountLimit`)
5. **Sensitive data**: Never enable `--reveal-sensitive-data` in production
6. **Output templates**: Use `{Level:u4}` for consistent 4-character level names (INFO, DBUG, WARN, EROR, FATL); use enricher properties like `{MigrationRunId}` in file sinks for correlation
7. **Parallel integration tests**: When running multiple database engines in parallel, use instance loggers with `UseSerilog(logger, dispose: false)` instead of the static `Log.Logger` to avoid `Log.CloseAndFlushAsync()` in one engine killing logging for all engines

## Related Documentation

- [Logging Schema](../03-database-layer/logging-schema.md)
- [Repository Options](repository-options.md)
- [Environment Variables](environment-variables.md)
