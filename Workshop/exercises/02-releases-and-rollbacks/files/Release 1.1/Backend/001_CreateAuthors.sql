/*
[RayMigrator]
Description = "Create table dbo.Authors"
*/
CREATE TABLE dbo.Authors
(
    Id   INT           NOT NULL CONSTRAINT PK_Authors PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL
);
