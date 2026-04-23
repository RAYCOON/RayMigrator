/*
[RayMigrator]
Description = "Create Alex Lee's UserProfile and UserPreference"
UseTransaction = false
*/

DELETE FROM [dbo].[UserProfile] WHERE LoginId = 10;
DELETE FROM [dbo].[UserPreferences] WHERE LoginId = 10;
