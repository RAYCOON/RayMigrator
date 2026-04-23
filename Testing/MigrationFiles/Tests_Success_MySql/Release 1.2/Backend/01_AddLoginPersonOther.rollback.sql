/*
[RayMigrator]
Description = "Delete Person with Sex 'Other'"
UseTransaction = false
*/

START TRANSACTION;

DELETE FROM Person WHERE LoginId = 10;
DELETE FROM Login WHERE Id = 10;

COMMIT;
