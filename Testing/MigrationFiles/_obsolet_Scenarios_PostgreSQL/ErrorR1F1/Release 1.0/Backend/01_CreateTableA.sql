/*
[RayMigrator]
Description = "Create TableA (intentional error)"
*/

CREATE TABLE tablea (
    id NONEXISTENT_TYPE NOT NULL,
    name VARCHAR(100) NOT NULL,
    CONSTRAINT pk_tablea PRIMARY KEY (id)
);
