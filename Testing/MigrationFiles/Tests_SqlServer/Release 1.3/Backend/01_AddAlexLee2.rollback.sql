/*
[RayMigrator]
Description = "Delete Person with Sex 'Other'"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	DELETE FROM [dbo].[Person] WHERE [LoginId] = 11;
	DELETE FROM [dbo].[Login] WHERE [Id] = 11;

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
