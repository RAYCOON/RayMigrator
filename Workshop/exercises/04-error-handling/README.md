# Exercise 4: When a migration fails (10 minutes)

**Goal:** break a release on purpose, read what RayMigrator tells you, compare the error actions `Terminate` and `Rollback`, repair, and see a `RunAlways` file at work.

You work in `Workshop/exercises/bookstore/`. The files for Release 1.2 are prepared in [`files/Release 1.2/Backend/`](files/Release%201.2/Backend/) together with their rollback files; `002_SeedCategories.sql` contains a deliberate bug in its third block.

## Steps

1. Copy the folder `files/Release 1.2` from this exercise into your `Migrations/` folder:

   ```text
   Migrations/Release 1.2/Backend/001_CreateCategories.sql        (+ .rollback.sql)
                                  002_SeedCategories.sql          (+ .rollback.sql)  three GO blocks, block 3 inserts a duplicate key
                                  900_vwBooks.sql                 (+ .rollback.sql)  RunAlways = true
   ```

2. Run with the default error action `Terminate`:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop
   ```

   Expected (abridged; the stack trace in the middle is normal):

   ```text
   [INF] Found 3 migration files to execute for product BookStore
   [WRN] [Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION] 900_vwBooks.sql has RunAlways=true but HashValidationScope=File on TargetGroup 'Backend'. ...
   [INF] Migration successful | ... | Release: Release 1.2 | ... | File: 001_CreateCategories.sql | MigrationRecordId: 5 | SqlBlocks: 1
   [ERR] Migration FAILED | ... | Release: Release 1.2 | ... | File: 002_SeedCategories.sql | MigrationRecordId: 6 | SqlBlock: 3/3
   Microsoft.Data.SqlClient.SqlException (0x80131904): Violation of PRIMARY KEY constraint 'PK_Categories'. Cannot insert duplicate key in object 'dbo.Categories'. The duplicate key value is (1).
   ...
   [FTL] MigrationErrorAction=Terminate: No rollback will be performed. Database may be in unclear state.
   [ERR] Migrate-Up failed for product BookStore: Violation of PRIMARY KEY constraint 'PK_Categories'. ...
   ```

   Exit code `1`. `900_vwBooks.sql` never ran because the run stopped at the failure.

3. Inspect the damage. `info` shows the last run with result `Error`. In SSMS:

   ```sql
   SELECT * FROM dbo.Categories;                                           -- exists, but 0 rows
   SELECT Filename, MigrationStatusId, FileUpBlocksMigrated, FileUpBlocksTotal
   FROM ray.MigrationRecord WHERE ReleaseVersion = 'Release 1.2';
   ```

   | Filename | MigrationStatusId | FileUpBlocksMigrated | FileUpBlocksTotal |
   |---|---|---|---|
   | 001_CreateCategories.sql | 100 (Migrated) | 1 | 1 |
   | 002_SeedCategories.sql | 30 (Failed) | 0 | 3 |

   Blocks 1 and 2 had inserted `Fiction` and `Science`, yet the table is empty. Because repository and target share one database, RayMigrator ran all blocks of the file and the repository update in **one** transaction and rolled everything back (the "atomic shared connection"). With a separate repository database, each block commits on its own: the table would hold two rows, `FileUpBlocksMigrated` would be `2`, and the next run would resume at block 3.

4. Clean up what the terminated run left behind by going back to the last good release:

   ```bash
   raymigrator migrate-down -p BookStore -env Workshop -tr "Release 1.1"
   ```

   ```text
   [INF] Rolling back 1 migration(s) for product BookStore to release Release 1.1
   [INF] Rollback successful | ... | File: 001_CreateCategories.sql | MigrationRecordId: 5 | SqlBlocks: 1
   ```

   `dbo.Categories` is gone. The failed record of `002` is left alone; `migrate-down` only touches applied files.

5. Let RayMigrator roll back on its own next time. Copy [`files/migsettings.txt`](files/migsettings.txt) to `Migrations/Release 1.2/migsettings.txt`:

   ```toml
   [RayMigrator]
   MigrationErrorAction = "Rollback"
   ```

   A `migsettings.txt` sets defaults for every migration file below its folder, here for Release 1.2 only. Run again:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop
   ```

   ```text
   [INF] Migration successful | ... | File: 001_CreateCategories.sql | MigrationRecordId: 5 | SqlBlocks: 1
   [ERR] Migration FAILED | ... | File: 002_SeedCategories.sql | MigrationRecordId: 6 | SqlBlock: 3/3
   ...
   [INF] MigrationErrorAction=Rollback: Rolling back failed migration and 1 previously successful migration(s).
   [INF] Rollback successful | ... | File: 002_SeedCategories.sql | MigrationRecordId: 6 | SqlBlocks: 1
   [INF] Rollback successful | ... | File: 001_CreateCategories.sql | MigrationRecordId: 5 | SqlBlocks: 1
   [ERR] Migrate-Up failed for product BookStore: Violation of PRIMARY KEY constraint 'PK_Categories'. ...
   ```

   Exit code is still `1`, but `info` now shows the result `Recovered`: the database is back where the run started. Note that the rollback only covers files applied **in this run**; that is why step 4 was needed.

6. Repair the bug. In `002_SeedCategories.sql` change the last insert from `(1, N'History')` to `(3, N'History')`, then:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop
   ```

   All three files succeed, result `Ok`, exit code `0`. The view `dbo.vwBooks` exists now.

7. Run `migrate-up` once more. Only `900_vwBooks.sql` runs again: `RunAlways = true` re-creates the view on every run. `info` reports `Pending Migrations: 1` from now on; a RunAlways file is always pending by design.

8. Look at the repair tool for crashed runs:

   ```bash
   raymigrator fix -p BookStore -env Workshop -rm simulate
   ```

   ```text
   [INF] Found 0 orphaned run(s), 0 matching --older-than 60 minutes
   [INF] Simulate mode: no changes applied
   ```

## Check

`info` shows `Current Release: Release 1.2`, and the run history reads (newest first) MigrateUp Ok, MigrateUp Ok, MigrateUp **Recovered**, MigrateDown Ok, MigrateUp **Error**.

## What you have seen

- A file is executed block by block (`SqlBlock: 3/3`). Each block has its own transaction, unless repository and target share the database, in which case the whole file is atomic.
- `MigrationErrorAction` decides what happens after a failure: `Terminate` (stop, nothing undone), `Rollback` (undo this run), `RollbackErrorOnly`, `RollbackRelease`, `Ignore` (skip the file, continue, result `PartialSuccess`).
- Run results: `Ok` (100), `PartialSuccess` (50), `Recovered` (80), `Error` (90). Every result except `Ok` exits with `1`, so pipelines must read `info` or the repository to tell them apart.
- `migsettings.txt` files set defaults per folder; the TOML header of a file wins over them, they win over `appsettings.json`.
- Failed files are retried automatically on the next run. `fix` repairs runs that crashed without finishing (orphaned runs); runs older than ten minutes are repaired automatically by the next command.

## Behind?

Copy [`../solutions/04-error-handling/`](../solutions/04-error-handling/) over your `bookstore/` folder, run `00-setup/reset-database.sql` in SSMS, then `raymigrator migrate-up -p BookStore -env Workshop`.
