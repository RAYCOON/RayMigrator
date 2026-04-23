/*
[RayMigrator]
Description = "Add Person with Sex 'Other'"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	SET IDENTITY_INSERT [dbo].[login] ON;

	-- Insert data into Login table
	INSERT INTO [dbo].[Login] ([Id], [Username], [PasswordHash], [LastLogin])
	VALUES
		(11, 'alex.lee2@example.com', 'hashed_password_5', '2023-10-01 16:00:00');

	SET IDENTITY_INSERT [dbo].[login] OFF;


	-- Insert data into Person table
	INSERT INTO [dbo].[Person] ([LoginId], [SexId], [NameFirst], [NameLast], [DateOfBirth])
	VALUES
		(11, 3, 'Alex', 'Lee2', '1988-09-17');

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
