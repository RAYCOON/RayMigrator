/*
[RayMigrator]
Description = "Create masterdata entries"
UseTransaction = false
*/

if NOT EXISTS (SELECT TOP(1) 1 FROM [dbo].[Sex])
BEGIN
	INSERT INTO [dbo].[Sex] ([Id], [Name]) VALUES
	(1, 'Male'),
	(2, 'Female');
END;
