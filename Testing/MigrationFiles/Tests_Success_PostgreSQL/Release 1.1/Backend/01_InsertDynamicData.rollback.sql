/*
[RayMigrator]
Description = "Delete Logins and Persons"
UseTransaction = false
*/

DO $$
BEGIN
    DELETE FROM Person WHERE LoginId BETWEEN 1 AND 9;
    DELETE FROM Login WHERE Id BETWEEN 1 AND 9;
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
