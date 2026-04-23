/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tableb (
    id INT AUTO_INCREMENT NOT NULL,
    label VARCHAR(100) NOT NULL,
    score INT NULL,
    CONSTRAINT pk_tableb PRIMARY KEY (id)
) ENGINE=InnoDB;
