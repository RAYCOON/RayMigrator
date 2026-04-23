-- PostgreSQL initialization script
-- Dieses Script wird als postgres Superuser ausgeführt

-- Benutzer erstellen falls nicht vorhanden
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayuser') THEN
        CREATE USER rayuser WITH PASSWORD 'raypass123';
    END IF;
END
$$;

-- Datenbank erstellen falls nicht vorhanden
SELECT 'CREATE DATABASE raydb OWNER rayuser' 
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb')\gexec

-- Rechte vergeben
GRANT ALL PRIVILEGES ON DATABASE raydb TO rayuser;
GRANT CREATE ON DATABASE raydb TO rayuser;

-- Zweite Datenbank erstellen
SELECT 'CREATE DATABASE raydb2 OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb2')\gexec

-- Dritte Datenbank für Frontend erstellen
SELECT 'CREATE DATABASE raydb_frontend OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb_frontend')\gexec

GRANT ALL PRIVILEGES ON DATABASE raydb_frontend TO rayuser;
GRANT CREATE ON DATABASE raydb_frontend TO rayuser;

-- Als rayuser zur Datenbank verbinden
\c raydb rayuser;

-- Schema erstellen
CREATE SCHEMA IF NOT EXISTS ray_schema;

-- Schema als Standard setzen
SET search_path TO ray_schema, public;

-- Beispieltabellen erstellen
-- Tabelle für Systeminformationen
CREATE TABLE IF NOT EXISTS ray_schema.system_info (
    id SERIAL PRIMARY KEY,
    property VARCHAR(255) NOT NULL UNIQUE,
    value TEXT,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabelle für Audit-Log
CREATE TABLE IF NOT EXISTS ray_schema.audit_log (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(255),
    action VARCHAR(50),
    user_name VARCHAR(255),
    changed_data JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Funktion für updated_at Trigger
CREATE OR REPLACE FUNCTION ray_schema.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger für updated_at
CREATE TRIGGER update_system_info_updated_at 
    BEFORE UPDATE ON ray_schema.system_info
    FOR EACH ROW
    EXECUTE FUNCTION ray_schema.update_updated_at_column();

-- Initiale Daten einfügen
INSERT INTO ray_schema.system_info (property, value, description) VALUES 
    ('version', '1.0.0', 'System version'),
    ('initialized', 'true', 'Database initialization status'),
    ('database_type', 'PostgreSQL', 'Database engine type'),
    ('schema_version', '1', 'Current schema version')
ON CONFLICT (property) DO NOTHING;

-- Zusätzliche Benutzer mit spezifischen Rechten erstellen
-- Als postgres Superuser wieder verbinden
\c raydb postgres;

-- Read-only Benutzer erstellen
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayreader') THEN
        CREATE USER rayreader WITH PASSWORD 'reader123';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE raydb TO rayreader;
GRANT USAGE ON SCHEMA ray_schema TO rayreader;
GRANT SELECT ON ALL TABLES IN SCHEMA ray_schema TO rayreader;
ALTER DEFAULT PRIVILEGES IN SCHEMA ray_schema GRANT SELECT ON TABLES TO rayreader;

-- Anwendungsbenutzer mit eingeschränkten Rechten
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayapp') THEN
        CREATE USER rayapp WITH PASSWORD 'app123';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE raydb TO rayapp;
GRANT USAGE ON SCHEMA ray_schema TO rayapp;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ray_schema TO rayapp;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA ray_schema TO rayapp;
ALTER DEFAULT PRIVILEGES IN SCHEMA ray_schema GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO rayapp;
ALTER DEFAULT PRIVILEGES IN SCHEMA ray_schema GRANT USAGE, SELECT ON SEQUENCES TO rayapp;

-- Zeige alle erstellten Benutzer
\du

-- Zeige alle Schemas
\dn

-- Zeige Tabellen im ray_schema
\dt ray_schema.*