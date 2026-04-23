/*
[RayMigrator]
Description = "Create audit_log table for tracking user actions"
*/

CREATE TABLE audit_log
(
    id              SERIAL PRIMARY KEY,
    action          VARCHAR(50) NOT NULL,
    entity_type     VARCHAR(100) NOT NULL,
    entity_id       INT NULL,
    user_profile_id INT NULL REFERENCES user_profile(id),
    details         JSONB NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);
