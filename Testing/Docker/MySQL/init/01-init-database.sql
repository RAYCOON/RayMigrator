-- Erstelle die Hauptdatenbank (falls nicht bereits durch Umgebungsvariable erstellt)
CREATE DATABASE IF NOT EXISTS raydb CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Erstelle den Benutzer (falls nicht bereits durch Umgebungsvariable erstellt)
CREATE USER IF NOT EXISTS 'rayuser'@'%' IDENTIFIED BY 'raypass123';

-- Zweite Datenbank erstellen
CREATE DATABASE IF NOT EXISTS raydb2 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Dritte Datenbank für Frontend erstellen
CREATE DATABASE IF NOT EXISTS raydb_frontend CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Gewähre alle Berechtigungen für die raydb Datenbank
GRANT ALL PRIVILEGES ON raydb.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb2.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb_frontend.* TO 'rayuser'@'%';

-- Gewähre globale Berechtigungen für CREATE USER (benötigt für Migrations)
GRANT CREATE USER ON *.* TO 'rayuser'@'%';

-- Gewähre Berechtigungen für Remote-Verbindungen (MySQL 8.0+ erfordert separates CREATE USER + GRANT)
CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY 'root123';
GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;

-- Aktualisiere die Berechtigungen
FLUSH PRIVILEGES;

-- Verwende die raydb Datenbank
USE raydb;

-- Erstelle eine Beispieltabelle für Tests
CREATE TABLE IF NOT EXISTS test_connection (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Füge einen Testwert ein
INSERT INTO test_connection (name) VALUES ('MySQL Connection Test');

-- Zeige alle Datenbanken an (für Debugging)
SHOW DATABASES;
