/*
[RayMigrator]
Description = "Rollback: delete the seeded books and authors"
*/
DELETE FROM dbo.Books;
DELETE FROM dbo.Authors;
