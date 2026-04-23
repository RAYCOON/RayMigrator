CREATE TABLE MigSettingsMarker
(
    Id INT AUTO_INCREMENT NOT NULL,
    MarkerName VARCHAR(100) NOT NULL,
    CONSTRAINT PK_MigSettingsMarker PRIMARY KEY (Id)
) ENGINE=InnoDB;

INSERT INTO MigSettingsMarker (MarkerName) VALUES ('R1.0_Backend');
