/*
[RayMigrator]
Description = "Delete Alex Lee's 2nd UserProfile and UserPreference"
UseTransaction = false
*/

DO $$
BEGIN
    DELETE FROM UserPreferences WHERE LoginId = 11;
    DELETE FROM UserProfile WHERE LoginId = 11;
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
