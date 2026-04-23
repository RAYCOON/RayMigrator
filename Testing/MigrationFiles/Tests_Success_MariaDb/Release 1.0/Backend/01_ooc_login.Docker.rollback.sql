/*
[RayMigrator]
Description = "Drop instance logins (DEV)"
Environments = ["Docker"]
*/

DROP USER IF EXISTS 'login_web'@'%';
DROP USER IF EXISTS 'login_bak'@'%';
