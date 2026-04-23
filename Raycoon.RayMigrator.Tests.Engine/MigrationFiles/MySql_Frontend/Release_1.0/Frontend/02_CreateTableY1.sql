/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tabley1 (
    id INT AUTO_INCREMENT NOT NULL,
    description TEXT NULL,
    weight DECIMAL(10,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT pk_tabley1 PRIMARY KEY (id)
) ENGINE=InnoDB;
