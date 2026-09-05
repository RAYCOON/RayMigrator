# RayMigrator Examples

This directory contains two complete example products that demonstrate different RayMigrator migration layouts, along with the Docker infrastructure needed to run them.

## Contents

| Directory | Description |
|-----------|-------------|
| `Docker/` | Docker Compose setup with SQL Server and PostgreSQL containers |
| `MySimpleApplication/` | Flat layout: single TargetGroup, single database (SqlServer) |
| `MyComplexApplication/` | Nested layout: two TargetGroups with different database engines (PostgreSQL + SqlServer) |

## Prerequisites

- Docker and Docker Compose installed
- RayMigrator built or published

## Starting Docker Containers

### Using Platform Scripts

**Windows (PowerShell):**
```powershell
# Start all containers
.\Examples\Docker\RunDocker.default.all.ps1

# Start only SQL Server
.\Examples\Docker\RunDocker.default.sqlserver.ps1

# Start only PostgreSQL
.\Examples\Docker\RunDocker.default.postgresql.ps1
```

**macOS / Linux (Shell):**
```bash
# Start all containers
./Examples/Docker/run-docker-all.sh

# Start only SQL Server
./Examples/Docker/run-docker-sqlserver.sh

# Start only PostgreSQL
./Examples/Docker/run-docker-postgresql.sh
```

### Using Docker Compose Directly

From the `Examples/Docker/` directory:

```bash
# Start all containers (SQL Server + PostgreSQL)
docker compose --env-file default.env --profile all up -d

# Start only SQL Server
docker compose --env-file default.env --profile sqlserver up -d

# Start only PostgreSQL
docker compose --env-file default.env --profile postgresql up -d
```

The SQL Server container initializes three databases on startup:
- `RayMigratorRepository` - shared migration tracking repository
- `SimpleApplicationDB` - target database for MySimpleApplication
- `BackendDB` - target database for MyComplexApplication's BackendTargetGroup

The PostgreSQL container initializes:
- `FrontendDB` - target database for MyComplexApplication's FrontendTargetGroup

### Ports

| Service | Port |
|---------|------|
| SQL Server | `localhost:14330` |
| PostgreSQL | `localhost:54320` |

## Running Migrations

Set the required environment variables before running RayMigrator:

```bash
# SQL Server SA password (matches Docker container)
export SA_PASSWORD="P@ssw0rd!"

# PostgreSQL password for rayuser (matches Docker container)
export POSTGRES_EXAMPLE_PASSWORD="raypass123"
```

### MySimpleApplication

This example uses a flat migration directory layout (no TargetGroup subdirectories). All migration files are directly inside each release folder and target a single SQL Server database.

Copy the appsettings file to the RayMigrator working directory, then run:

```bash
cp ./Examples/MySimpleApplication/appsettings.MySimpleApplication.Docker.json ./appsettings.Docker.json

raymigrator Migrate-Up \
    --product MySimpleApplication \
    --environment Docker \
    --run-mode Migrate
```

### MyComplexApplication

This example uses a nested migration directory layout with two TargetGroups, each targeting a different database engine:

- **FrontendTargetGroup** (PostgreSQL) - user profiles, sessions, preferences, notifications, audit logs, dashboard widgets
- **BackendTargetGroup** (SQL Server) - financial accounts, transactions, reporting views, indexes

Copy the appsettings file to the RayMigrator working directory, then run:

```bash
cp ./Examples/MyComplexApplication/appsettings.MyComplexApplication.Docker.json ./appsettings.Docker.json

raymigrator Migrate-Up \
    --product MyComplexApplication \
    --environment Docker \
    --run-mode Migrate
```

## Layout Comparison

### Flat Layout (MySimpleApplication)

When a product has only one TargetGroup, migration files live directly inside each release folder:

```
Migrations/MySimpleApplication/
    Release 1.0/
        01_CreateCustomerTable.sql
        01_CreateCustomerTable.rollback.sql
        02_CreateOrderTable.sql
        02_CreateOrderTable.rollback.sql
    Release 1.1/
        ...
```

### Nested Layout (MyComplexApplication)

When a product has multiple TargetGroups, each TargetGroup gets its own subdirectory inside each release:

```
Migrations/MyComplexApplication/
    Release 1.0/
        FrontendTargetGroup/
            migsettings.txt
            01_CreateUserProfileTable.sql
            02_CreateSessionTable.sql
        BackendTargetGroup/
            01_CreateAccountTable.sql
            01_CreateAccountTable.rollback.sql
            02_CreateTransactionTable.sql
            02_CreateTransactionTable.rollback.sql
    Release 1.1/
        ...
```

## migsettings.txt Override

The `FrontendTargetGroup` directories each contain a `migsettings.txt` file that overrides the product-level `RequireRollbackFile = true` setting:

```toml
[RayMigrator]
RequireRollbackFile = false
```

This allows the FrontendTargetGroup to omit rollback files (common for PostgreSQL schemas where rollback is handled differently), while the BackendTargetGroup still requires rollback files for every migration as configured at the product level.

## Stopping Docker Containers

```bash
docker-compose --env-file default.env --profile all down
```

To also remove all data volumes:

```bash
docker-compose --env-file default.env --profile all down -v
```
