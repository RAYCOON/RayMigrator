/*
[RayMigrator]
Description = "Drop instance logins (PROD)"
Environments = ["Production"]
*/

DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{ENV:DB_LOGIN_WEB}') THEN
        DROP ROLE {ENV:DB_LOGIN_WEB};
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not drop login for web-frontend. Maybe it does not exist?';
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{ENV:DB_LOGIN_BAK}') THEN
        DROP ROLE {ENV:DB_LOGIN_BAK};
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not drop login for backend. Maybe it does not exist?';
END $$;
