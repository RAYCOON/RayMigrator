/*
[RayMigrator]
Description = "Create user_preference table for per-user settings"
*/

CREATE TABLE user_preference
(
    id              SERIAL PRIMARY KEY,
    user_profile_id INT NOT NULL REFERENCES user_profile(id) ON DELETE CASCADE,
    preference_key  VARCHAR(100) NOT NULL,
    preference_value TEXT NULL,
    UNIQUE(user_profile_id, preference_key)
);
