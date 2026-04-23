/*
[RayMigrator]
Description = "Create instance logins (DEV)"
Environments = ["Docker"]
*/

BEGIN TRY
	CREATE LOGIN [login_web] WITH PASSWORD = N'DEV-30ae!';
END TRY
BEGIN CATCH
	PRINT 'Could not create login for web-frontend. Maybe it already exists?';
END CATCH;

BEGIN TRY
	CREATE LOGIN [login_bak] WITH PASSWORD = N'DEV-acd7!';
END TRY
BEGIN CATCH
	PRINT 'Could not create login for backend. Maybe it already exists?';
END CATCH;
