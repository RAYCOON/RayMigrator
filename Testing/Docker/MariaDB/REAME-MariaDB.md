# MariaDB Docker Setup

Diese Anleitung beschreibt die Installation und Konfiguration von MariaDB in Docker.

## Ordnerstruktur

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

### 1. Dateien erstellen

Erstellen Sie die Ordnerstruktur und kopieren Sie alle Dateien:

```bash
mkdir -p MariaDB/config MariaDB/init
```

### 2. Container starten

```bash
# Nur MariaDB starten
docker-compose --profile mariadb up -d

# Alle Services starten
docker-compose --profile all up -d

# Build und Start
docker-compose --profile mariadb up -d --build
```

### 3. Verbindung testen

```bash
# Verbindung zum Container
docker exec -it rm_db_mariadb mysql -u root -p

# Oder mit dem erstellten Benutzer
docker exec -it rm_db_mariadb mysql -u rayuser -p
```

## Konfiguration

### Umgebungsvariablen (.env)

- `MARIADB_ROOT_PASSWORD`: Root-Passwort (Standard: root123)
- `MARIADB_DATABASE`: Standard-Datenbankname (Standard: raydb)
- `MARIADB_USER`: Benutzer für die Anwendung (Standard: rayuser)
- `MARIADB_PASSWORD`: Passwort für den Anwendungsbenutzer (Standard: raypass123)

### Verbindungsdetails

- **Host**: localhost (oder rm_db_mariadb innerhalb des Docker-Netzwerks)
- **Port**: 3306
- **Datenbank**: raydb
- **Benutzer**: rayuser
- **Passwort**: raypass123

### Erstellte Benutzer

1. **root**: Vollzugriff (Passwort: root123)
2. **rayuser**: Vollzugriff auf raydb (Passwort: raypass123)
3. **readonly**: Nur-Lese-Zugriff (Passwort: readonly123)
4. **backup**: Backup-Berechtigungen (Passwort: backup123)

## Verwaltung

### Container-Management

```bash
# Status prüfen
docker-compose ps

# Logs anzeigen
docker-compose logs rm_db_mariadb

# Container stoppen
docker-compose --profile mariadb down

# Container und Volumes löschen
docker-compose --profile mariadb down -v
```

### Datenbank-Backup

```bash
# Backup erstellen
docker exec rm_db_mariadb mysqldump -u root -p raydb > backup.sql

# Backup wiederherstellen
docker exec -i rm_db_mariadb mysql -u root -p raydb < backup.sql
```

### Monitoring

```bash
# Prozesse anzeigen
docker exec rm_db_mariadb mysqladmin -u root -p processlist

# Status anzeigen
docker exec rm_db_mariadb mysqladmin -u root -p status
```

## Fehlerbehebung

### Häufige Probleme

1. **Container startet nicht**:
   ```bash
   docker-compose logs rm_db_mariadb
   ```

2. **Verbindung nicht möglich**:
   - Prüfen Sie die Ports (3306)
   - Überprüfen Sie die Firewall-Einstellungen
   - Kontrollieren Sie die Umgebungsvariablen

3. **Berechtigungsprobleme**:
   ```bash
   docker exec -it rm_db_mariadb mysql -u root -p
   SHOW GRANTS FOR 'rayuser'@'%';
   ```

### Neustart nach Problemen

```bash
# Container stoppen und entfernen
docker-compose down
docker volume rm ray_project_mariadb_data

# Neu starten
docker-compose --profile mariadb up -d --build
```

## Sicherheit

Für Produktionsumgebungen:

1. Ändern Sie alle Standard-Passwörter
2. Verwenden Sie starke Passwörter
3. Beschränken Sie Netzwerkzugriff
4. Aktivieren Sie SSL/TLS
5. Regelmäßige Backups
6. Monitoring und Logging

## Nützliche Befehle

```bash
# MariaDB-Version anzeigen
docker exec rm_db_mariadb mysql --version

# Konfiguration anzeigen
docker exec rm_db_mariadb mysql -u root -p -e "SHOW VARIABLES LIKE 'version%';"

# Datenbanken auflisten
docker exec rm_db_mariadb mysql -u root -p -e "SHOW DATABASES;"

# Tabellen auflisten
docker exec rm_db_mariadb mysql -u root -p -e "USE raydb; SHOW TABLES;"
```