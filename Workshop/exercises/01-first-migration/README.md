# Exercise 1: Your first migration (7 minutes)

**Goal:** apply one SQL file to your database with RayMigrator and see what it records.

You work in `Workshop/exercises/bookstore/`. Behind? Jump to the end of this page.

## Steps

1. Create the folder `Migrations/Release 1.0/Backend/`. The release folder name becomes the release version, the `Backend` folder name must match the target group alias in `appsettings.json`.

2. Create `Migrations/Release 1.0/Backend/001_CreateBooks.sql`:

   ```sql
   /*
   [RayMigrator]
   Description = "Create table dbo.Books"
   */
   CREATE TABLE dbo.Books
   (
       Id    INT           NOT NULL CONSTRAINT PK_Books PRIMARY KEY,
       Title NVARCHAR(200) NOT NULL
   );
   ```

   The comment at the top is the optional TOML header. Everything else is plain T-SQL.

3. Apply it:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop
   ```

   Expected (abridged, timestamps removed):

   ```text
   [INF] Executing Migrate-Up for product BookStore in environment Workshop
   [INF] Found 1 migration files to execute for product BookStore
   [INF] Migration successful | Product: BookStore | Env: Workshop | Release: Release 1.0 | TargetGroup: Backend | Target: MainDB | File: 001_CreateBooks.sql | MigrationRecordId: 1 | SqlBlocks: 1
   [INF] Migrate-Up completed successfully for product BookStore with 1 migrations
   ```

   Exit code `0`. On this first run RayMigrator also created the `ray` schema with its eleven repository tables; that happens silently at Information level.

4. Ask for the status:

   ```bash
   raymigrator info -p BookStore -env Workshop
   ```

   ```text
   --- Migration Status for product BookStore, environment Workshop ---
     Current Release:     Release 1.0
     Pending Migrations:  0
     Total Executed:      1
   --- Target Groups ---
     [Backend] Type=SqlServer, Release=Release 1.0, Executed=1, Targets=[MainDB]

   --- Last 1 Migration Runs ---
       RunId  Operation     RunMode     Result     # Migrations  StartedAt            FinishedAt           DurationInMs
     ───────  ────────────  ──────────  ─────────  ────────────  ───────────────────  ───────────────────  ────────────
           1  MigrateUp     Migrate     Ok                    1  2026-10-07 15:58:03  2026-10-07 15:58:03            59
   ```

5. Look into the database in SSMS:

   ```sql
   SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('ray') ORDER BY name;   -- 11 tables
   SELECT Filename, ReleaseVersion, TargetAlias, MigrationStatusId, FileUpBlocksMigrated, FileUpBlocksTotal
   FROM ray.MigrationRecord;                                                        -- one row, status 100 = Migrated
   SELECT * FROM dbo.Books;                                                         -- your table, still empty
   ```

6. Run `migrate-up` a second time:

   ```text
   [INF] All migration files already applied for product BookStore
   ```

   Nothing happens twice. The repository knows what has been applied where.

## Check

`info` shows `Current Release: Release 1.0` and `Total Executed: 1`.

## What you have seen

- A migration is a SQL file in `{Root}/{Release}/{TargetGroup}/`; the numeric prefix is only a sort key.
- `migrate-up` applies every pending file once and records it in the `ray` schema.
- `info` and the `ray.*` tables are the audit trail.
- The warnings `RULE_7_3` (literal password) and `RULE_7_1` (repository and target in the same database) are configuration hints, not errors. Production setups use `{ENV:VARIABLE}` placeholders and a separate repository database.

## Behind?

Copy the content of [`../solutions/01-first-migration/`](../solutions/01-first-migration/) over your `bookstore/` folder (it contains `appsettings.json` and the `Migrations/` tree), run `00-setup/reset-database.sql` in SSMS if your database is in an unclear state, then run `raymigrator migrate-up -p BookStore -env Workshop`.
