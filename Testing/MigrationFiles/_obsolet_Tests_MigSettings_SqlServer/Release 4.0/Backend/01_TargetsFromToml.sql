/*
[RayMigrator]
Description = "Targets from TOML test"
Targets = ["Backend1", "Backend2"]
*/

INSERT INTO [dbo].[MigSettingsMarker] ([MarkerName]) VALUES ('R4.0_Backend_TargetsFromToml')
