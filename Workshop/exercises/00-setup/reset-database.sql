-- RayMigrator workshop: drop and recreate your personal database.
-- Use it to start an exercise over or to catch up with a model solution:
--   1. run this script in SSMS (replace <NAME> as in create-database.sql),
--   2. copy the model solution folder over Workshop/exercises/bookstore/,
--   3. run: raymigrator migrate-up -p BookStore -env Workshop
USE master;
GO
IF DB_ID('BookStore_<NAME>') IS NOT NULL
BEGIN
    ALTER DATABASE BookStore_<NAME> SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE BookStore_<NAME>;
END
GO
CREATE DATABASE BookStore_<NAME>;
GO
