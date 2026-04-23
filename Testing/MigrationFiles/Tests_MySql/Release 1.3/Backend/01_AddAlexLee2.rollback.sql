/*
[RayMigrator]
Description = "Delete Person with Sex 'Other'"
UseTransaction = false
*/

START TRANSACTION;

DELETE FROM Person WHERE LoginId = 11;
DELETE FROM Login WHERE Id = 11;

COMMIT;
