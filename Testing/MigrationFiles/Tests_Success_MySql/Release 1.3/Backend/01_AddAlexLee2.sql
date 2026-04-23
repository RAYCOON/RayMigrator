/*
[RayMigrator]
Description = "Add Person with Sex 'Other'"
UseTransaction = false
*/

START TRANSACTION;

INSERT INTO Login (Id, Username, PasswordHash, LastLogin)
VALUES (11, 'alex.lee2@example.com', 'hashed_password_5', '2023-10-01 16:00:00');

INSERT INTO Person (LoginId, SexId, NameFirst, NameLast, DateOfBirth)
VALUES (11, 3, 'Alex', 'Lee2', '1988-09-17');

COMMIT;
