/*
[RayMigrator]
Description = "Create dashboard_widget table for user dashboard customization"
*/

CREATE TABLE dashboard_widget
(
    id              SERIAL PRIMARY KEY,
    user_profile_id INT NOT NULL REFERENCES user_profile(id) ON DELETE CASCADE,
    widget_type     VARCHAR(50) NOT NULL,
    position_x      INT NOT NULL DEFAULT 0,
    position_y      INT NOT NULL DEFAULT 0,
    config          JSONB NULL
);
