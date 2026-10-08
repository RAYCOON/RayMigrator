/*
[RayMigrator]
Description = "View dbo.vwBooks, re-created on every run"
RunAlways = true
*/
CREATE OR ALTER VIEW dbo.vwBooks AS
SELECT b.Id, b.Title, a.Name AS Author
FROM dbo.Books AS b
LEFT JOIN dbo.Authors AS a ON a.Id = b.AuthorId;
