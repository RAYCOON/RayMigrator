/*
[RayMigrator]
Description = "Delete Alex Lee's 2nd UserProfile and UserPreference"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	DELETE FROM [dbo].[UserPreferences] WHERE LoginId = 11;
	DELETE FROM [dbo].[UserProfile] WHERE LoginId = 11;

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
