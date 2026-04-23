#!/bin/bash
set -e

echo "Starte MariaDB-Setup-Skript..."

# Während der Initialisierung ist kein Passwort erforderlich
echo "MariaDB ist bereit. Führe zusätzliche Setup-Schritte aus..."

# Erstelle zusätzliche Benutzer oder führe weitere Konfigurationen aus
# Kein Passwort während der Initialisierung erforderlich
mariadb -u root -p"${MYSQL_ROOT_PASSWORD}" <<-EOSQL
    -- Erstelle einen Nur-Lese-Benutzer
    CREATE USER IF NOT EXISTS 'readonly'@'%' IDENTIFIED BY 'readonly123';
    GRANT SELECT ON ${MYSQL_DATABASE}.* TO 'readonly'@'%';
    
    -- Erstelle einen Backup-Benutzer
    CREATE USER IF NOT EXISTS 'backup'@'%' IDENTIFIED BY 'backup123';
    GRANT SELECT, LOCK TABLES, SHOW VIEW, EVENT, TRIGGER ON ${MYSQL_DATABASE}.* TO 'backup'@'%';
    
    -- Aktualisiere Berechtigungen
    FLUSH PRIVILEGES;
    
    -- Zeige alle Benutzer an
    SELECT User, Host FROM mysql.user;
EOSQL

echo "MariaDB-Setup abgeschlossen!"

# Erstelle eine Statusdatei
touch /tmp/mariadb-setup-complete