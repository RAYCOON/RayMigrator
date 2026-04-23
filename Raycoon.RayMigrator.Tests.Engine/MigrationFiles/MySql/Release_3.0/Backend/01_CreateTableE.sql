/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablee (
    id INT AUTO_INCREMENT NOT NULL,
    ref VARCHAR(50) NOT NULL,
    data TEXT NULL,
    CONSTRAINT pk_tablee PRIMARY KEY (id)
) ENGINE=InnoDB;
