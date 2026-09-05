# Test Infrastructure

Setup and configuration for RayMigrator testing environments.

## Overview

RayMigrator uses Docker containers to provide consistent test databases across different database systems.

## Directory Structure

```
Testing/
├── Docker/
│   ├── docker-compose.yml               # Main compose file
│   ├── default.env                      # Environment variables
│   ├── RunDocker.default.all.ps1        # Start all containers
│   ├── RunDocker.default.sqlserver.ps1  # Start SQL Server only
│   ├── RunDocker.default.postgresql.ps1 # Start PostgreSQL only
│   ├── RunDocker.default.mariadb.ps1    # Start MariaDB only
│   ├── RunDocker.default.mysql.ps1      # Start MySQL only
│   ├── RunDockerXecute.ps1              # Main executor script
│   ├── TeardownDocker.ps1              # Stop and remove containers
│   ├── teardown.sh                     # Bash teardown script
│   ├── SqlServer/
│   │   ├── Dockerfile
│   │   ├── entrypoint.sh
│   │   └── sql-scripts/
│   │       ├── 10_create_logins.sql
│   │       ├── 21_create_db_Backend_1.sql
│   │       ├── 22_create_db_Backend_2.sql
│   │       ├── 23_create_db_Frontend.sql
│   │       └── 90_create_user.sql
│   ├── PostgreSQL/
│   │   ├── Dockerfile
│   │   ├── README-postgresql.md
│   │   └── init/
│   │       └── 01-init.sql
│   ├── MariaDB/
│   │   ├── Dockerfile
│   │   ├── README-MariaDB.md
│   │   ├── config/
│   │   │   └── my.cnf
│   │   └── init/
│   │       ├── 01-init-database.sql
│   │       └── 02-setup-permissions.sh
│   └── MySQL/
│       ├── Dockerfile
│       ├── README-MySQL.md
│       ├── config/
│       │   └── my.cnf
│       └── init/
│           ├── 01-init-database.sql
│           └── 02-setup-permissions.sh
├── MigrationFiles/
│   ├── Tests_SqlServer/                        # SQL Server test migrations
│   ├── Tests_PostgreSQL/                       # PostgreSQL test migrations
│   ├── Tests_MariaDb/                          # MariaDB test migrations
│   ├── Tests_MySql/                            # MySQL test migrations
│   ├── Tests_Success_SqlServer/               # Success-only test product (no R1.3 error)
│   ├── Tests_Success_PostgreSQL/
│   ├── Tests_Success_MariaDb/
│   ├── Tests_Success_MySql/
│   └── Tests_SqlCmdDemo/                      # CLI tool execution testing via an external tool
└── TestingMsSqlServer2019.txp    # Database project file
```

## Docker Setup

### docker-compose.yml

The compose file uses **profiles** to selectively start database services:

```yaml
services:
  rm_db_sqlserver:
    container_name: rm_db_sqlserver
    image: rm_img_db_sqlserver
    hostname: rm_db_sqlserver
    build:
      context: ./SqlServer
      dockerfile: Dockerfile
      args:
        SOURCE_MICROSOFT: ${SOURCE_MICROSOFT}
    restart: always
    ports:
      - 1433:1433
    expose:
      - 1433
    networks:
      - ray_network
    profiles:
      - all
      - sqlserver

  rm_db_mariadb:
    container_name: rm_db_mariadb
    image: rm_img_db_mariadb
    hostname: rm_db_mariadb
    build:
      context: ./MariaDB
      dockerfile: Dockerfile
    restart: always
    environment:
      MYSQL_ROOT_PASSWORD: ${MARIADB_ROOT_PASSWORD:-root123}
      MYSQL_DATABASE: ${MARIADB_DATABASE:-raydb}
      MYSQL_USER: ${MARIADB_USER:-rayuser}
      MYSQL_PASSWORD: ${MARIADB_PASSWORD:-raypass123}
    ports:
      - 3306:3306
    expose:
      - 3306
    volumes:
      - ./MariaDB/init:/docker-entrypoint-initdb.d
      - ./MariaDB/config:/etc/mysql/conf.d
    networks:
      - ray_network
    profiles:
      - all
      - mariadb
    healthcheck:
      test: ["CMD", "mariadb-admin", "ping", "-h", "localhost", "-u", "root", "-p${MARIADB_ROOT_PASSWORD:-root123}"]
      interval: 10s
      timeout: 10s
      retries: 5

  rm_db_mysql:
    container_name: rm_db_mysql
    image: rm_img_db_mysql
    hostname: rm_db_mysql
    build:
      context: ./MySQL
      dockerfile: Dockerfile
    restart: always
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD:-root123}
      MYSQL_DATABASE: ${MYSQL_DATABASE:-raydb}
      MYSQL_USER: ${MYSQL_USER:-rayuser}
      MYSQL_PASSWORD: ${MYSQL_PASSWORD:-raypass123}
    ports:
      - 3307:3306
    expose:
      - 3306
    volumes:
      - ./MySQL/init:/docker-entrypoint-initdb.d
      - ./MySQL/config:/etc/mysql/conf.d
    networks:
      - ray_network
    profiles:
      - all
      - mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${MYSQL_ROOT_PASSWORD:-root123}"]
      timeout: 20s
      retries: 5

  rm_db_postgresql:
    container_name: rm_db_postgresql
    image: rm_img_db_postgresql
    hostname: rm_db_postgresql
    build:
      context: ./PostgreSQL
      dockerfile: Dockerfile
    restart: always
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    ports:
      - 5432:5432
    expose:
      - 5432
    #volumes:
    #  - ./PostgreSQL/init:/docker-entrypoint-initdb.d
    networks:
      - ray_network
    profiles:
      - all
      - postgresql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

networks:
  ray_network:
    name: ray_network
    driver: bridge
```

