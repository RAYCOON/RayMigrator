/*
[RayMigrator]
Description = "Delete UserProfiles and UserPreferences"
UseTransaction = false
*/

DO $$
BEGIN
    DELETE FROM UserPreferences WHERE LoginId IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    DELETE FROM UserProfile WHERE LoginId IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
