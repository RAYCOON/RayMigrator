# PostgreSQL Benutzerübersicht

## Erstellte Benutzer und ihre Zugangsdaten:

### 1. **postgres** (Superuser)
- **Rolle**: PostgreSQL Superuser mit allen Rechten
- **Passwort**: `postgres123`
- **Verbindung**:
  ```bash
  psql -h localhost -p 5432 -U postgres
  docker exec -it rm_db_postgresql psql -U postgres
  ```

### 2. **rayuser** (Datenbankbesitzer)
- **Rolle**: Besitzer der raydb Datenbank
- **Passwort**: `raypass123`
- **Rechte**: Alle Rechte auf raydb Datenbank
- **Verbindung**:
  ```bash
  psql -h localhost -p 5432 -U rayuser -d raydb
  docker exec -it rm_db_postgresql psql -U rayuser -d raydb
  ```

### 3. **rayreader** (Read-Only)
- **Rolle**: Nur-Lese-Zugriff
- **Passwort**: `reader123`
- **Rechte**: SELECT auf alle Tabellen in ray_schema
- **Verbindung**:
  ```bash
  psql -h localhost -p 5432 -U rayreader -d raydb
  ```

### 4. **rayapp** (Anwendungsbenutzer)
- **Rolle**: Standard-Anwendungsbenutzer
- **Passwort**: `app123`
- **Rechte**: SELECT, INSERT, UPDATE, DELETE auf ray_schema
- **Verbindung**:
  ```bash
  psql -h localhost -p 5432 -U rayapp -d raydb
  ```

## Verbindungsstrings für Anwendungen:

```
# Für Admin-Zugriff
postgresql://postgres:postgres123@localhost:5432/raydb

# Für normale Anwendung
postgresql://rayuser:raypass123@localhost:5432/raydb

# Für Read-Only Zugriff
postgresql://rayreader:reader123@localhost:5432/raydb

# Für Anwendung mit eingeschränkten Rechten
postgresql://rayapp:app123@localhost:5432/raydb
```

## Datenbank-Schema:

- **Datenbank**: `raydb`
- **Schema**: `ray_schema`
- **Tabellen**:
    - `system_info` - Systeminformationen
    - `audit_log` - Audit-Protokoll