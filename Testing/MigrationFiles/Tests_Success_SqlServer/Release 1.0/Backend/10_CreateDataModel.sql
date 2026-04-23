/*
[RayMigrator]
Description = "Create initial model"
UseTransaction = false
*/

-- Create tables section -------------------------------------------------

-- Table dbo.Person

CREATE TABLE [dbo].[Person]
(
 [Id] Int IDENTITY(1,1) NOT NULL,
 [LoginId] Int NOT NULL,
 [SexId] Tinyint NOT NULL,
 [NameFirst] Varchar(100) NOT NULL,
 [NameLast] Varchar(100) NOT NULL,
 [DateOfBirth] Date NULL
)
go

-- Add keys for table dbo.Person

ALTER TABLE [dbo].[Person] ADD CONSTRAINT [PK_Person] PRIMARY KEY ([Id])
go

-- Table dbo.Sex

CREATE TABLE [dbo].[Sex]
(
 [Id] Tinyint NOT NULL,
 [Name] Varchar(100) NOT NULL
)
go

-- Add keys for table dbo.Sex

ALTER TABLE [dbo].[Sex] ADD CONSTRAINT [PK_Sex] PRIMARY KEY ([Id])
go

-- Table dbo.Login

CREATE TABLE [dbo].[Login]
(
 [Id] Int IDENTITY(1,1) NOT NULL,
 [Username] Varchar(100) NOT NULL,
 [PasswordHash] Varchar(256) NOT NULL,
 [LastLogin] Datetime2(2) NULL
)
go

-- Add keys for table dbo.Login

ALTER TABLE [dbo].[Login] ADD CONSTRAINT [PK_Login] PRIMARY KEY ([Id])
go

-- Create foreign keys (relationships) section ------------------------------------------------- 


ALTER TABLE [dbo].[Person] ADD CONSTRAINT [Sex_Person] FOREIGN KEY ([SexId]) REFERENCES [dbo].[Sex] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
go



ALTER TABLE [dbo].[Person] ADD CONSTRAINT [Login_Person] FOREIGN KEY ([LoginId]) REFERENCES [dbo].[Login] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
go




