/*
[RayMigrator]
Description = "Create initial model"
*/

CREATE TABLE UserProfile
(
 Id INT AUTO_INCREMENT NOT NULL,
 LoginId INT NOT NULL,
 AvatarUrl VARCHAR(256) NULL,
 Bio VARCHAR(500) NULL,
 Location VARCHAR(100) NULL,
 Website VARCHAR(256) NULL,
 JoinDate DATE NOT NULL,
 LastActive DATETIME(2) NOT NULL,
 CONSTRAINT PK_UserProfile PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE UNIQUE INDEX IX_UserProfile_LoginId_uq ON UserProfile (LoginId);

CREATE TABLE UserPreferences
(
 Id INT AUTO_INCREMENT NOT NULL,
 LoginId INT NOT NULL,
 Theme VARCHAR(20) NOT NULL,
 NotificationsEnabled BOOLEAN NOT NULL,
 Language VARCHAR(10) NOT NULL,
 CONSTRAINT PK_UserPreferences PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE UNIQUE INDEX IX_UserPreferences_LoginId_uq ON UserPreferences (LoginId);
