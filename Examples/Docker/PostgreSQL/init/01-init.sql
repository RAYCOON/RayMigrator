-- PostgreSQL initialization script for RayMigrator examples

-- Create user if not exists
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'rayuser') THEN
        CREATE USER rayuser WITH PASSWORD 'raypass123';
    END IF;
END
$$;

-- Create database FrontendDB if not exists
SELECT 'CREATE DATABASE "FrontendDB" OWNER rayuser'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'FrontendDB')\gexec

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE "FrontendDB" TO rayuser;
GRANT CREATE ON DATABASE "FrontendDB" TO rayuser;
