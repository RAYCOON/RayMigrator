-- Create the main database (if not already created via environment variable)
CREATE DATABASE IF NOT EXISTS raydb CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Create the user (if not already created via environment variable)
CREATE USER IF NOT EXISTS 'rayuser'@'%' IDENTIFIED BY 'raypass123';

-- Create the second database
CREATE DATABASE IF NOT EXISTS raydb2 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Create the third database for the frontend
CREATE DATABASE IF NOT EXISTS raydb_frontend CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Grant all privileges on the raydb database
GRANT ALL PRIVILEGES ON raydb.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb2.* TO 'rayuser'@'%';
GRANT ALL PRIVILEGES ON raydb_frontend.* TO 'rayuser'@'%';

-- Grant the global CREATE USER privilege (required for migrations)
GRANT CREATE USER ON *.* TO 'rayuser'@'%';

-- Grant privileges for remote connections (MySQL 8.0+ requires separate CREATE USER + GRANT)
CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY 'root123';
GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;

-- Reload the privileges
FLUSH PRIVILEGES;

-- Use the raydb database
USE raydb;

-- Create a sample table for tests
CREATE TABLE IF NOT EXISTS test_connection (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert a test value
INSERT INTO test_connection (name) VALUES ('MySQL Connection Test');

-- Show all databases (for debugging)
SHOW DATABASES;
