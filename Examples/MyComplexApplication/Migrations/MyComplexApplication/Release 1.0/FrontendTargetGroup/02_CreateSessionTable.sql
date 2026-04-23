/*
[RayMigrator]
Description = "Create user_session table for session tracking"
*/

CREATE TABLE user_session
(
    id              SERIAL PRIMARY KEY,
    user_profile_id INT NOT NULL REFERENCES user_profile(id),
    session_token   VARCHAR(256) NOT NULL UNIQUE,
    expires_at      TIMESTAMP NOT NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);
