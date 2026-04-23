/*
[RayMigrator]
Description = "Delete Person with Sex 'Other'"
UseTransaction = false
*/

DO $$
BEGIN
    DELETE FROM Person WHERE LoginId = 10;
    DELETE FROM Login WHERE Id = 10;
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
