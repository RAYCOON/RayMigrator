/*
[RayMigrator]
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablex3 (
    id INT AUTO_INCREMENT NOT NULL,
    ref VARCHAR(50) NOT NULL,
    data TEXT NULL,
    CONSTRAINT pk_tablex3 PRIMARY KEY (id)
) ENGINE=InnoDB;
