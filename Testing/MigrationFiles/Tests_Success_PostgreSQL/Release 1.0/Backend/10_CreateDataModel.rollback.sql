/*
[RayMigrator]
Description = "Drop initial model"
UseTransaction = false
*/

ALTER TABLE Person DROP CONSTRAINT Login_Person;

ALTER TABLE Person DROP CONSTRAINT Sex_Person;

DROP TABLE Person;

DROP TABLE Login;

DROP TABLE Sex;
