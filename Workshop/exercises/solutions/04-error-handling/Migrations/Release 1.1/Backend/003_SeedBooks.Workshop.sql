/*
[RayMigrator]
Description = "Seed authors and books (Workshop environment only)"
*/
INSERT INTO dbo.Authors (Id, Name) VALUES
    (1, N'Ada Lovelace'),
    (2, N'Alan Turing');
GO
INSERT INTO dbo.Books (Id, Title, AuthorId) VALUES
    (1, N'Notes on the Analytical Engine', 1),
    (2, N'Computing Machinery and Intelligence', 2);
