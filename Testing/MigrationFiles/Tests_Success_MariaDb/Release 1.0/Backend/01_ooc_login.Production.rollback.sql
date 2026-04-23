/*
[RayMigrator]
Description = "Drop instance logins (PROD)"
Environments = ["Production"]
*/

DROP USER IF EXISTS '{ENV:DB_LOGIN_WEB}'@'%';
DROP USER IF EXISTS '{ENV:DB_LOGIN_BAK}'@'%';
