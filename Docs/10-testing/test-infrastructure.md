# Test Infrastructure

Setup and configuration for the RayMigrator test databases.

## Overview

The engine tests run against four database engines in Docker: SQL Server, PostgreSQL, MariaDB and MySQL. SQLite needs no container. Everything lives under [`Testing/Docker/`](../../Testing/Docker/); this page describes that folder and links to the files instead of copying them, so the files are always the source of truth.

## What Is in Testing/Docker

| Path | Purpose |
|------|---------|
| [`docker-compose.yml`](../../Testing/Docker/docker-compose.yml) | The four services, one profile per engine plus `all`, the shared bridge network `ray_network`, the healthchecks |
| [`default.env`](../../Testing/Docker/default.env) | The variables the compose file reads: image source, credentials and database names per engine, `COMPOSE_PROJECT_NAME` |
| `RunDocker.default.<profile>.ps1` | Start scripts, one per profile (`all`, `sqlserver`, `postgresql`, `mariadb`, `mysql`); they call `RunDockerXecute.ps1`, which checks Docker and the env file, rebuilds the images and starts the profile |
| `TeardownDocker.ps1`, `teardown.sh` | Stop and remove all containers together with their volumes and the network |
| [`SqlServer/`](../../Testing/Docker/SqlServer/) | Dockerfile, `entrypoint.sh` and the `sql-scripts/` that create the logins and databases |
| [`PostgreSQL/`](../../Testing/Docker/PostgreSQL/) | Dockerfile and `init/01-init.sql`; `README-postgresql.md` lists the users |
| [`MariaDB/`](../../Testing/Docker/MariaDB/) | Dockerfile, `config/my.cnf`, `init/` scripts; `README-MariaDB.md` |
| [`MySQL/`](../../Testing/Docker/MySQL/) | Dockerfile, `config/my.cnf`, `init/` scripts; `README-MySQL.md` |

The migration files the engine tests execute are under [`Testing/MigrationFiles/`](../../Testing/MigrationFiles/), one product per engine (`Tests_<Engine>`, `Tests_Success_<Engine>`) plus `Tests_SqlCmdDemo` for the CLI tool tests.

## The Env File

Every `${VAR}` in the compose file comes from an env file, and every variable is mandatory: the compose file uses `${VAR:?VAR is missing in the env file}`, so a missing variable stops `docker compose` before anything starts and the message names the variable. There are no defaults hidden in the compose file.

- `default.env` is in the repository. It holds disposable credentials for local testing only.
- Any other `<name>.env` in the folder is ignored by git (`.gitignore`), so real credentials stay on your machine. The start scripts take the env file name from their own file name: `RunDocker.<name>.<profile>.ps1` uses `<name>.env`. Copy a start script and `default.env` under a new name to use your own credentials.
- Every manual `docker compose` command needs `--env-file <name>.env`. The start scripts pass it for you.
- The start scripts also check the variables of their profile before building, so a copied env file that lacks a variable fails in the script rather than in compose.

## The Four Engines

| Container | Image | Host port | Profile | Ready when | Created by the init scripts |
|-----------|-------|-----------|---------|------------|-----------------------------|
| `rm_db_sqlserver` | `mssql/server:2022-latest` from `${SOURCE_MICROSOFT}` | `1433` | `sqlserver` | `entrypoint.sh` has run all `sql-scripts/*.sql` and created `/tmp/app-initialized` (Dockerfile `HEALTHCHECK`, start period 10 minutes) | login `rmlogin` (sysadmin, password `RM_LOGIN_PASSWORD`), databases `Backend_1`, `Backend_2`, `Frontend`, user `rmuser` in each; `sa` password is `MSSQL_SA_PASSWORD` |
| `rm_db_postgresql` | `postgres:latest` | `5432` | `postgresql` | `pg_isready -U postgres` | user `rayuser`, databases `raydb`, `raydb2`, `raydb_frontend`, schema `ray_schema` with the read-only user `rayreader` and the application user `rayapp` |
| `rm_db_mariadb` | `mariadb:11.6` | `3306` | `mariadb` | `healthcheck.sh --connect --innodb_initialized` | user `rayuser`, databases `raydb`, `raydb2`, `raydb_frontend`, users `readonly` and `backup`; `root` reachable from outside the container |
| `rm_db_mysql` | `mysql:8.4` | `3307` (container `3306`) | `mysql` | `mysqladmin ping` as `root` | same layout as MariaDB with the MySQL 8 collation `utf8mb4_0900_ai_ci` |

Notes:

- MySQL is published on host port `3307` so that it can run next to MariaDB.
- MariaDB and MySQL share the same `my.cnf` layout (`utf8mb4`, strict SQL mode, slow query log, `skip-name-resolve`); the two files differ only in the collation and in the query cache settings that MySQL 8 no longer has.
- SQL Server reports healthy only after its init scripts have completed. Because `localhost` resolves to IPv6 inside the container and SQL Server listens on IPv4 only, `entrypoint.sh` addresses `127.0.0.1`; do not change that back.
- The exact credentials are in `default.env` and in the init scripts linked above.

## Running the Containers

Start all four engines (PowerShell, any directory):

```powershell
./Testing/Docker/RunDocker.default.all.ps1
```

Start one engine by choosing the matching script, for example `RunDocker.default.sqlserver.ps1`. Without the scripts, from `Testing/Docker/`:

