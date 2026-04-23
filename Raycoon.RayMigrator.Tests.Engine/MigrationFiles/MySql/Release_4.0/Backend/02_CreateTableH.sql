/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tableh (
    id INT AUTO_INCREMENT NOT NULL,
    category VARCHAR(100) NOT NULL,
    weight DECIMAL(10,2) NULL,
    CONSTRAINT pk_tableh PRIMARY KEY (id)
) ENGINE=InnoDB;
