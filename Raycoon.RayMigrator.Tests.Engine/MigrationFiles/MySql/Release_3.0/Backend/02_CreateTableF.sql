/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablef (
    id INT AUTO_INCREMENT NOT NULL,
    tag VARCHAR(50) NOT NULL,
    priority INT NULL,
    CONSTRAINT pk_tablef PRIMARY KEY (id)
) ENGINE=InnoDB;
