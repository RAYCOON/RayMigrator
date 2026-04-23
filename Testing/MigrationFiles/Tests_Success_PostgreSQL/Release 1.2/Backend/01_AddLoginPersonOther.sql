/*
[RayMigrator]
Description = "Add Person with Sex 'Other'"
UseTransaction = false
*/

DO $$
BEGIN
    INSERT INTO Login (Id, Username, PasswordHash, LastLogin)
    VALUES (10, 'alex.lee@example.com', 'hashed_password_5', '2023-10-01 16:00:00');

    INSERT INTO Person (LoginId, SexId, NameFirst, NameLast, DateOfBirth)
    VALUES (10, 3, 'Alex', 'Lee', '1988-09-17');
EXCEPTION WHEN OTHERS THEN
    RAISE;
END $$;
