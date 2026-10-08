/*
[RayMigrator]
Description = "Create table dbo.Categories"
*/
CREATE TABLE dbo.Categories
(
    Id   INT           NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL
);
