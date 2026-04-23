/*
[RayMigrator]
Description = "Create instance logins (PROD)"
Environments = ["Production"]
*/

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{ENV:DB_LOGIN_WEB}') THEN
        CREATE ROLE {ENV:DB_LOGIN_WEB} WITH LOGIN PASSWORD '{ENV:DB_PASSWORD_WEB}';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not create login for web-frontend. Maybe it already exists?';
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{ENV:DB_LOGIN_BAK}') THEN
        CREATE ROLE {ENV:DB_LOGIN_BAK} WITH LOGIN PASSWORD '{ENV:DB_PASSWORD_BAK}';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not create login for backend. Maybe it already exists?';
END $$;
