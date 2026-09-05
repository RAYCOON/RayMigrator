# Launch Profiles

Launch profiles in `launchSettings.json` enable IDE debugging with pre-configured environment variables and command-line arguments.

## Location

`Raycoon.RayMigrator.Console/Properties/launchSettings.json`

> **Multi-target note**: The Console project targets `net10.0;net9.0;net8.0`. When using `dotnet run`, you must specify the framework explicitly, e.g. `dotnet run --framework net10.0`.

## Purpose

- Configure environment variables for debugging
- Set default command-line arguments
- Enable quick switching between configurations
- Support multiple test scenarios

## Structure

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "ProfileName": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Command -Option Value",
      "environmentVariables": {
        "VAR_NAME": "value"
      }
    }
  }
}
```

## Existing Profiles

The project includes profiles for each supported database type, with Mac/Win variants per platform:

| Profile | Database | Product Alias |
|---------|----------|---------------|
| `Docker_Mac_SqlServer` | SQL Server | `RM_Tests_Mac_SqlServer` |
| `Docker_Mac_SqlServer_Validate` | SQL Server | `RM_Tests_Mac_SqlServer` |
| `Docker_Mac_SqlServer_Simulate` | SQL Server | `RM_Tests_Mac_SqlServer` |
| `Docker_Win_SqlServer` | SQL Server | `RM_Tests_Win_SqlServer` |
| `Docker_Mac_MariaDb` | MariaDB | `RM_Tests_Mac_MariaDb` |
| `Docker_Win_MariaDb` | MariaDB | `RM_Tests_Win_MariaDb` |
| `Docker_Mac_MySql` | MySQL | `RM_Tests_Mac_MySql` |
| `Docker_Win_MySql` | MySQL | `RM_Tests_Win_MySql` |
| `Docker_Mac_PostgreSQL` | PostgreSQL | `RM_Tests_Mac_PostgreSQL` |
| `Docker_Win_PostgreSQL` | PostgreSQL | `RM_Tests_Win_PostgreSQL` |
| `Docker_Mac_Success_SqlServer` | SQL Server | `RM_Tests_Mac_Success_SqlServer` |
| `Docker_Win_Success_SqlServer` | SQL Server | `RM_Tests_Win_Success_SqlServer` |
| `Docker_Mac_Success_PostgreSQL` | PostgreSQL | `RM_Tests_Mac_Success_PostgreSQL` |
| `Docker_Win_Success_PostgreSQL` | PostgreSQL | `RM_Tests_Win_Success_PostgreSQL` |
| `Docker_Mac_Success_MariaDb` | MariaDB | `RM_Tests_Mac_Success_MariaDb` |
| `Docker_Win_Success_MariaDb` | MariaDB | `RM_Tests_Win_Success_MariaDb` |
| `Docker_Mac_Success_MySql` | MySQL | `RM_Tests_Mac_Success_MySql` |
| `Docker_Win_Success_MySql` | MySQL | `RM_Tests_Win_Success_MySql` |
| `Staging` | SQL Server | `RayMigratorTests` |
| `Production` | SQL Server | `RayMigratorTests` |

**Notes:**
- Docker profiles use `-env {ENV:DOTNET_ENVIRONMENT}` to read the environment from the profile's environment variables.
- The `_Validate` and `_Simulate` variants (e.g. `Docker_Mac_SqlServer_Validate`) use `--run-mode Validate` / `--run-mode Simulate` with the same product alias.
- The "Success" profiles use separate product aliases (e.g. `RM_Tests_Mac_Success_SqlServer`) for success-only test scenarios.
- Staging and Production profiles hardcode `--environment Staging` / `--environment Production` directly in the command-line arguments.
- MariaDB profiles use port 3306, while MySQL profiles use port 3307.
- MariaDB, MySQL, and PostgreSQL profiles define `ConnectionString_Frontend`; SQL Server profiles do not.

## Example Configuration

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "Docker_Mac_SqlServer": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Migrate-Up --product RM_Tests_Mac_SqlServer -env {ENV:DOTNET_ENVIRONMENT} --run-mode Migrate",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Docker",
        "ConnectionString_Backend1": "Server=localhost;Initial Catalog=Backend_1;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!",
        "ConnectionString_Backend2": "Server=localhost;Initial Catalog=Backend_2;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!"
      }
    },

    "Docker_Mac_MariaDb": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Migrate-Up --product RM_Tests_Mac_MariaDb -env {ENV:DOTNET_ENVIRONMENT} --run-mode Migrate",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Docker",
        "ConnectionString_Backend1": "Server=localhost;Port=3306;Database=raydb;User Id=rayuser;Password=raypass123",
        "ConnectionString_Backend2": "Server=localhost;Port=3306;Database=raydb2;User Id=rayuser;Password=raypass123",
        "ConnectionString_Frontend": "Server=localhost;Port=3306;Database=raydb;User Id=rayuser;Password=raypass123"
      }
    },

    "Docker_Mac_MySql": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Migrate-Up --product RM_Tests_Mac_MySql -env {ENV:DOTNET_ENVIRONMENT} --run-mode Migrate",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Docker",
        "ConnectionString_Backend1": "Server=localhost;Port=3307;Database=raydb;User Id=rayuser;Password=raypass123",
        "ConnectionString_Backend2": "Server=localhost;Port=3307;Database=raydb2;User Id=rayuser;Password=raypass123",
        "ConnectionString_Frontend": "Server=localhost;Port=3307;Database=raydb;User Id=rayuser;Password=raypass123"
      }
    },

    "Docker_Mac_PostgreSQL": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Migrate-Up --product RM_Tests_Mac_PostgreSQL -env {ENV:DOTNET_ENVIRONMENT} --run-mode Migrate",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Docker",
        "ConnectionString_Backend1": "Host=localhost;Port=5432;Database=raydb;Username=postgres;Password=postgres123",
        "ConnectionString_Backend2": "Host=localhost;Port=5432;Database=raydb2;Username=postgres;Password=postgres123",
        "ConnectionString_Frontend": "Host=localhost;Port=5432;Database=raydb;Username=postgres;Password=postgres123"
      }
    },

    "Production": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "Migrate-Up --product RayMigratorTests --environment Production --run-mode Migrate",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Production",
        "ConnectionString_Backend1": "Server=production-server;Initial Catalog=Backend_1;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=ProductionPassword!",
        "ConnectionString_Backend2": "Server=production-server;Initial Catalog=Backend_2;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=ProductionPassword!"
      }
    }
  }
}
```

