/*
[RayMigrator]
Description = "Delete UserProfiles and UserPreferences"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	DELETE FROM [UserPreferences]
	WHERE LoginId IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

	DELETE FROM [UserProfile]
	WHERE LoginId IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
