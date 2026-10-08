# Exercise 2: Releases, rollback files and the way back (8 minutes)

**Goal:** ship a second release with rollback files, preview it with Validate and Simulate, apply it, roll it back with `migrate-down` and apply it again.

You work in `Workshop/exercises/bookstore/`. The migration files of this exercise are prepared in [`files/Release 1.1/Backend/`](files/Release%201.1/Backend/); you write the rollback files yourself.

## Steps

1. In `appsettings.json`, switch rollback files on:

   ```json
   "RequireRollbackFile": true,
   ```

2. Copy the folder `files/Release 1.1` from this exercise into your `Migrations/` folder. You now have:

   ```text
   Migrations/
   ├── Release 1.0/Backend/001_CreateBooks.sql
   └── Release 1.1/Backend/001_CreateAuthors.sql
                           002_AddBookAuthorFK.sql        (two blocks separated by GO)
                           003_SeedBooks.Workshop.sql     (runs only in the Workshop environment)
   ```

3. Validate without touching any database:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop -rm validate
   ```

   RayMigrator refuses to continue (exit code `1`):

   ```text
   [ERR] Migrate-Up failed for product BookStore: RayMigrator aborted due to problems parsing the migration script. RequireRollbackFile validation failed. 4 rollback file(s) are missing:
     - Release 1.0/Backend/001_CreateBooks.rollback.sql
     - Release 1.1/Backend/001_CreateAuthors.rollback.sql
     - Release 1.1/Backend/002_AddBookAuthorFK.rollback.sql
     - Release 1.1/Backend/003_SeedBooks.Workshop.rollback.sql
   ```

   The check covers every file, including the one that is already applied.

4. Write the four rollback files next to their migrations. Rollback SQL must tolerate "nothing to undo", so use `IF EXISTS`:

   `Release 1.0/Backend/001_CreateBooks.rollback.sql`
   ```sql
   /*
   [RayMigrator]
   Description = "Rollback: drop dbo.Books"
   */
   DROP TABLE IF EXISTS dbo.Books;
   ```

   `Release 1.1/Backend/001_CreateAuthors.rollback.sql`
   ```sql
   /*
   [RayMigrator]
   Description = "Rollback: drop dbo.Authors"
   */
   DROP TABLE IF EXISTS dbo.Authors;
   ```

   `Release 1.1/Backend/002_AddBookAuthorFK.rollback.sql`
   ```sql
   /*
   [RayMigrator]
   Description = "Rollback: remove the foreign key and Books.AuthorId"
   */
   ALTER TABLE dbo.Books DROP CONSTRAINT IF EXISTS FK_Books_Authors;
   GO
   ALTER TABLE dbo.Books DROP COLUMN IF EXISTS AuthorId;
   ```

   `Release 1.1/Backend/003_SeedBooks.Workshop.rollback.sql` (the environment suffix stays in the name)
   ```sql
   /*
   [RayMigrator]
   Description = "Rollback: delete the seeded books and authors"
   */
   DELETE FROM dbo.Books;
   DELETE FROM dbo.Authors;
   ```

5. Run the three run modes in order:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop -rm validate
   raymigrator migrate-up -p BookStore -env Workshop -rm simulate
   raymigrator migrate-up -p BookStore -env Workshop
   ```

   Validate lists **four** files as `[Validate] Would execute` because it never reads the repository. Simulate connects, reads the repository and lists the **three** pending files. Migrate applies them:

   ```text
   [INF] Found 3 migration files to execute for product BookStore
   [INF] Migration successful | ... | Release: Release 1.1 | ... | File: 001_CreateAuthors.sql | MigrationRecordId: 2 | SqlBlocks: 1
   [INF] Migration successful | ... | Release: Release 1.1 | ... | File: 002_AddBookAuthorFK.sql | MigrationRecordId: 3 | SqlBlocks: 2
   [INF] Migration successful | ... | Release: Release 1.1 | ... | File: 003_SeedBooks.Workshop.sql | MigrationRecordId: 4 | SqlBlocks: 2
   [INF] Migrate-Up completed successfully for product BookStore with 3 migrations
   ```

6. In SSMS: `SELECT * FROM dbo.Books;` shows two books with an `AuthorId`.

7. Go back to Release 1.0:

   ```bash
   raymigrator migrate-down -p BookStore -env Workshop -tr "Release 1.0"
   raymigrator info -p BookStore -env Workshop
   ```

   ```text
   [INF] Rolling back 3 migration(s) for product BookStore to release Release 1.0
   [INF] Rollback successful | ... | File: 003_SeedBooks.Workshop.sql | MigrationRecordId: 4 | SqlBlocks: 1
   [INF] Rollback successful | ... | File: 002_AddBookAuthorFK.sql | MigrationRecordId: 3 | SqlBlocks: 2
   [INF] Rollback successful | ... | File: 001_CreateAuthors.sql | MigrationRecordId: 2 | SqlBlocks: 1
   [INF] Migrate-Down completed for product BookStore: 3/3 rollbacks successful
   ```

   `info` now shows `Current Release: Release 1.0` and `Pending Migrations: 3`. Release 1.0 itself stays applied; `--to-release` names the release you want to **keep**. In `ray.MigrationRecord` the three files have status `50` (NotMigrated).

8. Apply Release 1.1 again:

   ```bash
   raymigrator migrate-up -p BookStore -env Workshop
   ```

## Check

`info` shows `Current Release: Release 1.1`, `Pending Migrations: 0`, `Total Executed: 4`, and the run history ends with MigrateUp Ok, MigrateDown Ok, MigrateUp Ok.

## What you have seen

- `Validate` reads files only, `Simulate` reads the repository and checks connectivity, `Migrate` writes. Run them in this order in a pipeline.
- A rollback file is `<name>.rollback.sql` next to its migration. With `RequireRollbackFile` on, a missing one stops the run before any SQL executes.
- `GO` on its own line splits a file into blocks (`SqlBlocks: 2`). Each block runs in its own transaction.
- `003_SeedBooks.Workshop.sql` runs only when `-env Workshop` is given; the TOML key `Environments = ["Workshop"]` does the same.
- `migrate-down` walks the repository records backwards, release by release, and keeps the named release.

## Behind?

Copy [`../solutions/02-releases-and-rollbacks/`](../solutions/02-releases-and-rollbacks/) over your `bookstore/` folder, run `00-setup/reset-database.sql` in SSMS, then `raymigrator migrate-up -p BookStore -env Workshop`.
