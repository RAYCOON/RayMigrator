/*
[RayMigrator]
Description = "Delete Person with Sex 'Other'"
UseTransaction = false
*/

DO $$
BEGIN
    DELETE FROM Person WHERE LoginId = 11;
    DELETE FROM Login WHERE Id = 11;
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
