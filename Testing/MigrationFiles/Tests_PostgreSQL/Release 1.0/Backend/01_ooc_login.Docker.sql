/*
[RayMigrator]
Description = "Create instance logins (DEV)"
Environments = ["Docker"]
*/

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'login_web') THEN
        CREATE ROLE login_web WITH LOGIN PASSWORD 'DEV-30ae!';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not create login for web-frontend. Maybe it already exists?';
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'login_bak') THEN
        CREATE ROLE login_bak WITH LOGIN PASSWORD 'DEV-acd7!';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not create login for backend. Maybe it already exists?';
END $$;
