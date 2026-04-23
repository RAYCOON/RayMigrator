# MySQL Docker Setup for RayMigrator Testing

## Overview
This directory contains the Docker configuration for running a MySQL 8.4 instance for RayMigrator integration tests.

## Configuration
- **Image**: mysql:8.4
- **Port**: 3307 (mapped to internal 3306, to avoid conflict with MariaDB)
- **Database**: raydb (+ raydb2 for multi-target tests)
- **User**: rayuser / raypass123
- **Root Password**: root123

## Usage

### Start MySQL container
```bash
cd Testing/Docker
docker-compose --env-file default.env --profile mysql up -d
```

### Connect to MySQL
```bash
docker exec rm_db_mysql mysql -u rayuser -praypass123 raydb
```

### Stop MySQL container
```bash
docker-compose --env-file default.env --profile mysql down
```

## Notes
- MySQL 8.0+ does not support `GRANT ... IDENTIFIED BY` syntax (split into CREATE USER + GRANT)
- MySQL 8.0+ removed query_cache_size and query_cache_type settings
- MySQL 8.0+ requires parentheses for expression defaults: `DEFAULT (UTC_TIMESTAMP())`
- Uses the same MySqlConnector driver as MariaDB