> **Note**: Use `--profile all` to start all services, or `--profile sqlserver`, `--profile mariadb`, `--profile mysql`, `--profile postgresql` for a single database. MySQL uses external port 3307 to avoid conflict with MariaDB on 3306.

### Environment Variables (default.env)

```env
SOURCE_DOCKERHUB=docker.io
SOURCE_MICROSOFT=mcr.microsoft.com

# PostgreSQL
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres123
POSTGRES_DB=raydb

# MariaDB
MARIADB_ROOT_PASSWORD=root123
MARIADB_DATABASE=raydb
MARIADB_USER=rayuser
MARIADB_PASSWORD=raypass123

# MySQL
MYSQL_ROOT_PASSWORD=root123
MYSQL_DATABASE=raydb
MYSQL_USER=rayuser
MYSQL_PASSWORD=raypass123

COMPOSE_PROJECT_NAME=ray_project
```

> **Note**: SQL Server credentials are set directly in the Dockerfile (`MSSQL_SA_PASSWORD=P@ssw0rd!`), not in `default.env`.

## SQL Server Setup

### Dockerfile

```dockerfile
ARG SOURCE_MICROSOFT=mcr.microsoft.com
FROM ${SOURCE_MICROSOFT}/mssql/server:2022-latest

ENV ACCEPT_EULA=Y
ENV MSSQL_SA_PASSWORD="P@ssw0rd!"
ENV MSSQL_PID="Developer"
ENV MSSQL_AGENT_ENABLED=True
ENV TZ=Europe/Berlin
ENV MSSQL_COLLATION=Latin1_General_CI_AS

COPY ./sql-scripts /sql-scripts
COPY ./*.sh /

HEALTHCHECK --interval=3s --start-period=600s CMD test -e /tmp/app-initialized

ENTRYPOINT [ "/bin/bash", "entrypoint.sh" ]
CMD [ "/opt/mssql/bin/sqlservr" ]
```

### Initialization Scripts

The `entrypoint.sh` script waits for SQL Server to be ready (100s timeout), then executes initialization scripts in order:

```sql
-- 10_create_logins.sql
IF EXISTS (SELECT TOP(1) 1 FROM [master].[sys].[server_principals] WHERE [name] = 'rmlogin')
    DROP LOGIN [rmlogin];
GO
CREATE LOGIN [rmlogin] WITH PASSWORD=N'P@ssw0rd!', DEFAULT_DATABASE=[master],
    DEFAULT_LANGUAGE=[English], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF;
GO
ALTER SERVER ROLE [sysadmin] ADD MEMBER [rmlogin]
GO

-- 21_create_db_Backend_1.sql
CREATE DATABASE [Backend_1];
-- (includes multiple filegroups: PRIMARY, DATA, INDEX + transaction log)
-- (FULL recovery, compatibility level 160, Query Store enabled)
GO

-- 22_create_db_Backend_2.sql / 23_create_db_Frontend.sql
-- (similar structure for Backend_2 and Frontend databases)

-- 90_create_user.sql
-- Creates database user [rmuser] with db_owner role in each database
```

