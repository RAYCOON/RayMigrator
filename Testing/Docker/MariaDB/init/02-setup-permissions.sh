#!/bin/bash
set -e

echo "Starting MariaDB setup script..."

# No password is required during initialization
echo "MariaDB is ready. Running additional setup steps..."

# Create additional users or apply further configuration
# No password required during initialization
mariadb -u root -p"${MYSQL_ROOT_PASSWORD}" <<-EOSQL
    -- Create a read-only user
    CREATE USER IF NOT EXISTS 'readonly'@'%' IDENTIFIED BY 'readonly123';
    GRANT SELECT ON ${MYSQL_DATABASE}.* TO 'readonly'@'%';
    
    -- Create a backup user
    CREATE USER IF NOT EXISTS 'backup'@'%' IDENTIFIED BY 'backup123';
    GRANT SELECT, LOCK TABLES, SHOW VIEW, EVENT, TRIGGER ON ${MYSQL_DATABASE}.* TO 'backup'@'%';
    
    -- Reload privileges
    FLUSH PRIVILEGES;
    
    -- Show all users
    SELECT User, Host FROM mysql.user;
EOSQL

echo "MariaDB setup completed!"

# Create a status file
touch /tmp/mariadb-setup-complete