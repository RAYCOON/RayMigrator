/*
[RayMigrator]
Description = "Create Alex Lee's UserProfile and UserPreference"
UseTransaction = false
*/

-- Insert test data into UserProfile
INSERT INTO [dbo].[UserProfile] (LoginId, AvatarUrl, Bio, Location, Website, JoinDate, LastActive)
VALUES
(10, 'https://example.com/avatars/alexlee.jpg', 'Full-stack developer and open-source contributor', 'Seoul, South Korea', 'https://alexlee.dev', '2022-09-18', '2023-10-01 16:55:00')
GO

-- Insert test data into UserPreferences
INSERT INTO [dbo].[UserPreferences] (LoginId, Theme, NotificationsEnabled, Language)
VALUES
(10, 'dark', 1, 'ko-KR')
GO