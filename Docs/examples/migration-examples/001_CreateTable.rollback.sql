/*
[RayMigrator]
Description = "Rollback: Drop users table"
*/

-- Rollback script for 001_CreateTable.sql
-- Drops the Users table and its indexes

DROP INDEX IF EXISTS IX_Users_IsActive ON Users;
DROP INDEX IF EXISTS IX_Users_Email ON Users;
DROP TABLE IF EXISTS Users;
