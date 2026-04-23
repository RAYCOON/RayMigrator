/*
[RayMigrator]
Description = "Delete masterdata entries in Table 'Sex'"
UseTransaction = false
*/

DELETE FROM Sex WHERE Id IN (1,2);
