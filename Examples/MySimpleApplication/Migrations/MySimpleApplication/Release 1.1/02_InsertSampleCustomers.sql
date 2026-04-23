/*
[RayMigrator]
Description = "Insert sample customer records"
*/

INSERT INTO [dbo].[Customer] ([FirstName], [LastName], [Phone], [Email])
VALUES
    ('John', 'Doe', '+1-555-0101', 'john.doe@example.com'),
    ('Jane', 'Smith', '+1-555-0102', 'jane.smith@example.com'),
    ('Bob', 'Johnson', NULL, 'bob.j@example.com')
GO
