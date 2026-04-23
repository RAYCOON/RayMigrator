#!/bin/bash
set -e
set +m

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

echo "Starting SQL Server..."

# Make sure this file was saved using linux line endings (LF) if you have problems executing this file!
# Do not save this file using Windows line endings (CR + LF)!!! This will cause execution errors.

if [ "$1" = '/opt/mssql/bin/sqlservr' ]; then
  if [ ! -f /tmp/app-initialized ]; then

    function initialize_app_database() {
      # Wait for SQL Server to complete start up
      timeout=100
      while ! $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -Q "SELECT 1" &>/dev/null; do
        sleep 3
        timeout=$((timeout - 3))
        if [ $timeout -le 0 ]; then
          echo "SQL Server startup timed out"
          exit 1
        fi
      done

      # Wait for finishing the master database setup process
      echo "Waiting 10 sec. to complete DB startup process..."
      sleep 10
      echo "START: Executing database scripts..."

      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/10_create_logins.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/20_create_db_RayMigratorRepository.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/21_create_db_SimpleApplicationDB.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/22_create_db_BackendDB.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/90_create_user.sql

      echo "END: Database scripts successfully executed."
      echo ""
      echo "END: Database creation completed."

      touch /tmp/app-initialized
    }

    initialize_app_database &
  fi
fi

set -m
exec "$@"
