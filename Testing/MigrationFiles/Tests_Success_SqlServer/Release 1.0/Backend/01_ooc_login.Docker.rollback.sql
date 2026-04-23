/*
[RayMigrator]
Description = "Drop instance logins (DEV)"
Environments = ["Docker"]
*/

BEGIN TRY
	DROP LOGIN [login_web];
END TRY
BEGIN CATCH
	PRINT 'Could not drop login for web-frontend. Maybe it does not exist?';
END CATCH;

BEGIN TRY
	DROP LOGIN [login_bak];
END TRY
BEGIN CATCH
	PRINT 'Could not drop login for backend. Maybe it does not exist?';
END CATCH;