## PostgreSQL Setup

### Dockerfile

```dockerfile
FROM postgres:latest

ENV LANG=en_US.utf8

COPY ./init/*.sql /docker-entrypoint-initdb.d/

EXPOSE 5432

CMD ["postgres"]
```

### Initialization Script

```sql
-- 01-init.sql
-- Creates user 'rayuser' with password 'raypass123'
-- Creates databases: raydb (owned by rayuser), raydb2 (owned by postgres),
--   raydb_frontend (owned by postgres, grants to rayuser)
-- Creates schema: ray_schema (with system_info, audit_log tables and triggers)
-- Creates additional users: rayreader (read-only), rayapp (CRUD)

-- Uses DO $$ blocks for idempotent user creation
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayuser') THEN
        CREATE USER rayuser WITH PASSWORD 'raypass123';
    END IF;
END
$$;

-- Uses \gexec pattern for idempotent database creation
SELECT 'CREATE DATABASE raydb OWNER rayuser'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb')\gexec

GRANT ALL PRIVILEGES ON DATABASE raydb TO rayuser;
GRANT CREATE ON DATABASE raydb TO rayuser;

SELECT 'CREATE DATABASE raydb2 OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb2')\gexec

SELECT 'CREATE DATABASE raydb_frontend OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb_frontend')\gexec

GRANT ALL PRIVILEGES ON DATABASE raydb_frontend TO rayuser;
GRANT CREATE ON DATABASE raydb_frontend TO rayuser;

\c raydb rayuser;
CREATE SCHEMA IF NOT EXISTS ray_schema;
-- (includes system_info and audit_log tables, update_updated_at trigger, initial data)

\c raydb postgres;
-- Creates rayreader (SELECT-only) and rayapp (CRUD) users with scoped grants
-- (uses DO $$ blocks for idempotent user creation)
```

## MariaDB Setup

### Dockerfile

```dockerfile
FROM mariadb:11.6

ENV MYSQL_ROOT_PASSWORD=root123
ENV MYSQL_DATABASE=raydb
ENV MYSQL_USER=rayuser
ENV MYSQL_PASSWORD=raypass123

COPY config/my.cnf /etc/mysql/conf.d/
COPY init/ /docker-entrypoint-initdb.d/

RUN chmod 644 /etc/mysql/conf.d/my.cnf && \
    chmod +x /docker-entrypoint-initdb.d/*.sh

EXPOSE 3306

ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["mariadbd"]
```

### Configuration

```ini
# my.cnf
[mysqld]
bind-address = 0.0.0.0
port = 3306
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
innodb_buffer_pool_size = 256M
innodb_log_file_size = 64M
innodb_flush_log_at_trx_commit = 1
innodb_lock_wait_timeout = 50
max_connections = 100
table_open_cache = 64
max_allowed_packet = 16M
thread_cache_size = 8
query_cache_size = 32M
query_cache_type = 1
general_log = 0
slow_query_log = 1
slow_query_log_file = /var/log/mysql/slow.log
long_query_time = 2
skip-name-resolve
sql_mode = STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO

[mysql]
default-character-set = utf8mb4

[client]
default-character-set = utf8mb4
```

### Initialization

```sql
-- 01-init-database.sql
CREATE DATABASE IF NOT EXISTS raydb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'rayuser'@'%' IDENTIFIED BY 'raypass123';
CREATE DATABASE IF NOT EXISTS raydb2 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS raydb_frontend CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
GRANT ALL PRIVILEGES ON raydb.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb2.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb_frontend.* TO 'rayuser'@'%';
GRANT CREATE USER ON *.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' IDENTIFIED BY 'root123' WITH GRANT OPTION;
FLUSH PRIVILEGES;

USE raydb;

-- Test connection table
CREATE TABLE IF NOT EXISTS test_connection (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO test_connection (name) VALUES ('MariaDB Connection Test');
```

> **Note**: The `GRANT CREATE USER ON *.* TO 'rayuser'@'%'` is required because migration files may create database users. Without this grant, migrations that issue `CREATE USER` statements will fail with a permission error.

