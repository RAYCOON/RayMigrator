/*
[RayMigrator]
Description = "Create notification table for user notifications"
*/

CREATE TABLE notification
(
    id              SERIAL PRIMARY KEY,
    user_profile_id INT NOT NULL REFERENCES user_profile(id) ON DELETE CASCADE,
    title           VARCHAR(200) NOT NULL,
    message         TEXT NOT NULL,
    is_read         BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);
