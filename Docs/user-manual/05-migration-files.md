# Writing Migration Files

This chapter continues the BookStore tutorial by explaining how to write migration files, structure them in directories, and handle rollbacks. You will learn the TOML metadata format, naming conventions, dialect differences, and best practices.

By the end of this chapter you will understand:

- The anatomy of a migration file
- Every TOML metadata parameter
- File naming and directory structure rules
- How to write rollback files
- SQL dialect differences across supported databases

---

## File Anatomy

A migration file is just SQL. The simplest valid migration file has no header at all:

```sql
CREATE TABLE [dbo].[Authors]
(
    [Id]        INT IDENTITY(1,1) NOT NULL,
    [Name]      NVARCHAR(200) NOT NULL,
    [Country]   NVARCHAR(100) NULL,
    CONSTRAINT [PK_Authors] PRIMARY KEY ([Id])
);
```

This file runs in all environments, applies to all targets, wraps execution in a transaction, and executes only once — the sensible defaults.

Optionally, you can add a **TOML metadata header** inside a `/* ... */` block comment at the top of the file. Use it to provide a description or to override a default:

```sql
/*
[RayMigrator]
Description = "Create Authors table"
*/

CREATE TABLE [dbo].[Authors]
(
    [Id]        INT IDENTITY(1,1) NOT NULL,
    [Name]      NVARCHAR(200) NOT NULL,
    [Country]   NVARCHAR(100) NULL,
    CONSTRAINT [PK_Authors] PRIMARY KEY ([Id])
);
```

**Key rules:**

- The TOML header is optional. Without it, all defaults apply.
- When present, the header must be inside a `/* ... */` block comment at the top of the file.
- The `[RayMigrator]` section marker is required inside the header.
- The SQL body follows after the closing `*/`.
- The SQL body can contain any valid SQL for the target database engine.

---

## TOML Parameters

The TOML header controls how RayMigrator processes the migration file. The available parameters are `Description`, `Environments`, `Targets`, `UseTransaction`, `RunAlways`, `RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, `MigrationErrorAction`, `RollbackErrorAction`, `UseCliToolAlias`, and `TargetGroupMigrationOrder`. For the complete specification with all defaults and cross-layer override behavior, see [TOML Metadata](../07-migration-files/toml-metadata.md).

### Description

A short, human-readable summary of the migration. This value is stored in the repository and displayed in logs. Keep it concise but meaningful.

```toml
Description = "Add ISBN column to Books table with unique index"
```

### Environments

Controls which environments this migration runs in. The value is matched against the `--environment` CLI argument.

- Omit the key — Run in all environments (default behavior when the key is not present).
- `["*"]` — Explicit wildcard; run in all environments.
- `["Production"]` — Run only when `--environment Production` is specified.
- `["Dev", "Staging"]` — Run in Dev or Staging but not in Production.

```toml
Environments = ["Production"]
```

> **Tip:** Use environment filtering for migrations that should only run in specific contexts, such as inserting test data in development or applying performance tuning in production.

### Targets

Metadata field that records which targets this migration is intended for. The value is parsed and stored in the repository as part of the migration record, but it is **not used for runtime target filtering**. Every target in a target group receives every migration file regardless of this value.

- Omit the key — All targets (default behavior when the key is not present).
- `["*"]` — Explicit wildcard; all targets.
- `["Primary"]` — Intended for the target with Alias `Primary` (informational only).

```toml
Targets = ["Primary"]
```

> **Note:** This parameter is reserved for future runtime filtering. Currently it has no effect on execution. See [TOML Metadata](../07-migration-files/toml-metadata.md#target-filtering) for details.

### UseTransaction

When `true`, RayMigrator wraps the SQL body in a database transaction. If any statement fails, the entire migration is rolled back.

```toml
UseTransaction = true
```

> **Warning:** In MariaDB and MySQL, DDL statements (CREATE TABLE, ALTER TABLE, DROP TABLE) cause an implicit transaction commit. Setting `UseTransaction = true` does not protect DDL from partial execution on these engines. See the [SQL Dialect Differences](#sql-dialect-differences) section for details.

### RunAlways

When `true`, this migration is executed on every migration run, even if it was successfully executed before. Useful for recreating views, stored procedures, or refreshing reference data.

```toml
RunAlways = true
```

During migrate-up, RunAlways files bypass the hash-based skip logic entirely and are always included in the execution list. The `validate-hash` command still checks RunAlways files for hash changes, so you can detect unintended modifications before deploying.

### RequireRollbackFile

Overrides the product-level `RequireRollbackFile` setting for this specific file. Set to `false` for migrations that cannot be meaningfully rolled back (such as data transformations).

```toml
RequireRollbackFile = false
```

If omitted, the value is inherited from the product configuration.

### MigrationErrorAction

Overrides the product-level `MigrationErrorAction` setting for this specific file. Controls what happens when the migration fails during execution.

```toml
MigrationErrorAction = "Rollback"
```

Valid values:

- `Terminate` — Stop the migration run immediately. No rollback is performed.
- `Rollback` — Roll back all migrations performed by the current migration run.
- `RollbackErrorOnly` — Roll back only the failed migration file using its rollback file.
- `RollbackRelease` — Roll back all migrations from the release that caused the error. Migrations from earlier releases remain intact.
- `Ignore` — Skip the failed SQL blocks and continue with the next migration file.

If omitted, the value is inherited from the nearest `migsettings.txt` / `migsettings.{Environment}.txt` file in the directory hierarchy, or from the product configuration.

### RollbackErrorAction

Overrides the product-level `RollbackErrorAction` setting for this specific file. Controls what happens when a rollback operation itself encounters an error.

```toml
RollbackErrorAction = "Terminate"
```

Valid values:

- `Terminate` — Stop the rollback chain immediately. No further rollbacks are performed. This is the default.
- `Ignore` — Skip the failed rollback and continue with the next rollback file in the chain.

If omitted, the value is inherited from the nearest `migsettings.txt` / `migsettings.{Environment}.txt` file in the directory hierarchy, or from the product configuration.

### UseCliToolAlias

Overrides the product/target-level `UseCliToolAlias` setting for this specific file. When set, RayMigrator uses the referenced external CLI tool (e.g., `sqlcmd`, `psql`) to execute the migration instead of the built-in DAL.

```toml
UseCliToolAlias = "sqlcmd-tool"
```

The alias must reference a `CliTools[].Alias` defined at the `RayMigrator` root level in configuration. If omitted, the value is inherited from the nearest `migsettings.txt` / `migsettings.{Environment}.txt` file in the directory hierarchy, or from the Target/TargetGroup/Product/ProductDefaults configuration cascade. See [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) for details.

### StopRollbackOnMissingRollbackFile

Declares the `StopRollbackOnMissingRollbackFile` preference for this file. Controls whether an error-recovery rollback chain stops when the rollback file for this migration is missing. Only applies when `RequireRollbackFile = false`.

```toml
StopRollbackOnMissingRollbackFile = false
```

Valid values:

- `true` (default) — The error-recovery rollback chain stops at this migration if its rollback file is missing. A warning is logged and the record status is left unchanged.
- `false` — The error-recovery rollback chain continues past this migration if its rollback file is missing. A warning is logged and the record status is left unchanged.

> **Note:** This setting is parsed from the TOML header but is not stored in the migration file metadata. The runtime rollback decision uses only the appsettings-level value (`ProductDefaults`, `Product`, `TargetGroup`) and the CLI `--stop-rollback-on-missing-rollback-file` option. The per-file TOML value is not consulted at rollback execution time. To control this behavior, set it in `appsettings.json` or via the CLI option. This setting has no effect on explicit `migrate-down` operations.

### TargetGroupMigrationOrder

Overrides the execution order of target groups for this release directory. The value is a list of TargetGroup aliases specifying the order in which groups are processed. When set, all TargetGroup aliases for the product must be listed exactly once.

```toml
TargetGroupMigrationOrder = ["Frontend", "Backend"]
```

This TOML key is only meaningful in release-level migsettings files (applied per-release), not in individual migration file headers. If omitted, the value is inherited from the nearest `migsettings.txt` / `migsettings.{Environment}.txt` file in the directory hierarchy, from the `TargetGroupMigrationOrder` property in the product's `appsettings.json`, or from the `--target-group-migration-order` CLI option. When no order is set anywhere, target groups are processed in the order they appear in configuration.

This parameter is only applicable when the product has more than one target group. Applies to `migrate-up` and `baseline` commands.

---

## Tutorial: Add More Migrations

Let us extend the BookStore with additional migration files. In Chapter 3 we created `001_CreateBooks.sql`. Now we add three more files.

### 002_CreateAuthors.sql

```sql
/*
[RayMigrator]
Description = "Create Authors table"
*/

CREATE TABLE [dbo].[Authors]
(
    [Id]        INT IDENTITY(1,1) NOT NULL,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName]  NVARCHAR(100) NOT NULL,
    [Country]   NVARCHAR(100) NULL,
    CONSTRAINT [PK_Authors] PRIMARY KEY ([Id])
);
```

### 003_AddBookAuthorFK.sql

```sql
/*
[RayMigrator]
Description = "Add AuthorId foreign key to Books table"
*/

