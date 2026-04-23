/*
[RayMigrator]
Description = "Create instance logins (PROD)"
Environments = ["Production"]
*/

CREATE USER IF NOT EXISTS '{ENV:DB_LOGIN_WEB}'@'%' IDENTIFIED BY '{ENV:DB_PASSWORD_WEB}';
CREATE USER IF NOT EXISTS '{ENV:DB_LOGIN_BAK}'@'%' IDENTIFIED BY '{ENV:DB_PASSWORD_BAK}';
