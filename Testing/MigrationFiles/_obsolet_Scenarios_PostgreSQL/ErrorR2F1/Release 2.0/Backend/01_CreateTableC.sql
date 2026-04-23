/*
[RayMigrator]
Description = "Create TableC (intentional error)"
*/

CREATE TABLE tablec (
    id NONEXISTENT_TYPE NOT NULL,
    description VARCHAR(300) NULL,
    CONSTRAINT pk_tablec PRIMARY KEY (id)
);
