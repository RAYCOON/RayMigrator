PRINT '';
PRINT '### EXECUTE: 90_create_user.sql ###';
GO

PRINT '# Create user [rmuser] for DB [Backend_1] ###';
GO
USE [Backend_1]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO

PRINT '# Create user [rmuser] for DB [Backend_2] ###';
GO
USE [Backend_2]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO

PRINT '# Create user [rmuser] for DB [Frontend] ###';
GO
USE [Frontend]
GO
CREATE USER [rmuser] FOR LOGIN [rmlogin] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [rmuser]
GO
