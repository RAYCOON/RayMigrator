/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tabled (
    id INT AUTO_INCREMENT NOT NULL,
    code VARCHAR(50) NOT NULL,
    description TEXT NULL,
    CONSTRAINT pk_tabled PRIMARY KEY (id)
) ENGINE=InnoDB;