## MySQL Setup

### Dockerfile

```dockerfile
FROM mysql:8.4

ENV MYSQL_ROOT_PASSWORD=root123
ENV MYSQL_DATABASE=raydb
ENV MYSQL_USER=rayuser
ENV MYSQL_PASSWORD=raypass123

COPY config/my.cnf /etc/mysql/conf.d/
COPY init/ /docker-entrypoint-initdb.d/

RUN chmod 644 /etc/mysql/conf.d/my.cnf && \
    chmod +x /docker-entrypoint-initdb.d/*.sh

EXPOSE 3306

ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["mysqld"]
```

### Configuration

```ini
# my.cnf
[mysqld]
bind-address = 0.0.0.0
port = 3306
character-set-server = utf8mb4
collation-server = utf8mb4_0900_ai_ci
innodb_buffer_pool_size = 256M
innodb_log_file_size = 64M
innodb_flush_log_at_trx_commit = 1
innodb_lock_wait_timeout = 50
max_connections = 100
table_open_cache = 64
max_allowed_packet = 16M
thread_cache_size = 8
general_log = 0
slow_query_log = 1
slow_query_log_file = /var/log/mysql/slow.log
long_query_time = 2
skip-name-resolve
sql_mode = STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO

[mysql]
default-character-set = utf8mb4

[client]
default-character-set = utf8mb4
```

> **Note**: Unlike the MariaDB my.cnf, the MySQL my.cnf does **not** include `query_cache_size` or `query_cache_type` settings because the query cache was removed in MySQL 8.0+. The collation is `utf8mb4_0900_ai_ci` (vs. `utf8mb4_unicode_ci` for MariaDB).

### Initialization

```sql
-- 01-init-database.sql
CREATE DATABASE IF NOT EXISTS raydb CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'rayuser'@'%' IDENTIFIED BY 'raypass123';
CREATE DATABASE IF NOT EXISTS raydb2 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS raydb_frontend CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
GRANT ALL PRIVILEGES ON raydb.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb2.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb_frontend.* TO 'rayuser'@'%';
GRANT CREATE USER ON *.* TO 'rayuser'@'%';
CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY 'root123';
GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;
FLUSH PRIVILEGES;

USE raydb;

-- Test connection table
CREATE TABLE IF NOT EXISTS test_connection (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO test_connection (name) VALUES ('MySQL Connection Test');
```

> **Note**: MySQL 8.0+ requires separate `CREATE USER IF NOT EXISTS` + `GRANT` statements. The `GRANT...IDENTIFIED BY` syntax used by MariaDB is not supported in MySQL 8.0+.

## Running Tests

### Start All Containers

```powershell
# PowerShell
./Testing/Docker/RunDocker.default.all.ps1
```

```bash
# Bash
cd Testing/Docker
docker-compose --env-file default.env --profile all up -d
```

### Start Specific Database

```powershell
# SQL Server only
./Testing/Docker/RunDocker.default.sqlserver.ps1

# PostgreSQL only
./Testing/Docker/RunDocker.default.postgresql.ps1

# MariaDB only
./Testing/Docker/RunDocker.default.mariadb.ps1

# MySQL only
./Testing/Docker/RunDocker.default.mysql.ps1
```

### Check Container Status

```bash
docker-compose --env-file default.env --profile all ps
docker-compose --env-file default.env --profile all logs -f rm_db_sqlserver
```

### Stop Containers

```bash
docker-compose --env-file default.env --profile all down

# Remove volumes (fresh start)
docker-compose --env-file default.env --profile all down -v
```

## Test Configuration

### Shared Test Helpers

The `Raycoon.RayMigrator.Testing` project (multi-target `net10.0;net9.0;net8.0`, published as a reusable NuGet package) centralizes infrastructure used by both the unit and engine test suites:

