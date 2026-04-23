/*
[RayMigrator]
Description = "Create instance logins (DEV)"
Environments = ["Docker"]
*/

CREATE USER IF NOT EXISTS 'login_web'@'%' IDENTIFIED BY 'DEV-30ae!';
CREATE USER IF NOT EXISTS 'login_bak'@'%' IDENTIFIED BY 'DEV-acd7!';
