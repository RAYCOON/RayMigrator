/*
[RayMigrator]
Description = "Multi-block migration with GO separators"
UseTransaction = false
*/

-- SQL Server migrations can contain multiple blocks separated by GO
-- Each block is executed separately
-- UseTransaction = false because some DDL operations cannot be combined

-- Block 1: Create stored procedure
CREATE PROCEDURE GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.Id,
        u.Username,
        u.Email,
        u.IsActive,
        r.Name AS RoleName
    FROM Users u
    INNER JOIN UserRoles r ON u.RoleId = r.Id
    WHERE u.Id = @UserId;
END
GO

-- Block 2: Create function
CREATE FUNCTION GetActiveUserCount()
RETURNS INT
AS
BEGIN
    DECLARE @Count INT;
    SELECT @Count = COUNT(*) FROM Users WHERE IsActive = 1;
    RETURN @Count;
END
GO

-- Block 3: Create view
CREATE VIEW vw_ActiveUsers
AS
SELECT
    u.Id,
    u.Username,
    u.Email,
    u.CreatedAt,
    r.Name AS RoleName
FROM Users u
INNER JOIN UserRoles r ON u.RoleId = r.Id
WHERE u.IsActive = 1;
GO

-- Block 4: Grant permissions
GRANT SELECT ON vw_ActiveUsers TO PUBLIC;
