/*
[RayMigrator]
Description = "Add Logins and Persons"
UseTransaction = false
*/

START TRANSACTION;

INSERT INTO Login (Id, Username, PasswordHash, LastLogin)
VALUES
(1, 'john.doe@example.com', 'hashed_password_1', '2023-10-01 08:30:00'),
(2, 'jane.smith@example.com', 'hashed_password_2', '2023-10-02 14:45:00'),
(3, 'michael.johnson@example.com', 'hashed_password_3', '2023-09-30 11:20:00'),
(4, 'emily.brown@example.com', 'hashed_password_4', '2023-10-03 09:15:00'),
(5, 'olivia.wilson@example.com', 'hashed_password_6', '2023-10-02 10:30:00'),
(6, 'david.taylor@example.com', 'hashed_password_7', '2023-09-29 13:45:00'),
(7, 'sophia.anderson@example.com', 'hashed_password_8', '2023-10-03 15:20:00'),
(8, 'william.martinez@example.com', 'hashed_password_9', '2023-10-01 12:00:00'),
(9, 'emma.garcia@example.com', 'hashed_password_10', '2023-10-02 17:30:00');

INSERT INTO Person (LoginId, SexId, NameFirst, NameLast, DateOfBirth)
VALUES
(1, 1, 'John', 'Doe', '1985-03-15'),
(2, 2, 'Jane', 'Smith', '1990-07-22'),
(3, 1, 'Michael', 'Johnson', '1978-11-30'),
(4, 2, 'Emily', 'Brown', '1992-04-08'),
(5, 2, 'Olivia', 'Wilson', '1995-01-25'),
(6, 1, 'David', 'Taylor', '1982-06-11'),
(7, 2, 'Sophia', 'Anderson', '1993-12-03'),
(8, 1, 'William', 'Martinez', '1980-08-19'),
(9, 2, 'Emma', 'Garcia', '1987-02-28');

COMMIT;
