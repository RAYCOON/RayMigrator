/*
[RayMigrator]
Description = "Rollback: remove the foreign key and Books.AuthorId"
*/
ALTER TABLE dbo.Books DROP CONSTRAINT IF EXISTS FK_Books_Authors;
GO
ALTER TABLE dbo.Books DROP COLUMN IF EXISTS AuthorId;
