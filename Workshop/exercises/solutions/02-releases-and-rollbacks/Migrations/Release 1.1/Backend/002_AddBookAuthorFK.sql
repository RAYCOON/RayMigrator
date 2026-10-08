/*
[RayMigrator]
Description = "Add Books.AuthorId with a foreign key to Authors"
*/
ALTER TABLE dbo.Books ADD AuthorId INT NULL;
GO
ALTER TABLE dbo.Books ADD CONSTRAINT FK_Books_Authors
    FOREIGN KEY (AuthorId) REFERENCES dbo.Authors (Id);
