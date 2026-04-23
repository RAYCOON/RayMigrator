/*
[RayMigrator]
Description = "Create errortest_data table and insert initial seed data"
*/

CREATE TABLE errortest_data (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE
);

INSERT INTO errortest_data (name) VALUES ('R1_data');
