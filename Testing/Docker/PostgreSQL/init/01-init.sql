-- PostgreSQL initialization script
-- This script is executed as the postgres superuser

-- Create the user if it does not exist
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayuser') THEN
        CREATE USER rayuser WITH PASSWORD 'raypass123';
    END IF;
END
$$;

-- Create the database if it does not exist
SELECT 'CREATE DATABASE raydb OWNER rayuser' 
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb')\gexec

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE raydb TO rayuser;
GRANT CREATE ON DATABASE raydb TO rayuser;

-- Create the second database
SELECT 'CREATE DATABASE raydb2 OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb2')\gexec

-- Create the third database for the frontend
SELECT 'CREATE DATABASE raydb_frontend OWNER postgres'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'raydb_frontend')\gexec

GRANT ALL PRIVILEGES ON DATABASE raydb_frontend TO rayuser;
GRANT CREATE ON DATABASE raydb_frontend TO rayuser;

-- Connect to the database as rayuser
\c raydb rayuser;

-- Create the schema
CREATE SCHEMA IF NOT EXISTS ray_schema;

-- Set the schema as default
SET search_path TO ray_schema, public;

-- Create sample tables
-- Table for system information
CREATE TABLE IF NOT EXISTS ray_schema.system_info (
    id SERIAL PRIMARY KEY,
    property VARCHAR(255) NOT NULL UNIQUE,
    value TEXT,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Table for the audit log
CREATE TABLE IF NOT EXISTS ray_schema.audit_log (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(255),
    action VARCHAR(50),
    user_name VARCHAR(255),
    changed_data JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Function for the updated_at trigger
CREATE OR REPLACE FUNCTION ray_schema.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger for updated_at
CREATE TRIGGER update_system_info_updated_at 
    BEFORE UPDATE ON ray_schema.system_info
    FOR EACH ROW
    EXECUTE FUNCTION ray_schema.update_updated_at_column();

-- Insert initial data
INSERT INTO ray_schema.system_info (property, value, description) VALUES 
    ('version', '1.0.0', 'System version'),
    ('initialized', 'true', 'Database initialization status'),
    ('database_type', 'PostgreSQL', 'Database engine type'),
    ('schema_version', '1', 'Current schema version')
ON CONFLICT (property) DO NOTHING;

-- Create additional users with specific privileges
-- Reconnect as the postgres superuser
\c raydb postgres;

-- Create a read-only user
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

-- Application user with restricted privileges
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

-- Show all created users
\du

-- Show all schemas
\dn

-- Show tables in ray_schema
\dt ray_schema.*