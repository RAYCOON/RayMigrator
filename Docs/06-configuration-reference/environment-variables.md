# Environment Variables

All string configuration values support `{ENV:VARIABLE_NAME}` placeholder syntax.

## Syntax

```
{ENV:VARIABLE_NAME}
```

Variable names must consist of word characters only: letters, digits, and underscores (`[a-zA-Z0-9_]`). The underlying regex pattern is `\{ENV:(\w+)\}`.

**Example**:
```json
{
  "ConnectionString": "{ENV:REPO_CONNECTION}"
}
```

## Resolution

Placeholders are resolved at runtime before configuration is bound to options classes:

1. Configuration files are loaded and merged into an `IConfigurationSection`
2. `{ENV:VARIABLE_NAME}` placeholders are found via regex matching
3. Each placeholder is replaced with the corresponding environment variable value (via `Environment.GetEnvironmentVariable`)
4. Resolved environment variable values are registered with the `SensitiveDataMasker` for automatic masking in trace logs
5. Configuration is bound to strongly-typed options classes

## Environment Variables in SQL Migration Files

The same `{ENV:VARIABLE_NAME}` syntax can be used directly in SQL migration file content. This allows dynamic values like admin usernames, default passwords, or environment-specific settings to be injected into migration files at execution time.

### Key Differences from Configuration Replacement

| Aspect | Configuration | SQL Migration Files |
|--------|--------------|---------------------|
| **When** | At startup, before config binding | At execution time, before `ExecuteNonQueryAsync` |
| **Scope** | `appsettings.json` values and DAL SQL template content | SQL content in `.sql` migration and rollback files |
| **Hash impact** | N/A | None — hashes are computed on the **original** SQL content |
| **Missing variable** | Logged as error, terminates application (`ApplicationStartupException` for config values, `ConfigurationValidationException` for DAL templates) | Replaced with empty string, logged as warning |

### Example

```sql
/*
[RayMigrator]
Description = "Seed default admin user"
UseTransaction = true
*/

INSERT INTO Users (Username, Email, IsActive)
VALUES ('{ENV:DEFAULT_ADMIN}', '{ENV:DEFAULT_ADMIN_EMAIL}', 1);
```

With environment variables set:
```bash
export DEFAULT_ADMIN="superadmin"
export DEFAULT_ADMIN_EMAIL="admin@example.com"
```

The executed SQL becomes:
```sql
INSERT INTO Users (Username, Email, IsActive)
VALUES ('superadmin', 'admin@example.com', 1);
```

### Applies To

- **migrate-up (Migrate mode)**: All SQL blocks in migration files are replaced before execution
- **migrate-up (Simulate mode)**: Replacement happens (for trace logging), but SQL is not executed against databases
- **migrate-up (Validate mode)**: No replacement (SQL is not executed; only configuration and file validity are checked)
- **migrate-down / Rollback (Migrate mode)**: All SQL blocks in rollback files are replaced before execution
- **Baseline**: No replacement (SQL is not executed, only repository records are written)

## Environment Variables in DAL SQL Templates

The `{ENV:VARIABLE_NAME}` syntax is also supported in DAL SQL template files (loaded by `TemplateCache` at startup). This allows database-specific SQL templates (e.g., repository schema creation scripts) to reference environment-dependent values.

The behavior matches configuration replacement: missing or empty environment variables are logged as errors and cause the application to terminate with a `ConfigurationValidationException`.

## Common Variables

| Variable | Purpose | Example Value |
|----------|---------|---------------|
| `DOTNET_ENVIRONMENT` | Environment name | Docker, Development, Production |
| `MigrationFilesRootDirectory` | Migration files path | /app/migrations |
| `REPO_CONNECTION` | Repository database | Server=localhost;... |
| `LOG_CONNECTION` | Logging database | Server=localhost;... |
| `ConnectionString_*` | Target databases | Server=localhost;... |

## Setting Environment Variables

### Windows (Command Prompt)

```batch
set REPO_CONNECTION=Server=localhost;Database=RayMigrator;User Id=sa;Password=pass
set MigrationFilesRootDirectory=C:\migrations
```

### Windows (PowerShell)

```powershell
$env:REPO_CONNECTION = "Server=localhost;Database=RayMigrator;User Id=sa;Password=pass"
$env:MigrationFilesRootDirectory = "C:\migrations"
```

### Linux/macOS

