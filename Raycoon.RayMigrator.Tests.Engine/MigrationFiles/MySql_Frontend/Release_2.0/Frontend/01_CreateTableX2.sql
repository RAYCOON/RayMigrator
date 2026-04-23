/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablex2 (
    id INT AUTO_INCREMENT NOT NULL,
    title VARCHAR(100) NOT NULL,
    amount DECIMAL(10,2) NULL,
    CONSTRAINT pk_tablex2 PRIMARY KEY (id)
) ENGINE=InnoDB;