Note that Docker command-line arguments use `{ENV:DOTNET_ENVIRONMENT}` to reference the environment variable defined in the same profile. The Staging and Production profiles hardcode the environment name directly. To reveal sensitive data in logs (e.g., connection strings), add `--reveal-sensitive-data true` to the profile's `commandLineArgs`.

## Environment Variables in Profiles

These variables are set in the launch profile and referenced in `appsettings.json` via `{ENV:VariableName}`:

| Variable | Purpose | Example |
|----------|---------|---------|
| `DOTNET_ENVIRONMENT` | Environment name | Docker, Staging, Production |
| `ConnectionString_Backend1` | First target database | `Server=localhost;Initial Catalog=Backend_1;...` |
| `ConnectionString_Backend2` | Second target database | `Server=localhost;Initial Catalog=Backend_2;...` |
| `ConnectionString_Frontend` | Frontend target database (MariaDB, MySQL, PostgreSQL profiles only) | `Server=localhost;Port=3306;Database=raydb;...` |

The repository connection string and migration files root directory are typically configured directly in `appsettings.{Environment}.json` rather than via environment variables in launch profiles.

## appsettings.json Reference

Connection strings from launch profile environment variables are referenced in `appsettings.json`:

```json
{
  "RayMigrator": {
    "Products": [{
      "TargetGroups": [{
        "Targets": [{
          "Alias": "Backend1",
          "ConnectionString": "{ENV:ConnectionString_Backend1}"
        },
        {
          "Alias": "Backend2",
          "ConnectionString": "{ENV:ConnectionString_Backend2}"
        }]
      }]
    }]
  }
}
```

## IDE Support

### Visual Studio

1. Open project properties
2. Go to Debug tab
3. Select launch profile from dropdown
4. Press F5 to debug

### JetBrains Rider

1. Open Run/Debug Configurations
2. launchSettings profiles appear automatically
3. Select profile and run

### VS Code

1. Add `launch.json` configuration
2. Reference launchSettings profile or copy settings

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Docker MigrateUp SqlServer",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/Raycoon.RayMigrator.Console/bin/Debug/net10.0/raymigrator.dll",
      "args": ["Migrate-Up", "--product", "RM_Tests_Mac_SqlServer", "-env", "Docker", "--run-mode", "Migrate"],
      "env": {
        "DOTNET_ENVIRONMENT": "Docker",
        "ConnectionString_Backend1": "Server=localhost;Initial Catalog=Backend_1;TrustServerCertificate=true;User Id=sa;Password=P@ssw0rd!",
        "ConnectionString_Backend2": "Server=localhost;Initial Catalog=Backend_2;TrustServerCertificate=true;User Id=sa;Password=P@ssw0rd!"
      }
    }
  ]
}
```

## Profile Naming Convention

Pattern: `{Environment}_{Platform}_{DatabaseType}`

Examples:
- `Docker_Mac_SqlServer`
- `Docker_Win_MariaDb`
- `Docker_Mac_PostgreSQL`
- `Docker_Win_MySql`

For run-mode variants of an existing profile, a `_{RunMode}` suffix is appended:
- `Docker_Mac_SqlServer_Validate`
- `Docker_Mac_SqlServer_Simulate`

For success-only test scenarios, the pattern is `{Environment}_{Platform}_Success_{DatabaseType}`:
- `Docker_Mac_Success_SqlServer`
- `Docker_Win_Success_MariaDb`

For non-Docker environments, a simpler name is used (e.g. `Staging`, `Production`).

## Sensitive Data

**Warning**: `launchSettings.json` may contain passwords.

Best practices:
1. Add to `.gitignore` if contains real credentials
2. Use `launchSettings.json.template` for documentation
3. Use Windows Credential Manager or environment-specific secrets

## Troubleshooting

### Variables Not Resolved

**Symptom**: `{ENV:VAR_NAME}` appears in logs

**Cause**: Variable not defined in profile

**Fix**: Add missing variable to `environmentVariables` section

### Wrong Configuration Loaded

**Symptom**: Different settings than expected

**Cause**: Wrong `DOTNET_ENVIRONMENT` value

**Fix**: Verify environment matches appsettings file name

### Profile Not Found

**Symptom**: IDE doesn't show profile

**Cause**: Invalid JSON syntax

**Fix**: Validate JSON structure

## Related Documentation

- [Command Structure](command-structure.md) - CLI arguments
- [Configuration System](../02-core-concepts/configuration-system.md) - Environment variables
- [Environment Variables](../06-configuration-reference/environment-variables.md)
