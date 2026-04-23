/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablex1 (
    id INT AUTO_INCREMENT NOT NULL,
    label VARCHAR(100) NOT NULL,
    active TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT pk_tablex1 PRIMARY KEY (id)
) ENGINE=InnoDB;
