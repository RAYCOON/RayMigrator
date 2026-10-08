# Exercise 3: Who changed that file? (5 minutes)

**Goal:** see how RayMigrator detects a changed migration file and how you accept an intended change.

You work in `Workshop/exercises/bookstore/`.

## Steps

1. Append a comment to the already applied file `Migrations/Release 1.0/Backend/001_CreateBooks.sql`, for example as the last line:

   ```sql
   -- Reviewed during the workshop
   ```

2. Check the stored hashes against your files:

   ```bash
   raymigrator validate-hash -p BookStore -env Workshop
   ```

   ```text
   [INF] Validate-Hash completed. Total: 4, Valid: 3, Invalid: 1, Missing: 0
   [WRN] Hash issue: 001_CreateBooks.sql - Modified: Hash mismatch detected for file in Release: Release 1.0, TargetGroup: Backend, Target(s): MainDB (Scope: File)
   ```

   Exit code `1`. This is the command a pipeline runs before `migrate-up`; a modified or missing file fails the build.

3. **Do not run `migrate-up` now.** Think about what it would do, then look at the answer below.

4. Accept the change:

   ```bash
   raymigrator update-hash -p BookStore -env Workshop
   ```

   ```text
   [INF] Updating hashes for migration 001_CreateBooks.sql (Release: Release 1.0, TargetGroup: Backend) on target(s) [MainDB]
   [INF] Update-Hash completed. Updated: 1 file(s) / 1 record(s), New: 0, Removed: 0
   ```

5. Validate again: `Total: 4, Valid: 4, Invalid: 0, Missing: 0`, exit code `0`.

## Check

`validate-hash` exits with `0`.

## The answer to step 3

`migrate-up` does **not** refuse a modified file. It warns and schedules the file to run again from block 1:

```text
[WRN] Migration file 001_CreateBooks.sql has changed since it was executed on target MainDB (hash mismatch, scope: File). Re-executing on that target.
```

In this workshop the run then stops for a second reason: Release 1.1 is already applied, so a pending file from Release 1.0 is out of order:

```text
[ERR] Out-of-order migrations detected but --allow-out-of-order not specified. 1 file(s) from releases before 'Release 1.1' not applied. Aborting.
```

Had you passed `--allow-out-of-order`, `CREATE TABLE dbo.Books` would have run again and failed. Two lessons: never edit an applied migration file without `update-hash`, and let `validate-hash` guard your pipeline.

## What you have seen

- Three SHA-256 hashes per file (whole file, TOML header, SQL blocks) are stored in `ray.MigrationRecord`. `HashValidationScope` per target group decides which one `migrate-up` compares: `File` (default), `SqlBlocks` (header edits allowed) or `Disabled`.
- `validate-hash` is read-only and exits `1` on Modified or Missing files; `update-hash` rewrites the stored hashes after a review.
- Files from a release older than the highest applied release abort the run unless `--allow-out-of-order` is given.

## Behind?

Copy [`../solutions/03-hash-validation/`](../solutions/03-hash-validation/) over your `bookstore/` folder, run `00-setup/reset-database.sql` in SSMS, then `raymigrator migrate-up -p BookStore -env Workshop`.
