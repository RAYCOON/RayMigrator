# PostgreSQL User Overview

## Created users and their credentials:

### 1. **postgres** (Superuser)
- **Role**: PostgreSQL superuser with all privileges
- **Password**: `postgres123`
- **Connection**:
  ```bash
  psql -h localhost -p 5432 -U postgres
  docker exec -it rm_db_postgresql psql -U postgres
  ```

### 2. **rayuser** (Database owner)
- **Role**: Owner of the raydb database
- **Password**: `raypass123`
- **Privileges**: All privileges on the raydb database
- **Connection**:
  ```bash
  psql -h localhost -p 5432 -U rayuser -d raydb
  docker exec -it rm_db_postgresql psql -U rayuser -d raydb
  ```

### 3. **rayreader** (Read-only)
- **Role**: Read-only access
- **Password**: `reader123`
- **Privileges**: SELECT on all tables in ray_schema
- **Connection**:
  ```bash
  psql -h localhost -p 5432 -U rayreader -d raydb
  ```

### 4. **rayapp** (Application user)
- **Role**: Standard application user
- **Password**: `app123`
- **Privileges**: SELECT, INSERT, UPDATE, DELETE on ray_schema
- **Connection**:
  ```bash
  psql -h localhost -p 5432 -U rayapp -d raydb
  ```

## Connection strings for applications:

```
# For admin access
postgresql://postgres:postgres123@localhost:5432/raydb

# For the normal application
postgresql://rayuser:raypass123@localhost:5432/raydb

# For read-only access
postgresql://rayreader:reader123@localhost:5432/raydb

# For the application with restricted privileges
postgresql://rayapp:app123@localhost:5432/raydb
```

## Database schema:

- **Database**: `raydb`
- **Schema**: `ray_schema`
- **Tables**:
    - `system_info` - System information
    - `audit_log` - Audit log
