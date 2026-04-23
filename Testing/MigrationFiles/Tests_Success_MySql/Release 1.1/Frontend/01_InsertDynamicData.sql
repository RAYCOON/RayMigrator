/*
[RayMigrator]
Description = "Create UserProfiles and UserPreferences"
*/

INSERT INTO UserProfile (LoginId, AvatarUrl, Bio, Location, Website, JoinDate, LastActive)
VALUES
(1, 'https://example.com/avatars/johndoe.jpg', 'Software developer and coffee enthusiast', 'Berlin, Germany', 'https://johndoe.com', '2022-01-15', '2023-10-05 14:30:00'),
(2, 'https://example.com/avatars/janesmith.jpg', 'Digital marketer with a passion for creativity', 'London, UK', 'https://janesmith.io', '2022-03-22', '2023-10-04 09:45:00'),
(3, 'https://example.com/avatars/mikejohnson.jpg', 'Data scientist and machine learning expert', 'San Francisco, USA', 'https://mikejohnson.ai', '2022-05-10', '2023-10-03 18:20:00'),
(4, NULL, 'UX designer focused on creating intuitive interfaces', 'Toronto, Canada', NULL, '2022-07-01', '2023-10-02 11:10:00'),
(5, 'https://example.com/avatars/oliviawilson.jpg', 'Product manager with a knack for innovation', 'Stockholm, Sweden', 'https://oliviawilson.pm', '2022-11-30', '2023-09-30 13:15:00'),
(6, 'https://example.com/avatars/davidtaylor.jpg', 'Cybersecurity specialist and ethical hacker', 'Tel Aviv, Israel', 'https://securedavid.net', '2023-01-20', '2023-09-29 10:40:00'),
(7, NULL, 'AI researcher focusing on natural language processing', 'Zurich, Switzerland', NULL, '2023-03-15', '2023-09-28 17:25:00'),
(8, 'https://example.com/avatars/williammartinez.jpg', 'Mobile app developer specializing in AR', 'Barcelona, Spain', 'https://williammartinez.app', '2023-05-05', '2023-09-27 14:50:00'),
(9, 'https://example.com/avatars/emmagarcia.jpg', 'DevOps engineer and cloud architecture expert', 'Melbourne, Australia', 'https://emmagarcia.cloud', '2023-07-12', '2023-09-26 09:30:00');

INSERT INTO UserPreferences (LoginId, Theme, NotificationsEnabled, Language)
VALUES
(1, 'dark', 1, 'en-US'),
(2, 'light', 1, 'en-GB'),
(3, 'auto', 1, 'en-US'),
(4, 'light', 0, 'en-CA'),
(5, 'auto', 1, 'sv-SE'),
(6, 'dark', 1, 'he-IL'),
(7, 'light', 0, 'de-CH'),
(8, 'auto', 1, 'es-ES'),
(9, 'dark', 1, 'en-AU');
