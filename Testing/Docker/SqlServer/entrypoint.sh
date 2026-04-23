#!/bin/bash
set -e
set +m

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

echo "Starting SQL Server..."

# Make sure this file was saved using linux line endings (LF) if you have problems executing this file!
# Do not save this file using Windows line endings (CR + LF)!!! This will cause execution errors.

if [ "$1" = '/opt/mssql/bin/sqlservr' ]; then
  # If this is the container's first run, initialize the application database
  if [ ! -f /tmp/app-initialized ]; then
    
    # Initialize the application database asynchronously in a background process. This allows a) the SQL Server process to be the main process in the container, which allows graceful shutdown and other goodies, and b) us to only start the SQL Server process once, as opposed to starting, stopping, then starting it again.
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

      # Wait for finishing the master database setup process to obtain the correct collation in all subsequent user databases
      echo "Waiting 10 sec. to complete DB startup process..."
      sleep 10
      echo "START: Executing database scripts..."

      #run the setup script to create the DB and the schema in the DB
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/10_create_logins.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/21_create_db_Backend_1.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/22_create_db_Backend_2.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/23_create_db_Frontend.sql
      $SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d master -i ./sql-scripts/90_create_user.sql

      # sa login change
      #/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "P@ssw0rd!" -d master -Q "ALTER LOGIN sa WITH NAME = securesa;"

      echo "END: Database scripts successfully executed."
      echo ""
      echo "END: Database creation completed."

      # Note that the container has been initialized so future starts won't wipe changes to the data
      touch /tmp/app-initialized
    }

    initialize_app_database &
  fi
fi

set -m
exec "$@"
