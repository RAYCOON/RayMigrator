/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tabley4 (
    id INT AUTO_INCREMENT NOT NULL,
    category VARCHAR(100) NOT NULL,
    score DECIMAL(10,2) NULL,
    CONSTRAINT pk_tabley4 PRIMARY KEY (id)
) ENGINE=InnoDB;
