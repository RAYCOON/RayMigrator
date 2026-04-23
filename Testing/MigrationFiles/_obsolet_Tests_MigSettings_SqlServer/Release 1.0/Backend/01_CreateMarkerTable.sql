CREATE TABLE [dbo].[MigSettingsMarker]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [MarkerName] VARCHAR(100) NOT NULL,
    CONSTRAINT [PK_MigSettingsMarker] PRIMARY KEY ([Id])
)
go

INSERT INTO [dbo].[MigSettingsMarker] ([MarkerName]) VALUES ('R1.0_Backend')
