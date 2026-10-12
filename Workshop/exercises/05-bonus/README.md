# Bonus exercises (take home)

Four tasks for after the workshop, each about 15 minutes (D takes five). They need the `bookstore/` folder in the state after exercise 4.

## A. Secrets out of the file

1. In `appsettings.Workshop.json`, replace both connection strings with the placeholder `{ENV:BOOKSTORE_CONNECTION}`.
2. Set the variable in your shell (PowerShell: `$env:BOOKSTORE_CONNECTION = "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"`, bash: `export BOOKSTORE_CONNECTION="..."`).
3. Run `raymigrator info -p BookStore -env Workshop`. It works as before, and the two `RULE_7_3` hints are gone: the rule checks the connection strings as written in the files, so a credential that arrives through a placeholder is not hardcoded. `RULE_7_1` still appears, because both placeholders name the same database. The Config Wizard shows the same picture (import both files at <https://config.raymigrator.com> to compare). With 0.16.0 the CLI still printed the two hints, because it ran the rule on the resolved values (issue #30).
4. Open a new terminal without the variable and run `info` again. RayMigrator lists both configuration paths that could not be resolved (`Could not replace setting for configuration-path [...] from environment-variable [BOOKSTORE_CONNECTION]`) and stops with `RayMigrator aborted due to startup-problems`, exit code `1`.
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

## D. A `RunAlways` file after a newer release (5 minutes, no Docker)

Since 0.16.0 a `RunAlways` file may stay in an older release. Verify it with the view from exercise 4:

1. Create `Migrations/Release 1.3/Backend/001_AddBooksPublishedYear.sql`:

   ```sql
   /*
   [RayMigrator]
   Description = "Add Books.PublishedYear"
   */
   ALTER TABLE dbo.Books ADD PublishedYear INT NULL;
   ```

   and `001_AddBooksPublishedYear.rollback.sql`:

   ```sql
   /*
   [RayMigrator]
   Description = "Rollback: drop Books.PublishedYear"
   */
   ALTER TABLE dbo.Books DROP COLUMN IF EXISTS PublishedYear;
   ```

2. Run `raymigrator migrate-up -p BookStore -env Workshop`. Two files run, in release order, and there is no out-of-order error:

   ```text
   [INF] Found 2 migration files to execute for product BookStore
   [INF] Migration successful | ... | Release: Release 1.2 | ... | File: 900_vwBooks.sql | MigrationRecordId: 7 | SqlBlocks: 1
   [INF] Migration successful | ... | Release: Release 1.3 | ... | File: 001_AddBooksPublishedYear.sql | MigrationRecordId: 8 | SqlBlocks: 1
   [INF] Migrate-Up completed successfully for product BookStore with 2 migrations
   ```

3. Run `migrate-up` again: only `900_vwBooks.sql` runs. `info` shows `Current Release: Release 1.3` and `Pending Migrations: 1`.

4. The rule: a `RunAlways` file a target has already received is exempt from the out-of-order guard, however many newer releases follow. A `RunAlways` file that is new to a target and sits in an older release counts as out of order like any other new file and needs `--allow-out-of-order` for its first run. Before 0.16.0 every later `migrate-up` would have aborted here.
