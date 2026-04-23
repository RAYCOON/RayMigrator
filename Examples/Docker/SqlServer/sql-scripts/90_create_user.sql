PRINT '';
PRINT '### EXECUTE: 90_create_user.sql ###';
GO

PRINT '# Create user [rmuser] for DB [RayMigratorRepository] ###';
GO
USE [RayMigratorRepository]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO

PRINT '# Create user [rmuser] for DB [SimpleApplicationDB] ###';
GO
USE [SimpleApplicationDB]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO

PRINT '# Create user [rmuser] for DB [BackendDB] ###';
GO
USE [BackendDB]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO
