# Model solutions

Each folder holds the complete content of `Workshop/exercises/bookstore/` **after** the exercise of the same number: the tracked `appsettings.json` and the whole `Migrations/` tree. Your personal `appsettings.Workshop.json` is not part of a solution; keep yours.

To catch up with a solution:

1. In SSMS, run `../00-setup/reset-database.sql` (replace `<NAME>` first). This drops and recreates your database.
2. Delete your `bookstore/Migrations/` folder, then copy `appsettings.json` and `Migrations/` from the solution folder into `bookstore/`.
3. Run `raymigrator migrate-up -p BookStore -env Workshop`. Your database is now in the state the next exercise expects.

| Folder | State after |
|---|---|
| `01-first-migration` | Release 1.0 with `001_CreateBooks.sql`; rollback files not yet required |
| `02-releases-and-rollbacks` | Release 1.1 added, every file has a rollback file, `RequireRollbackFile` is `true` |
| `03-hash-validation` | Same as 02, `001_CreateBooks.sql` carries the reviewed comment (its hash was accepted with `update-hash`) |
| `04-error-handling` | Release 1.2 with Categories, the corrected seed file, the `RunAlways` view and `migsettings.txt` |
