/*
[RayMigrator]
Description = "Insert master data for user roles"
RequireRollbackFile = true
MigrationErrorAction = "Rollback"
RollbackErrorAction = "Terminate"
*/

-- Insert default user roles
-- This migration adds initial lookup data

CREATE TABLE UserRoles (
    Id INT PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL,
    Description NVARCHAR(255) NULL
);

INSERT INTO UserRoles (Id, Name, Description) VALUES
    (1, 'Admin', 'Full system administrator'),
    (2, 'Manager', 'Department manager with elevated permissions'),
    (3, 'User', 'Standard user with basic permissions'),
    (4, 'ReadOnly', 'Read-only access to data');

-- Add foreign key to Users table
ALTER TABLE Users ADD RoleId INT NOT NULL DEFAULT 3;
ALTER TABLE Users ADD CONSTRAINT FK_Users_UserRoles FOREIGN KEY (RoleId) REFERENCES UserRoles(Id);
