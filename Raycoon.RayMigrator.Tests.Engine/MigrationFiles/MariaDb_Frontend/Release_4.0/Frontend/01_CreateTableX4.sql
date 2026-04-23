/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablex4 (
    id INT AUTO_INCREMENT NOT NULL,
    type VARCHAR(50) NOT NULL,
    status INT NOT NULL DEFAULT 0,
    CONSTRAINT pk_tablex4 PRIMARY KEY (id)
) ENGINE=InnoDB;
