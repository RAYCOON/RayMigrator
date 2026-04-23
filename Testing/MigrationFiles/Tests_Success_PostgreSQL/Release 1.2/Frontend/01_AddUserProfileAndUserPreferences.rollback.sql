/*
[RayMigrator]
Description = "Create Alex Lee's UserProfile and UserPreference"
UseTransaction = false
*/

DELETE FROM UserProfile WHERE LoginId = 10;
DELETE FROM UserPreferences WHERE LoginId = 10;
