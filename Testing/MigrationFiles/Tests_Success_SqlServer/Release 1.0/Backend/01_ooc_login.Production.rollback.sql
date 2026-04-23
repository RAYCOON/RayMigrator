/*
[RayMigrator]
Description = "Drop instance logins (PROD)"
Environments = ["Production"]
*/

BEGIN TRY
	DROP LOGIN [{ENV:DB_LOGIN_WEB}];
END TRY
BEGIN CATCH
	PRINT 'Could not drop login for web-frontend. Maybe it does not exist?';
END CATCH;

BEGIN TRY
	DROP LOGIN [{ENV:DB_LOGIN_BAK}];
END TRY
BEGIN CATCH
	PRINT 'Could not drop login for backend. Maybe it does not exist?';
END CATCH;
