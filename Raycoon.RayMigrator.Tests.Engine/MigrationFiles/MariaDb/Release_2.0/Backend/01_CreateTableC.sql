/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablec (
    id INT AUTO_INCREMENT NOT NULL,
    title VARCHAR(100) NOT NULL,
    amount DECIMAL(10,2) NULL,
    CONSTRAINT pk_tablec PRIMARY KEY (id)
) ENGINE=InnoDB;
