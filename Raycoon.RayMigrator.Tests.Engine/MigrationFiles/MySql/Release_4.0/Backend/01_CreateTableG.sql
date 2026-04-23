/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tableg (
    id INT AUTO_INCREMENT NOT NULL,
    type VARCHAR(50) NOT NULL,
    status INT NOT NULL DEFAULT 0,
    CONSTRAINT pk_tableg PRIMARY KEY (id)
) ENGINE=InnoDB;
