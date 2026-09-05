# MariaDB Docker Setup

This guide describes the installation and configuration of MariaDB in Docker.

## Folder Structure

```
.
├── docker-compose.yml
├── .env
├── MariaDB/
│   ├── Dockerfile
│   ├── config/
│   │   └── my.cnf
│   └── init/
│       ├── 01-init-database.sql
│       └── 02-setup-permissions.sh
└── README-MariaDB.md
```

## Installation

### 1. Create files

Create the folder structure and copy all files:

```bash
mkdir -p MariaDB/config MariaDB/init
```

### 2. Start the container

```bash
# Start MariaDB only
docker-compose --profile mariadb up -d

# Start all services
docker-compose --profile all up -d

# Build and start
docker-compose --profile mariadb up -d --build
```

### 3. Test the connection

```bash
# Connect to the container
docker exec -it rm_db_mariadb mysql -u root -p

# Or with the created user
docker exec -it rm_db_mariadb mysql -u rayuser -p
```

## Configuration

### Environment variables (.env)

- `MARIADB_ROOT_PASSWORD`: Root password (default: root123)
- `MARIADB_DATABASE`: Default database name (default: raydb)
- `MARIADB_USER`: Application user (default: rayuser)
- `MARIADB_PASSWORD`: Password of the application user (default: raypass123)

### Connection details

- **Host**: localhost (or rm_db_mariadb inside the Docker network)
- **Port**: 3306
- **Database**: raydb
- **User**: rayuser
- **Password**: raypass123

### Created users

1. **root**: Full access (password: root123)
2. **rayuser**: Full access to raydb (password: raypass123)
3. **readonly**: Read-only access (password: readonly123)
4. **backup**: Backup privileges (password: backup123)

## Administration

### Container management

```bash
# Check status
docker-compose ps

# Show logs
docker-compose logs rm_db_mariadb

# Stop the container
docker-compose --profile mariadb down

# Remove the container and volumes
docker-compose --profile mariadb down -v
```

### Database backup

```bash
# Create a backup
docker exec rm_db_mariadb mysqldump -u root -p raydb > backup.sql

# Restore a backup
docker exec -i rm_db_mariadb mysql -u root -p raydb < backup.sql
```

### Monitoring

```bash
# Show processes
docker exec rm_db_mariadb mysqladmin -u root -p processlist

# Show status
docker exec rm_db_mariadb mysqladmin -u root -p status
```

## Troubleshooting

### Common problems

1. **Container does not start**:
   ```bash
   docker-compose logs rm_db_mariadb
   ```

2. **Connection not possible**:
   - Check the ports (3306)
   - Check the firewall settings
   - Check the environment variables

3. **Permission problems**:
   ```bash
   docker exec -it rm_db_mariadb mysql -u root -p
   SHOW GRANTS FOR 'rayuser'@'%';
   ```

### Restart after problems

```bash
# Stop and remove the container
docker-compose down
docker volume rm ray_project_mariadb_data

# Start again
docker-compose --profile mariadb up -d --build
```

## Security

For production environments:

1. Change all default passwords
2. Use strong passwords
3. Restrict network access
4. Enable SSL/TLS
5. Regular backups
6. Monitoring and logging

## Useful Commands

```bash
# Show MariaDB version
docker exec rm_db_mariadb mysql --version

# Show configuration
docker exec rm_db_mariadb mysql -u root -p -e "SHOW VARIABLES LIKE 'version%';"

# List databases
docker exec rm_db_mariadb mysql -u root -p -e "SHOW DATABASES;"

# List tables
docker exec rm_db_mariadb mysql -u root -p -e "USE raydb; SHOW TABLES;"
```
