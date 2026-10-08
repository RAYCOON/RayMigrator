/*
[RayMigrator]
Description = "Create table dbo.Books"
*/
CREATE TABLE dbo.Books
(
    Id    INT           NOT NULL CONSTRAINT PK_Books PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL
);

-- Reviewed during the workshop
