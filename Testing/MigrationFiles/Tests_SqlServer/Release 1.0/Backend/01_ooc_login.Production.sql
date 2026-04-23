/*
[RayMigrator]
Description = "Create instance logins (PROD)"
Environments = ["Production"]
*/

BEGIN TRY
	CREATE LOGIN [{ENV:DB_LOGIN_WEB}] WITH PASSWORD = N'{ENV:DB_PASSWORD_WEB}';
END TRY
BEGIN CATCH
	PRINT 'Could not create login for web-frontend. Maybe it already exists?';
END CATCH;

BEGIN TRY
	CREATE LOGIN [{ENV:DB_LOGIN_BAK}] WITH PASSWORD = N'{ENV:DB_PASSWORD_BAK}';
END TRY
BEGIN CATCH
	PRINT 'Could not create login for backend. Maybe it already exists?';
END CATCH;