```bash
docker compose --env-file default.env --profile all up -d --build
```

Wait until every `rm_db_*` container reports `(healthy)`; SQL Server takes the longest, about a minute after a fresh build:

```bash
docker ps --filter "name=rm_db_" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker compose --env-file default.env --profile all logs -f rm_db_sqlserver
```

Stop everything, and remove the volumes for a fresh start:

```bash
docker compose --env-file default.env --profile all down
docker compose --env-file default.env --profile all down -v
```

`TeardownDocker.ps1` and `teardown.sh` run the second command.

## Test Configuration

### Shared Test Helpers

The `Raycoon.RayMigrator.Testing` project (multi-target `net10.0;net9.0;net8.0`, published as a reusable NuGet package) centralizes infrastructure used by both the unit and engine test suites:

- `DatabaseCleanupHelper.cs` — truncates or drops repository + user tables between test runs (keeps engines in sync with any schema change; must be updated whenever the repository schema evolves). After DAL-018, `GetMySqlFamilyCleanupSql` drops both new snake_case tables and the legacy backtick-quoted PascalCase names (safety net for pre-DAL-018 Docker volumes).
- `RepositoryQueryHelper.cs` — query helpers against the repository tables (`ProductExists`, `GetProductId`, `EnvironmentExists`, `CountMigrations`, `CountMigrationRuns`, `InsertRunningMigrationRun`, etc.). `ProductExists` and `GetProductId` perform case-insensitive lookups via the `NameLower` column (computed with `ToLowerInvariant()` in C#). Schema/table names are formatted per engine via `QuoteColumn` / `GetQualifiedTableName`: for PostgreSQL and MariaDB/MySQL the helper converts the input to snake_case via the internal `ToSnakeCase` helper (which honours the `RayMigrator`-single-token exception); for PostgreSQL identifiers are unquoted; for MariaDB/MySQL table names are backtick-quoted snake_case (e.g., `` `migration_record` ``) and column names are also backtick-quoted snake_case; for SQL Server identifiers are bracket-quoted PascalCase.
- `DockerHealthCheck.cs` — probes `docker ps` for container availability.

### Engine Tests

Engine tests (`Raycoon.RayMigrator.Tests.Engine`) embed their database connection strings directly in the fixture classes (`Fixtures/PostgreSqlFixture.cs`, `Fixtures/SqlServerFixture.cs`, `Fixtures/MariaDbFixture.cs`, `Fixtures/MySqlFixture.cs`, `Fixtures/SqliteFixture.cs`) rather than using appsettings files. No external configuration files are needed — the fixtures read connection strings from code and report `IsDatabaseAvailable` based on a live connection probe.

> **Note**: Console project test configs (`appsettings.RM_Tests_Mac_*.Docker.json`, `appsettings.RM_Tests_Win_*.Docker.json`) are used for manual IDE debugging via launch profiles and are documented in [Launch Profiles](../05-console-layer/launch-profiles.md). They point at the same containers; their product `MigrationErrorAction` is `Rollback` (not the default `Terminate`) to exercise rollback behavior, and the Mac variants use absolute paths for `MigrationFilesRootDirectory`.

## Connection Strings

The fixtures use these connection strings; `{database}` is one of the databases listed in the engine table above.

### SQL Server

```
Server=127.0.0.1;Initial Catalog={database};TrustServerCertificate=true;User Id=sa;Password=P@ssw0rd!;
```

### PostgreSQL

```
Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres123;
```

### MariaDB

```
Server=127.0.0.1;Port=3306;Database={database};User Id=rayuser;Password=raypass123;
```

### MySQL

```
Server=127.0.0.1;Port=3307;Database={database};User Id=rayuser;Password=raypass123;
```

### SQLite

```
Data Source=/path/to/database.sqlite
```

> **Note**: SQLite is file-based and does not require Docker. Engine tests create temporary database files under `Path.GetTempPath()/RayMigrator_SqliteTests/raytest_<guid>.sqlite` (see `SqliteFixture.cs`). The fixture deletes the files in `DisposeAsync`.

## Troubleshooting

### Compose Stops Before Starting Anything

The message names a variable, for example `MARIADB_ROOT_PASSWORD is missing in the env file`: the env file passed with `--env-file` lacks that variable, or `--env-file` was left out. Compare your env file with `default.env`.

### Container Won't Start

```bash
# Check logs
docker compose --env-file default.env --profile all logs rm_db_sqlserver

# Check if port is in use
netstat -an | grep 1433
```

A port held by an orphan container of an older version of this compose project (compose reports it as an orphan) is the usual cause on port `1433`; stop that container.

### Connection Refused

```bash
# Verify container is running
docker ps

# Check container health
docker inspect --format='{{.State.Health.Status}}' rm_db_sqlserver
```

### Permission Issues

```bash
# SQL Server log permissions
docker exec -it rm_db_sqlserver /bin/bash
ls -la /var/opt/mssql/
```

### Reset to Clean State

```bash
# Stop and remove everything
docker compose --env-file default.env --profile all down -v

# Rebuild and start
docker compose --env-file default.env build --no-cache
docker compose --env-file default.env --profile all up -d
```

## Related Documentation

- [Unit Tests](unit-tests.md)
- [Engine Tests](engine-tests.md)
- [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md)
