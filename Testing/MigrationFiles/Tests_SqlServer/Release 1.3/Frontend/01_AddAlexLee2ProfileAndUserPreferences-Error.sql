/*
[RayMigrator]
Description = "Create Alex Lee's 2nd UserProfile and UserPreference"
UseTransaction = false
*/

BEGIN TRANSACTION;

BEGIN TRY

	INSERT INTO [dbo].[UserProfile] (LoginId, AvatarUrl, Bio, Location, Website, JoinDate, LastActive)
	VALUES
	(11, 'https://example.com/avatars/alexlee2.jpg', 'Full-stack developer and open-source contributor', 'Seoul, South Korea', 'https://alexlee2.dev', '2022-09-18', '2023-10-01 16:55:00');

	INSERT INTO [dbo].[UserPreferences] (LoginId, Theme, NotificationsEnabled, Language)
	VALUES
	(10, 'dark', 1, 'ko-KR'); -- ERROR since LoginId already exists!

	COMMIT TRANSACTION;

END TRY
BEGIN CATCH

	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	;THROW;

END CATCH;
