PRINT '';
PRINT '### EXECUTE: 21_create_db_SimpleApplicationDB.sql ###';
GO

USE [master]
GO

PRINT 'Drop existing database ...'
IF EXISTS (SELECT TOP(1) 1 FROM master.dbo.sysdatabases WHERE ([name] = 'SimpleApplicationDB'))
BEGIN
    PRINT 'Drop DB [SimpleApplicationDB]..'
    ALTER DATABASE [SimpleApplicationDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [SimpleApplicationDB];
END;
GO

PRINT 'Create database [SimpleApplicationDB] ...';
CREATE DATABASE [SimpleApplicationDB] COLLATE Latin1_General_CI_AS;
GO

ALTER DATABASE [SimpleApplicationDB] SET COMPATIBILITY_LEVEL = 160
GO
ALTER DATABASE [SimpleApplicationDB] SET MULTI_USER
GO
ALTER DATABASE [SimpleApplicationDB] SET READ_COMMITTED_SNAPSHOT OFF
GO
ALTER DATABASE [SimpleApplicationDB] SET RECOVERY SIMPLE
GO
