/*
[RayMigrator]
Description = "Insert sample account records"
*/

INSERT INTO [dbo].[Account] ([AccountNumber], [HolderName], [Balance], [StatusId])
VALUES
    ('ACC-001', 'Alice Wonderland', 5000.00, 1),
    ('ACC-002', 'Bob Builder', 12500.50, 1),
    ('ACC-003', 'Charlie Chaplin', 0.00, 3)
GO
