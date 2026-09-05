# Chapter 2 — Quick Start: Your First Migration in 10 Minutes

This chapter walks you through creating a minimal project, writing your first migration, and executing it. By the end, you will have a working BookStore database with a `Books` table and a fully tracked migration history.

## Prerequisites

- **.NET 8 or later** installed (`dotnet --version` to verify) — [download here](https://dotnet.microsoft.com/download)
- **A SQL Server instance** — either a local installation or a Docker container
- **RayMigrator** available on your PATH — download the latest release for your platform from [GitHub Releases](https://github.com/RAYCOON/RayMigrator/releases), extract it, and add the directory to your PATH:

  **Linux:** `tar -xzf RayMigrator-<version>-linux-x64.tar.gz -C /opt/raymigrator && export PATH="$PATH:/opt/raymigrator"`

  **macOS:** `tar -xzf RayMigrator-<version>-osx-arm64.tar.gz -C /usr/local/raymigrator && export PATH="$PATH:/usr/local/raymigrator"`

  **Windows:** `Expand-Archive RayMigrator-<version>-win-x64.zip -DestinationPath C:\Tools\RayMigrator` and add `C:\Tools\RayMigrator` to your system PATH.

  Verify with `raymigrator --version`.

> **Tip:** Tired of typing the full name? You can define a short shell alias such as `raymig` — see [Shortening the Command with a Shell Alias](06-cli-commands.md#shortening-the-command-with-a-shell-alias) in Chapter 6.

> **Tip:** To start a SQL Server container quickly:
> ```bash
> docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStr0ngP@ssword" \
>   -p 1433:1433 --name bookstore-db -d mcr.microsoft.com/mssql/server:2022-latest
> ```

## Step 1 — Create the Directory Structure

RayMigrator discovers migration files by scanning a directory tree. The structure encodes releases and target groups:

```
BookStore/
├── appsettings.json
└── Migrations/
    └── Release 1.0/
        └── Backend/
            └── 001_CreateBooks.sql
```

Create the directories:

```bash
mkdir -p BookStore/Migrations/"Release 1.0"/Backend
```

The path segments have specific meaning:

| Segment         | Purpose |
|-----------------|---------|
| `Migrations/`   | Root directory configured in `MigrationFilesRootDirectory` |
| `Release 1.0/`  | A release — migrations are grouped and ordered by release |
| `Backend/`       | Must match a TargetGroup alias defined in configuration |
| `001_CreateBooks.sql` | The migration file — numeric prefix controls execution order |

## Step 2 — Write the Configuration

Create `BookStore/appsettings.json` with the following content:

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}",
      "SchemaName": "ray"
    },
    "ProductDefaults": {
      "MigrationErrorAction": "Terminate",
      "MigrationFilesExtension": "sql",
      "MigrationRollbackFilesPreExtension": "rollback",
      "MigrationFilesEncoding": "UTF-8",
      "RequireRollbackFile": false,
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
        "MigrationFilesRootDirectory": "./Migrations",
        "TargetGroups": [
          {
            "Alias": "Backend",
            "DatabaseType": "SqlServer",
            "Targets": [
              {
                "Alias": "MainDB",
                "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}"
              }
            ]
          }
        ]
      }
    ],
    "Serilog": {
      "MinimumLevel": {
        "Default": "Information"
      },
      "WriteTo": [
        { "Name": "Console" }
      ]
    }
  }
}
```

Key points about this configuration:

| Setting | Value | Purpose |
|---------|-------|---------|
| `Repository.SchemaName` | `ray` | All tracking tables are created in this schema |
| `MigrationErrorAction` | `Terminate` | Stop immediately if any migration fails |
| `RequireRollbackFile` | `false` | Rollback files are optional for now (we add them in Chapter 9) |
| `TargetMigrationOrder` | `Successively` | Apply all files to one target before moving to the next |
| `{ENV:BOOKSTORE_CONNECTION}` | — | Replaced at runtime with the environment variable value |
| `Serilog` | Console sink | Controls log output; without it, no migration progress is displayed |

For a complete reference of all configuration options, see [Configuration Reference](../06-configuration-reference/product-options.md).

## Step 3 — Write Your First Migration

Create `BookStore/Migrations/Release 1.0/Backend/001_CreateBooks.sql`:

```sql
/*
[RayMigrator]
Description = "Create Books table"
UseTransaction = true
*/

