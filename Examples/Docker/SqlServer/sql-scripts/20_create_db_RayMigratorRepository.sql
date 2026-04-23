PRINT '';
PRINT '### EXECUTE: 20_create_db_RayMigratorRepository.sql ###';
GO

USE [master]
GO

PRINT 'Drop existing database ...'
IF EXISTS (SELECT TOP(1) 1 FROM master.dbo.sysdatabases WHERE ([name] = 'RayMigratorRepository'))
BEGIN
    PRINT 'Drop DB [RayMigratorRepository]..'
    ALTER DATABASE [RayMigratorRepository] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [RayMigratorRepository];
END;
GO

PRINT 'Create database [RayMigratorRepository] ...';
CREATE DATABASE [RayMigratorRepository] COLLATE Latin1_General_CI_AS;
GO

ALTER DATABASE [RayMigratorRepository] SET COMPATIBILITY_LEVEL = 160
GO
ALTER DATABASE [RayMigratorRepository] SET MULTI_USER
GO
ALTER DATABASE [RayMigratorRepository] SET READ_COMMITTED_SNAPSHOT OFF
GO
ALTER DATABASE [RayMigratorRepository] SET RECOVERY SIMPLE
GO
