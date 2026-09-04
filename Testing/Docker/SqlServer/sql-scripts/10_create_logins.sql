PRINT '';
PRINT '### EXECUTE: 10_create_logins.sql ###';
GO

USE [master]
GO

PRINT 'Drop existing logins ...';
IF EXISTS (SELECT TOP(1) 1 FROM [master].[sys].[server_principals] WHERE [name] = 'rmlogin') DROP LOGIN [rmlogin];

USE [master]
GO
PRINT 'Create login ...'
CREATE LOGIN [rmlogin] WITH PASSWORD=N'$(RM_LOGIN_PASSWORD)', DEFAULT_DATABASE=[master], DEFAULT_LANGUAGE=[English], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF;
GO
ALTER SERVER ROLE [sysadmin] ADD MEMBER [rmlogin]
GO
