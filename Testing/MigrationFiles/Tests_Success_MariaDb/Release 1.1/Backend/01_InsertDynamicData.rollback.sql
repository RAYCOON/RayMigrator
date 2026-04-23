/*
[RayMigrator]
Description = "Delete Logins and Persons"
UseTransaction = false
*/

START TRANSACTION;

DELETE FROM Person WHERE LoginId BETWEEN 1 AND 9;
DELETE FROM Login WHERE Id BETWEEN 1 AND 9;

COMMIT;