CREATE TABLE [dbo].[Books]
(
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [Title]         NVARCHAR(200) NOT NULL,
    [Author]        NVARCHAR(100) NOT NULL,
    [ISBN]          VARCHAR(13) NULL,
    [PublishedDate]  DATE NULL,
    [Price]         DECIMAL(10,2) NOT NULL,
    CONSTRAINT [PK_Books] PRIMARY KEY ([Id])
);
```

The file has two parts:

1. **TOML metadata** inside a block comment (`/* ... */`) — controls how RayMigrator handles this file
2. **SQL body** — the actual database commands to execute

The `Description` appears in logs and in the repository for easy identification. `UseTransaction = true` wraps the execution in a transaction so the change is atomic.

> **Note:** The TOML header is optional. Without it, RayMigrator uses sensible defaults. Chapter 5 covers all available TOML fields.

## Step 4 — Set the Environment Variable

RayMigrator replaces `{ENV:VARIABLE_NAME}` placeholders in configuration with environment variable values at runtime. Set the connection string:

**Linux / macOS:**

```bash
export BOOKSTORE_CONNECTION="Server=localhost;Database=BookStore;User Id=sa;Password=YourStr0ngP@ssword;TrustServerCertificate=True"
```

**Windows (PowerShell):**

```powershell
$env:BOOKSTORE_CONNECTION = "Server=localhost;Database=BookStore;User Id=sa;Password=YourStr0ngP@ssword;TrustServerCertificate=True"
```

> **Warning:** Make sure the database `BookStore` exists on the server before running migrations. RayMigrator creates tables inside the database but does not create the database itself.

Create the database if it does not exist:

```bash
# Using sqlcmd
sqlcmd -S localhost -U sa -P "YourStr0ngP@ssword" -C -Q "CREATE DATABASE BookStore"

# Or via Docker
docker exec bookstore-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStr0ngP@ssword" -C \
  -Q "CREATE DATABASE BookStore"
```

## Step 5 — Run the Migration

Navigate to the `BookStore/` directory and execute:

```bash
raymigrator migrate-up --product BookStore --environment Development --run-mode migrate
```

| Flag | Purpose |
|------|---------|
| `migrate-up` | Apply pending migrations forward |
| `--product BookStore` | Match the `Alias` in configuration |
| `--environment Development` | Used for environment-based filtering (Chapter 5) |
| `--run-mode migrate` | Actually execute the SQL (`Simulate` previews, `Validate` checks files only) |

You should see output similar to:

```
[INF] Starting migration run for product 'BookStore'
[INF] Environment: Development
[INF] Release 1.0 / Backend / MainDB — 001_CreateBooks.sql — Migrated
[INF] Migration run completed. Result: Ok
```

> **Tip:** Run with `--run-mode simulate` first to preview what would be executed without making any changes, or use `--run-mode validate` to check files and configuration without connecting to any database:
> ```bash
> raymigrator migrate-up --product BookStore --environment Development --run-mode simulate
> ```

## Step 6 — Verify the Result

After a successful run, two things have happened in your `BookStore` database:

### Your Application Table

The `Books` table now exists in the `dbo` schema:

```sql
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'Books';
```

| TABLE_SCHEMA | TABLE_NAME |
|--------------|------------|
| dbo          | Books      |

### The Repository Tables

RayMigrator created a `ray` schema with tracking tables:

```sql
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'ray'
ORDER BY TABLE_NAME;
```

| TABLE_SCHEMA | TABLE_NAME              |
|--------------|-------------------------|
| ray          | Environment             |
| ray          | MigrationOperation      |
| ray          | MigrationRecord         |
| ray          | MigrationRecordHistory  |
| ray          | MigrationRun            |
| ray          | MigrationRunMeta        |
| ray          | MigrationRunMode        |
| ray          | MigrationRunResult      |
| ray          | MigrationStatus         |
| ray          | MigratorMeta            |
| ray          | Product                 |

The `MigrationRun` table contains one record for the run you just executed. The `MigrationRecord` table contains one record for `001_CreateBooks.sql`, including its SHA-256 hash, status (`Migrated = 100`), and timestamps.

### Hash Integrity

The file's SHA-256 hash is now stored in the repository. If anyone modifies `001_CreateBooks.sql` after execution, the `validate-hash` command will detect the tampering:

```bash
raymigrator validate-hash --product BookStore --environment Development
```

## What Just Happened?

Here is the lifecycle of the migration you just ran:

1. **Configuration loaded** — RayMigrator read `appsettings.json`, replaced `{ENV:BOOKSTORE_CONNECTION}` with the actual connection string, and built the product/target graph.
2. **Files discovered** — The `Migrations/` directory was scanned. `Release 1.0/Backend/001_CreateBooks.sql` was found and matched to the `Backend` target group.
3. **Repository checked** — RayMigrator queried the `ray.MigrationRecord` table. Since the repository was empty (first run), the file was marked as pending.
4. **SQL executed** — The SQL body was extracted (excluding the TOML comment), wrapped in a transaction, and executed against the `MainDB` target.
5. **Result recorded** — The migration status (`Migrated`), the file hash, and timestamps were written to the repository.

This same lifecycle applies to every migration run, whether it contains one file or hundreds.

## What's Next

Now that you have a working migration, the next chapter explains the core concepts — the Product/TargetGroup/Target hierarchy, releases, the repository, and the migration status lifecycle — so you understand *why* the directory structure and configuration are organized the way they are.

**Next:** [Chapter 03 — Core Concepts](03-concepts.md)
