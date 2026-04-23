/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tabley3 (
    id INT AUTO_INCREMENT NOT NULL,
    tag VARCHAR(50) NOT NULL,
    priority INT NULL,
    CONSTRAINT pk_tabley3 PRIMARY KEY (id)
) ENGINE=InnoDB;
