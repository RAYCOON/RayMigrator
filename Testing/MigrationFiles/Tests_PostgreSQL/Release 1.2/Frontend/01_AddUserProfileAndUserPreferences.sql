/*
[RayMigrator]
Description = "Create Alex Lee's UserProfile and UserPreference"
UseTransaction = false
*/

INSERT INTO UserProfile (LoginId, AvatarUrl, Bio, Location, Website, JoinDate, LastActive)
VALUES
(10, 'https://example.com/avatars/alexlee.jpg', 'Full-stack developer and open-source contributor', 'Seoul, South Korea', 'https://alexlee.dev', '2022-09-18', '2023-10-01 16:55:00');

INSERT INTO UserPreferences (LoginId, Theme, NotificationsEnabled, Language)
VALUES
(10, 'dark', TRUE, 'ko-KR');