```bash
export REPO_CONNECTION="Server=localhost;Database=RayMigrator;User Id=sa;Password=pass"
export MigrationFilesRootDirectory="/app/migrations"
```

### Persistent (Linux/macOS)

```bash
# Add to ~/.bashrc or ~/.zshrc
echo 'export REPO_CONNECTION="..."' >> ~/.bashrc
source ~/.bashrc
```

### Docker

**Dockerfile**:
```dockerfile
ENV REPO_CONNECTION="Server=db;Database=RayMigrator;..."
ENV MigrationFilesRootDirectory="/migrations"
```

**docker-compose.yml**:
```yaml
services:
  raymigrator:
    environment:
      - REPO_CONNECTION=Server=db;Database=RayMigrator;...
      - MigrationFilesRootDirectory=/migrations
```

**docker run**:
```bash
docker run -e REPO_CONNECTION="..." raymigrator
```

### launchSettings.json (IDE)

```json
{
  "profiles": {
    "Docker": {
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Docker",
        "REPO_CONNECTION": "Server=localhost;...",
        "MigrationFilesRootDirectory": "Testing/MigrationFiles"
      }
    }
  }
}
```

## Example Configuration

### appsettings.json

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "{ENV:REPO_DB_TYPE}",
      "ConnectionString": "{ENV:REPO_CONNECTION}",
      "SchemaName": "{ENV:REPO_SCHEMA}"
    },
    "Products": [{
      "Alias": "{ENV:PRODUCT_NAME}",
      "MigrationFilesRootDirectory": "{ENV:MigrationFilesRootDirectory}",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "{ENV:TARGET_DB_TYPE}",
        "Targets": [{
          "Alias": "Primary",
          "ConnectionString": "{ENV:PRIMARY_CONNECTION}"
        }]
      }]
    }]
  }
}
```

### Environment Setup

```bash
export REPO_DB_TYPE=SqlServer
export REPO_CONNECTION="Server=localhost;Database=RayMigrator;..."
export REPO_SCHEMA=migrations
export PRODUCT_NAME=MyProduct
export MigrationFilesRootDirectory=/app/migrations
export TARGET_DB_TYPE=SqlServer
export PRIMARY_CONNECTION="Server=localhost;Database=MyApp;..."
```

## Best Practices

### 1. Use for Sensitive Data

```json
{
  "ConnectionString": "{ENV:DB_PASSWORD_CONNECTION}"
}
```

Never hardcode passwords in configuration files.

### 2. Use for Environment-Specific Values

```json
{
  "ConnectionString": "{ENV:REPO_CONNECTION}"
}
```

Different values per environment without changing config files.

### 3. Use for Deployment Flexibility

```json
{
  "MigrationFilesRootDirectory": "{ENV:MigrationFilesRootDirectory}"
}
```

Different paths in different deployment environments.

### 4. Document Required Variables

Create an `example.env` file:

```bash
# Required environment variables for RayMigrator
DOTNET_ENVIRONMENT=Development
REPO_CONNECTION=Server=localhost;Database=RayMigrator;...
MigrationFilesRootDirectory=./migrations
```

### 5. Validate at Startup

RayMigrator validates all environment variable placeholders at startup. Unresolved placeholders (missing, empty, or whitespace-only environment variables) are logged as **errors** and cause the application to **terminate** with an `ApplicationStartupException`. Each unresolved variable is logged individually before termination, so all missing variables are reported in a single startup attempt.

## Troubleshooting

### Placeholder Not Resolved

**Symptom**: `{ENV:VAR_NAME}` appears in logs or errors

**Causes**:
1. Variable not set in environment
2. Variable name typo
3. Variable not exported (Linux/macOS)

**Fix**: Verify variable is set:
```bash
echo $VAR_NAME
# or
printenv VAR_NAME
```

### Wrong Value

**Symptom**: Configuration uses wrong value

**Causes**:
1. Variable set with wrong value
2. Multiple sources (shell, IDE, Docker) conflicting

**Fix**: Check all environment sources

### Windows Path Issues

**Symptom**: Path not found

**Cause**: Backslashes in Windows paths

**Fix**: Use forward slashes or double backslashes:
```batch
set MigrationFilesRootDirectory=C:/migrations
# or
set MigrationFilesRootDirectory=C:\\migrations
```

## Related Documentation

- [Configuration Hierarchy](appsettings-hierarchy.md)
- [Launch Profiles](../05-console-layer/launch-profiles.md)
- [Repository Options](repository-options.md)
