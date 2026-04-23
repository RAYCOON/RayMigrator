/*
[RayMigrator]
Description = "Create table tablea"
Environments = ["*"]
UseTransaction = false
RunAlways = false
*/

CREATE TABLE tablea (
    id INT AUTO_INCREMENT NOT NULL,
    name VARCHAR(100) NOT NULL,
    value INT NULL,
    CONSTRAINT pk_tablea PRIMARY KEY (id)
) ENGINE=InnoDB;
