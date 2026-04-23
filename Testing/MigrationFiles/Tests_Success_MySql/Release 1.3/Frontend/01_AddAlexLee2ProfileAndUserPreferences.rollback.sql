/*
[RayMigrator]
Description = "Delete Alex Lee's 2nd UserProfile and UserPreference"
UseTransaction = false
*/

START TRANSACTION;

DELETE FROM UserPreferences WHERE LoginId = 11;
DELETE FROM UserProfile WHERE LoginId = 11;

COMMIT;
