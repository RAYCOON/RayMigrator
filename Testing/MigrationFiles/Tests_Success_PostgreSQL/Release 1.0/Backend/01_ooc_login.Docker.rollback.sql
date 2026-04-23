/*
[RayMigrator]
Description = "Drop instance logins (DEV)"
Environments = ["Docker"]
*/

DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'login_web') THEN
        DROP ROLE login_web;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not drop login for web-frontend. Maybe it does not exist?';
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'login_bak') THEN
        DROP ROLE login_bak;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Could not drop login for backend. Maybe it does not exist?';
END $$;
