/*
[RayMigrator]
Description = "Create user_profile table for frontend user management"
*/

CREATE TABLE user_profile
(
    id           SERIAL PRIMARY KEY,
    username     VARCHAR(100) NOT NULL UNIQUE,
    display_name VARCHAR(200) NOT NULL,
    email        VARCHAR(255) NULL,
    created_at   TIMESTAMP NOT NULL DEFAULT NOW()
);
