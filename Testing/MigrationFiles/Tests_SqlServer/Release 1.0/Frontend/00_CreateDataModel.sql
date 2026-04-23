/*
[RayMigrator]
Description = "Create initial model"
*/

-- Create tables section -------------------------------------------------

-- Table dbo.UserProfile

CREATE TABLE [dbo].[UserProfile]
(
 [Id] Int IDENTITY(1,1) NOT NULL,
 [LoginId] Int NOT NULL,
 [AvatarUrl] Varchar(256) NULL,
 [Bio] Nvarchar(500) NULL,
 [Location] Nvarchar(100) NULL,
 [Website] Varchar(256) NULL,
 [JoinDate] Date NOT NULL,
 [LastActive] Datetime2(2) NOT NULL
)
go

-- Create indexes for table dbo.UserProfile

CREATE UNIQUE INDEX [IX_LoginId_uq] ON [dbo].[UserProfile] ([LoginId])
go

-- Add keys for table dbo.UserProfile

ALTER TABLE [dbo].[UserProfile] ADD CONSTRAINT [PK_UserProfile] PRIMARY KEY ([Id])
go

-- Table dbo.UserPreferences

CREATE TABLE [dbo].[UserPreferences]
(
 [Id] Int IDENTITY(1,1) NOT NULL,
 [LoginId] Int NOT NULL,
 [Theme] Varchar(20) NOT NULL,
 [NotificationsEnabled] Bit NOT NULL,
 [Language] Varchar(10) NOT NULL
)
go

-- Create indexes for table dbo.UserPreferences

CREATE UNIQUE INDEX [IX_LoginId_uq] ON [dbo].[UserPreferences] ([LoginId])
go

-- Add keys for table dbo.UserPreferences

ALTER TABLE [dbo].[UserPreferences] ADD CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
go




