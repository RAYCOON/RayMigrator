# Bonus exercises (take home)

Three tasks for after the workshop, each about 15 minutes. They need the `bookstore/` folder in the state after exercise 4.

## A. Secrets out of the file

1. In `appsettings.Workshop.json`, replace both connection strings with the placeholder `{ENV:BOOKSTORE_CONNECTION}`.
2. Set the variable in your shell (PowerShell: `$env:BOOKSTORE_CONNECTION = "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"`, bash: `export BOOKSTORE_CONNECTION="..."`).
3. Run `raymigrator info -p BookStore -env Workshop`. The `RULE_7_3` warnings are gone.
4. Open a new terminal without the variable and run `info` again. RayMigrator lists the unresolved placeholder and stops with exit code `1`.
5. Read [Docs/06-configuration-reference/environment-variables.md](../../../Docs/06-configuration-reference/environment-variables.md): placeholders work in configuration values, in CLI option values and inside migration SQL, each with its own rule for missing variables.

## B. A second target group on PostgreSQL (needs Docker)

1. Start the example PostgreSQL container: `Examples/Docker/run-docker-postgresql.sh` (see [Examples/README.md](../../../Examples/README.md)).
2. Add a target group `Reporting` with `"DatabaseType": "PostgreSQL"` and one target to the `BookStore` product in both `appsettings.json` (structure) and `appsettings.Workshop.json` (connection string). The repository stays on SQL Server.
3. Create `Migrations/Release 1.3/Reporting/001_CreateBookStats.sql` with PostgreSQL SQL. Blocks are separated by a `;` **alone on a line**; a `;` at the end of a statement does not split.
4. Add `Release 1.3/Backend/001_AddBooksPublishedYear.sql` with a rollback file, then run Validate, Simulate, Migrate. Watch the order: release, then target group, then files.
5. Set `TargetGroupMigrationOrder = ["Reporting", "Backend"]` in `Release 1.3/migsettings.txt` and run `-rm simulate` again. Reference: [Docs/user-manual/10-advanced-features.md](../../../Docs/user-manual/10-advanced-features.md).

## C. Let sqlcmd run the files (needs Docker)

1. Read [Docs/06-configuration-reference/cli-tools-options.md](../../../Docs/06-configuration-reference/cli-tools-options.md) and the example [Docs/examples/appsettings.docker-cli.json](../../../Docs/examples/appsettings.docker-cli.json).
2. Define a `CliTools` entry that runs `docker exec -i <container> /opt/mssql-tools18/bin/sqlcmd ... -b` in `Stdin` mode and select it with `UseCliToolAlias` on the `Backend` target group.
3. Run `-rm simulate`, then `migrate-up`. Files executed by a CLI tool count as one block, and `UseTransaction` has no effect on them.
