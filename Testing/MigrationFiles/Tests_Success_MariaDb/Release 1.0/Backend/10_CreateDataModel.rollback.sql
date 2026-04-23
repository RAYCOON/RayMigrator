/*
[RayMigrator]
Description = "Drop initial model"
UseTransaction = false
*/

ALTER TABLE Person DROP FOREIGN KEY Login_Person;

ALTER TABLE Person DROP FOREIGN KEY Sex_Person;

DROP TABLE Person;

DROP TABLE Login;

DROP TABLE Sex;
