/*
[RayMigrator]
Description = "Seed categories"
*/
INSERT INTO dbo.Categories (Id, Name) VALUES (1, N'Fiction');
GO
INSERT INTO dbo.Categories (Id, Name) VALUES (2, N'Science');
GO
-- Bug: Id 1 already exists, so this block fails with a primary key violation.
INSERT INTO dbo.Categories (Id, Name) VALUES (1, N'History');
