/*
[RayMigrator]
Description = "Delete Logins and Persons"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	-- L�schen der Eintr�ge aus der Person-Tabelle
	DELETE FROM [Person] WHERE [LoginId] BETWEEN 1 AND 9;

	-- L�schen der Eintr�ge aus der Login-Tabelle
	DELETE FROM [Login] WHERE [Id] BETWEEN 1 AND 9;

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