ALTER TABLE [dbo].[Books]
    ADD [AuthorId] INT NULL;

ALTER TABLE [dbo].[Books]
    ADD CONSTRAINT [FK_Books_Authors]
    FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[Authors]([Id]);
```

### 004_InsertSeedData.sql

```sql
/*
[RayMigrator]
Description = "Insert sample authors and books"
*/

INSERT INTO [dbo].[Authors] ([FirstName], [LastName], [Country])
VALUES
    ('Frank', 'Herbert', 'USA'),
    ('Isaac', 'Asimov', 'USA'),
    ('Ursula', 'Le Guin', 'USA');

INSERT INTO [dbo].[Books] ([Title], [Author], [ISBN], [Price], [AuthorId])
VALUES
    ('Dune', 'Frank Herbert', '978-0441013593', 9.99, 1),
    ('Foundation', 'Isaac Asimov', '978-0553293357', 8.99, 2),
    ('The Left Hand of Darkness', 'Ursula Le Guin', '978-0441478125', 10.99, 3);
```

---

## File Naming Conventions

Migration files must follow a consistent naming pattern so RayMigrator processes them in the correct order.

**Format:** `{Sequence}_{Description}.sql`

**Rules:**

- **Sequence numbers** determine execution order. Files are sorted alphabetically using ordinal, case-insensitive comparison.
- **Zero-pad** sequence numbers to ensure correct alphabetical sorting.
- **Description** should be a brief, meaningful summary using PascalCase or camelCase.
- **Extension** must match the `MigrationFilesExtension` configuration (default: `sql`).

> **Important:** Always zero-pad sequence numbers! Without padding, `10_AddIndex.sql` sorts before `2_CreateTable.sql` alphabetically. Use `002_CreateTable.sql` and `010_AddIndex.sql` to guarantee correct order.

**Good examples:**

```
001_CreateBooks.sql
002_CreateAuthors.sql
003_AddBookAuthorFK.sql
010_AddISBNIndex.sql
100_RefreshViews.sql
```

**Bad examples:**

```
1_CreateBooks.sql        ← not zero-padded
2_CreateAuthors.sql      ← will sort after 10_xxx
create books.sql         ← no sequence number, spaces in name
```

---

## Directory Structure

Migration files are organized in a specific directory hierarchy under the `MigrationFilesRootDirectory` configured for the product.

```
MigrationFilesRootDirectory/
├── Release 1.0/
│   ├── Backend/                    ← matches TargetGroup Alias
│   │   ├── 001_CreateBooks.sql
│   │   ├── 001_CreateBooks.rollback.sql
│   │   ├── 002_CreateAuthors.sql
│   │   ├── 002_CreateAuthors.rollback.sql
│   │   ├── 003_AddBookAuthorFK.sql
│   │   ├── 003_AddBookAuthorFK.rollback.sql
│   │   ├── 004_InsertSeedData.sql
│   │   └── 004_InsertSeedData.rollback.sql
│   └── Reporting/                  ← another TargetGroup
│       ├── 001_CreateReportViews.sql
│       └── 001_CreateReportViews.rollback.sql
├── Release 1.1/
│   └── Backend/
│       ├── 001_AddISBNIndex.sql
│       └── 001_AddISBNIndex.rollback.sql
└── Release 2.0/
    └── Backend/
        └── 001_RefactorAuthors.sql
```

**Rules:**

- **Release directories** are the first level under the root. They are sorted alphabetically, so use consistent naming (e.g., `Release 1.0`, `Release 1.1`, `Release 2.0`).
- **TargetGroup directories** are the second level in the traditional layout. The directory name **must match** the `Alias` of a configured TargetGroup exactly (case-sensitive). When a product has exactly one target group, you may omit the subdirectory entirely and place migration files directly under the release directory (flat layout). See [Directory Structure](../07-migration-files/directory-structure.md) for full details.
- **Migration files** are inside TargetGroup directories (traditional layout) or directly under the release directory (flat layout). They are sorted alphabetically within each directory.
- **Rollback files** sit alongside their corresponding migration files.

> **Tip:** Sequence numbers reset in each release directory. Each release starts fresh with `001_`.

---

## Rollback Files

Rollback files contain SQL that undoes a migration. They are executed during `migrate-down` operations.

### Naming Convention

The default naming pattern is:

```
{MigrationFilename}.rollback.sql
```

For example:

| Migration File | Rollback File |
|---------------|---------------|
| `001_CreateBooks.sql` | `001_CreateBooks.rollback.sql` |
| `002_CreateAuthors.sql` | `002_CreateAuthors.rollback.sql` |

The `rollback` part is configured via the `MigrationRollbackFilesPreExtension` product setting. If you change it to `undo`, rollback files would be named `001_CreateBooks.undo.sql`.

### Rollback File Structure

Rollback files have the same structure as migration files: an optional TOML header followed by SQL.

```sql
/*
[RayMigrator]
Description = "Rollback: Drop Books table"
*/

DROP TABLE IF EXISTS [dbo].[Books];
```

### Tutorial: BookStore Rollback Files

**001_CreateBooks.rollback.sql:**

```sql
/*
[RayMigrator]
Description = "Rollback: Drop Books table"
*/

DROP TABLE IF EXISTS [dbo].[Books];
```

**002_CreateAuthors.rollback.sql:**

```sql
/*
[RayMigrator]
Description = "Rollback: Drop Authors table"
*/

DROP TABLE IF EXISTS [dbo].[Authors];
```

**003_AddBookAuthorFK.rollback.sql:**

```sql
/*
[RayMigrator]
Description = "Rollback: Remove AuthorId FK and column from Books"
*/

ALTER TABLE [dbo].[Books]
    DROP CONSTRAINT IF EXISTS [FK_Books_Authors];

ALTER TABLE [dbo].[Books]
    DROP COLUMN IF EXISTS [AuthorId];
```

**004_InsertSeedData.rollback.sql:**

```sql
/*
[RayMigrator]
Description = "Rollback: Remove seed data"
*/

DELETE FROM [dbo].[Books]
WHERE [Title] IN ('Dune', 'Foundation', 'The Left Hand of Darkness');

DELETE FROM [dbo].[Authors]
WHERE [LastName] IN ('Herbert', 'Asimov', 'Le Guin');
```

### Best Practices for Rollback Files

- **Always use `IF EXISTS`** in DROP and ALTER statements to make rollbacks idempotent.
- **Handle foreign keys first.** Drop dependent constraints before dropping referenced tables.
- **Reverse the order.** If migration 003 added a FK and a column, the rollback should drop the FK first, then the column.
- **Test rollbacks.** Run `migrate-up` followed by `migrate-down` in development to verify rollbacks work correctly.

---

## Environment-Specific Files

Migration files can be restricted to specific environments using a special naming convention.

**Format:** `{Sequence}_{Description}.{Environment}.sql`

**Examples:**

```
005_InsertTestData.Development.sql
005_InsertProdConfig.Production.sql
```

**How it works:**

- A file named `005_InsertTestData.Development.sql` only runs when `--environment Development` is specified.
- If a base file `005_InsertConfig.sql` and an environment-specific file `005_InsertConfig.Production.sql` both exist, **both** files are included when running in Production. The environment-specific file does not replace the base file. To avoid double-execution, use the TOML `Environments` parameter in the base file to exclude the environment that has its own variant.
- Files without an environment suffix run in all environments (unless restricted by TOML `Environments` parameter).

> **Tip:** Use the TOML `Environments` parameter for simple environment filtering. Use environment-specific file naming when you need completely different SQL for different environments.

---

## migsettings Files

Migsettings files let you override TOML defaults at the directory level. Place them in any migration directory to apply settings to all files in that directory and its subdirectories.

There are two variants:

| File | Scope |
|------|-------|
| `migsettings.txt` | Base settings for the directory |
| `migsettings.{Environment}.txt` | Environment-specific overrides (e.g., `migsettings.Docker.txt`) |

**Example — `migsettings.txt`:**

```
[RayMigrator]
UseTransaction = false
RunAlways = true
```

**Example — `migsettings.Production.txt`:**

```
[RayMigrator]
UseTransaction = true
```

**Valid keys:** `UseTransaction`, `RunAlways`, `RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, `Environments`, `Targets`, `MigrationErrorAction`, `RollbackErrorAction`, `UseCliToolAlias`, `TargetGroupMigrationOrder` (10 keys). `Description` is accepted but has no effect in migsettings files.

**Rules:**

- The file must be named exactly `migsettings.txt` or `migsettings.{Environment}.txt`.
- It must contain the `[RayMigrator]` section header.
- It uses the same TOML key-value syntax as the file headers (plain TOML, no `/* */` SQL comment wrapper).
- Values apply to all migration files in the directory and its subdirectories.
- Within the same directory, environment-specific settings override the base `migsettings.txt`.
- File-level TOML headers override migsettings values.

**Use case:** Place `UseTransaction = false` in a directory containing DDL-heavy migrations for MariaDB/MySQL where transactions do not protect DDL.

**Precedence (lowest to highest):**

1. Product-level configuration defaults
2. `migsettings.txt` at the product root
3. `migsettings.{Environment}.txt` at the product root
4. `migsettings.txt` at the release level
5. `migsettings.{Environment}.txt` at the release level
6. `migsettings.txt` at the target group level
7. `migsettings.{Environment}.txt` at the target group level
8. File-level TOML header (highest priority)

---

## SQL Dialect Differences

RayMigrator supports five database engines, each with different SQL syntax requirements. For the complete database support matrix, see [SQL Dialects](../03-database-layer/sql-dialects.md).

### SQL Server

SQL Server uses `GO` as the batch separator. Each `GO`-delimited block is sent to the server as a separate batch.

```sql
CREATE TABLE [dbo].[Books]
(
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Title] NVARCHAR(200) NOT NULL
);
GO

CREATE INDEX [IX_Books_Title] ON [dbo].[Books]([Title]);
GO
```

> **Important:** `GO` must appear on its own line. It is not a SQL statement — it is a batch separator processed by RayMigrator before sending SQL to the server.

### PostgreSQL

PostgreSQL uses `;` as the statement separator and supports full DDL transactions.

```sql
CREATE TABLE "Authors"
(
    "Id"      SERIAL PRIMARY KEY,
    "Name"    VARCHAR(200) NOT NULL,
    "Country" VARCHAR(100)
);

CREATE INDEX "IX_Authors_Name" ON "Authors"("Name");
```

> **Tip:** PostgreSQL folds unquoted identifiers to lowercase. If your schema uses PascalCase column names, always use double-quoted identifiers.

### MariaDB and MySQL

Both use `;` as the statement separator. DDL statements cause an implicit transaction commit.

```sql
CREATE TABLE Authors
(
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    Name    VARCHAR(200) NOT NULL,
    Country VARCHAR(100)
);

CREATE INDEX IX_Authors_Name ON Authors(Name);
```

> **Warning:** In MariaDB and MySQL, DDL statements (CREATE TABLE, ALTER TABLE, DROP TABLE) cause an implicit transaction commit. This means that if a migration file contains multiple DDL statements and one fails, the preceding DDL statements cannot be rolled back by the transaction. Setting `UseTransaction = true` only protects DML statements (INSERT, UPDATE, DELETE). Plan your DDL migrations accordingly — ideally one DDL change per migration file.

### SQLite

SQLite uses `;` as the statement separator and supports full DDL transactions.

```sql
CREATE TABLE Authors
(
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT NOT NULL,
    Country TEXT
);

CREATE INDEX IX_Authors_Name ON Authors(Name);
```

> **Tip:** SQLite uses dynamic typing. Column type names are advisory and do not enforce strict types. Use `INTEGER PRIMARY KEY AUTOINCREMENT` for auto-incrementing primary keys.

---

## RunAlways Migrations

Migrations with `RunAlways = true` are re-executed on every migration run, regardless of whether they were previously executed successfully.

**Common use cases:**

- **Views and stored procedures** that need to be recreated when their definition changes.
- **Reference data** that must be refreshed to match the current release.
- **Permissions and grants** that must be reapplied after schema changes.

**Example — recreate a view:**

```sql
/*
[RayMigrator]
Description = "Recreate BookDetails view"
RunAlways = true
UseTransaction = true
*/

DROP VIEW IF EXISTS [dbo].[vw_BookDetails];
GO

CREATE VIEW [dbo].[vw_BookDetails]
AS
SELECT
    b.[Id],
    b.[Title],
    b.[ISBN],
    a.[FirstName] + ' ' + a.[LastName] AS [AuthorName],
    a.[Country] AS [AuthorCountry]
FROM [dbo].[Books] b
LEFT JOIN [dbo].[Authors] a ON b.[AuthorId] = a.[Id];
GO
```

**Hash validation:** During migrate-up, RunAlways files bypass the hash-based skip logic and are always re-executed regardless of whether their content has changed. To detect unintended changes to RunAlways files, use the `validate-hash` command, which compares current file hashes against the repository records.

> **Tip:** Place RunAlways files at the end of your sequence (e.g., `900_RecreateViews.sql`) so they execute after all schema changes are applied.

---

## Next Steps

With migration files written and organized, the next chapter covers running migrations: executing `migrate-up`, simulating runs, and handling errors.

See [Migration File Specification](../07-migration-files/directory-structure.md) for the full migration file specification including advanced TOML options.

**Next:** [Chapter 06 — CLI Command Reference](06-cli-commands.md)