- `DatabaseCleanupHelper.cs` — truncates or drops repository + user tables between test runs (keeps engines in sync with any schema change; must be updated whenever the repository schema evolves). After DAL-018, `GetMySqlFamilyCleanupSql` drops both new snake_case tables and the legacy backtick-quoted PascalCase names (safety net for pre-DAL-018 Docker volumes).
- `RepositoryQueryHelper.cs` — query helpers against the repository tables (`ProductExists`, `GetProductId`, `EnvironmentExists`, `CountMigrations`, `CountMigrationRuns`, `InsertRunningMigrationRun`, etc.). `ProductExists` and `GetProductId` perform case-insensitive lookups via the `NameLower` column (computed with `ToLowerInvariant()` in C#). Schema/table names are formatted per engine via `QuoteColumn` / `GetQualifiedTableName`: for PostgreSQL and MariaDB/MySQL the helper converts the input to snake_case via the internal `ToSnakeCase` helper (which honours the `RayMigrator`-single-token exception); for PostgreSQL identifiers are unquoted; for MariaDB/MySQL table names are backtick-quoted snake_case (e.g., `` `migration_record` ``) and column names are also backtick-quoted snake_case; for SQL Server identifiers are bracket-quoted PascalCase.
- `DockerHealthCheck.cs` — probes `docker ps` for container availability.

### Engine Tests

Engine tests (`Raycoon.RayMigrator.Tests.Engine`) embed their database connection strings directly in the fixture classes (`Fixtures/PostgreSqlFixture.cs`, `Fixtures/SqlServerFixture.cs`, `Fixtures/MariaDbFixture.cs`, `Fixtures/MySqlFixture.cs`, `Fixtures/SqliteFixture.cs`) rather than using appsettings files. No external configuration files are needed — the fixtures read connection strings from code and report `IsDatabaseAvailable` based on a live connection probe.

> **Note**: Console project test configs (`appsettings.RM_Tests_Mac_*.Docker.json`, `appsettings.RM_Tests_Win_*.Docker.json`) are used for manual IDE debugging via launch profiles and are documented in [Launch Profiles](../05-console-layer/launch-profiles.md).

### Example: SQL Server (Mac)

`appsettings.RM_Tests_Mac_SqlServer.Docker.json` (excerpt):

```json
{
  "RayMigrator": {
    "Repository": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "Server=localhost;Initial Catalog=Backend_1;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!",
      "SchemaName": "ray",
      "TableBaseName": ""
    },
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "Server=localhost;Initial Catalog=Backend_1;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!",
      "MinimumLevel": "Debug"
    },
    "Products": [{
      "Alias": "RM_Tests_Mac_SqlServer",
      "MigrationFilesRootDirectory": "/Users/.../Testing/MigrationFiles/Tests_SqlServer",
      "MigrationErrorAction": "Rollback",
      "TargetGroups": [
        {
          "Alias": "Backend",
          "DatabaseType": "SqlServer",
          "TargetMigrationOrder": "Simultaneously",
          "HashValidationScope": "SqlBlocks",
          "Targets": [
            {
              "Alias": "Backend1",
              "ConnectionString": "Server=localhost;Initial Catalog=Backend_1;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!"
            },
            {
              "Alias": "Backend2",
              "ConnectionString": "Server=localhost;Initial Catalog=Backend_2;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!"
            }
          ]
        },
        {
          "Alias": "Frontend",
          "DatabaseType": "SqlServer",
          "Targets": [
            {
              "Alias": "Frontend",
              "ConnectionString": "Server=localhost;Initial Catalog=Frontend;TrustServerCertificate=true;MultiSubnetFailover=True;User Id=sa;Password=P@ssw0rd!"
            }
          ]
        }
      ]
    }]
  }
}
```

> **Note**: Mac configs use absolute paths for `MigrationFilesRootDirectory`. Windows configs use Windows-style paths. The `MigrationErrorAction` is set to `Rollback` (not the default `Terminate`) to test rollback behavior.

## Connection Strings

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

> **Note**: MySQL uses port 3307 (external) to avoid conflict with MariaDB on 3306.

### SQLite

```
Data Source=/path/to/database.sqlite
```

> **Note**: SQLite is file-based and does not require Docker. Engine tests create temporary database files under `Path.GetTempPath()/RayMigrator_SqliteTests/raytest_<guid>.sqlite` (see `SqliteFixture.cs`). The fixture deletes the files in `DisposeAsync`.

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker-compose --env-file default.env --profile all logs rm_db_sqlserver

# Check if port is in use
netstat -an | grep 1433
```

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
docker-compose --env-file default.env --profile all down -v

# Rebuild and start
docker-compose --env-file default.env build --no-cache
docker-compose --env-file default.env --profile all up -d
```

## Related Documentation

- [Unit Tests](unit-tests.md)
- [Engine Tests](engine-tests.md)
- [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md)
