# SQL Dialects

RayMigrator supports multiple database systems, each with specific SQL dialect requirements.

## Comparison Table

| Feature | SQL Server | PostgreSQL | MariaDB | MySQL | SQLite |
|---------|------------|------------|---------|-------|--------|
| **.NET Driver** | `Microsoft.Data.SqlClient` | `Npgsql` | `MySqlConnector` | `MySqlConnector` | `Microsoft.Data.Sqlite` |
| **Statement Separator** | `;` | `;` | `;` | `;` | `;` |
| **Block Separator** (`SqlBlockDelimiter`) | `GO` | `;` | `;` | `;` | `;` |
| **Multi-Line Comment** (`SqlMultiLineCommentStart`/`End`) | `/*` `*/` | `/*` `*/` | `/*` `*/` | `/*` `*/` | `/*` `*/` |
| **Identifier Escape** | `[` `]` | `"` `"` | `` ` `` | `` ` `` | `"` `"` |
| **DDL Transactions** | Full | Full | Limited | Limited | Full |
| **Auto-increment** | `IDENTITY(1,1)` | `GENERATED ALWAYS AS IDENTITY` | `AUTO_INCREMENT` | `AUTO_INCREMENT` | `INTEGER PRIMARY KEY AUTOINCREMENT` |
| **UUID Type** | `UNIQUEIDENTIFIER` | `UUID` | `UUID` | `CHAR(36)` | `TEXT` |
| **Current UTC Timestamp** | `SYSUTCDATETIME()` | `NOW()` (columns are `TIMESTAMPTZ`) | `CURRENT_TIMESTAMP` (columns are `TIMESTAMP`, session `time_zone='+00:00'`) | `CURRENT_TIMESTAMP` (columns are `TIMESTAMP`, session `time_zone='+00:00'`) | `datetime('now')` |
| **Audit Column Type** | `DATETIME2(3)` | `TIMESTAMPTZ` | `TIMESTAMP` | `TIMESTAMP` | `TEXT` |
| **Default Charset / Collation** | n/a (UTF-16) | n/a (database-wide) | `utf8mb4` / `utf8mb4_unicode_ci` | `utf8mb4` / `utf8mb4_0900_ai_ci` | UTF-8 |
| **Minimum Engine Version** | 2016+ | 11+ | 10.5+ (LTS, for `utf8mb4_unicode_ci`) | 8.0+ (for `utf8mb4_0900_ai_ci`) | 3.35+ |
| **String Concatenation** | `+` | `\|\|` | `CONCAT()` | `CONCAT()` | `\|\|` |
| **Schema Support** | Yes | Yes | No (databases) | No (databases) | No |

**Note**: Both MariaDB and MySQL use the same `MySqlConnector` NuGet package as their ADO.NET driver, since MySqlConnector supports both database engines.

## Template maintenance policy — MySQL ↔ MariaDB

MySQL and MariaDB share a common ancestor and most SQL syntax, but they are two separate engines with diverging feature sets. RayMigrator ships **fully separate template files per engine** (Option B) rather than maintaining a single shared set with per-engine placeholders (Option A) or duplicate templates kept in sync by convention (Option C).

**Rationale**

- **Volume is manageable.** Each engine has approximately 20 SQL template files under 300 lines each. The maintenance cost of duplicated files is measurable but bounded, and considerably cheaper than the readability cost of inline placeholders for every dialect-specific token.
- **Templates stay idiomatic.** Readers see native MySQL SQL or native MariaDB SQL — no `{ENGINE:Var}` placeholders obscuring which dialect is actually running.
- **Divergence is expected to grow.** JSON path expressions, window-function variants, vector types, and dialect-specific optimizer hints are all areas where MySQL 8+ and MariaDB 10.10+ have already moved apart. A placeholder-shared file scales poorly into that future.
- **Per-engine CI catches regressions.** `dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Database=MySql"` runs only the MySQL engine; the equivalent MariaDB filter runs the MariaDB engine. A template change that breaks one engine is caught before merge.

**Existing intentional divergence points**

- **Default collation.** MySQL templates use `utf8mb4_0900_ai_ci` (available only in MySQL 8.0+). MariaDB templates use `utf8mb4_unicode_ci` (stable from MariaDB 10.5+ LTS). MariaDB does not ship `utf8mb4_0900_*` collations — copying MySQL's collation name into a MariaDB template produces `ERROR 1273 (HY000): Unknown collation: 'utf8mb4_0900_ai_ci'`. See DAL-015 for the original design decision.
- **Transient-error codes.** `RetryHelper.TransientMySqlErrorCodes` and `RetryHelper.TransientMariaDbErrorCodes` are separate arrays even though today they hold the same values. Keeping them separate lets each engine's list diverge as the two driver dialects emit different codes for the same condition.
- **Minimum-version comments.** MySQL templates target 8.0+ (for expression defaults, `utf8mb4_0900_ai_ci`, and related features); MariaDB templates target 10.5+ LTS. Notes in the TOML header of each template should call out the engine-specific floor.

**Out of scope for this section**

The accompanying PR-checklist item ("If this touches MySQL templates, does MariaDB need the same or a different change?") is deferred to a future workflow change and is not part of the current policy documentation.

## SQL Server

### Block Separator: GO

SQL Server uses `GO` as a batch separator. This is a client-side command, not T-SQL.

```sql
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100)
);
GO

CREATE INDEX IX_Users_Name ON Users(Name);
GO
```

**Important**: `GO` is processed by sqlcmd, SSMS, and RayMigrator, not by SQL Server itself.

### Identifier Escaping

```sql
-- Use brackets for special characters or reserved words
SELECT [Order], [User Name]
FROM [My Table]
WHERE [Date] > '2024-01-01';
```

### DDL Transactions

SQL Server fully supports DDL in transactions:

```sql
BEGIN TRANSACTION;
    CREATE TABLE Test (Id INT);
    ALTER TABLE Test ADD Name VARCHAR(100);
COMMIT;
-- Or ROLLBACK if needed
```

### Schema Support

```sql
-- Create schema
CREATE SCHEMA myschema;

-- Use schema
CREATE TABLE myschema.MyTable (...);
```

## PostgreSQL

### No Block Separator

PostgreSQL uses `;` for both statement and block separation:

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100)
);

CREATE INDEX ix_users_name ON users(name);
```

### Identifier Escaping

```sql
-- Use double quotes for case-sensitive identifiers (user migrations)
SELECT "Order", "User Name"
FROM "My Table"
WHERE "Date" > '2024-01-01';
```

**Important**: Unquoted identifiers are folded to lowercase in PostgreSQL. RayMigrator's own repository tables use **unquoted snake_case** identifiers (e.g., `migration_run_result_id`) — the PostgreSQL community convention. Queries written by DBAs can therefore reference repository tables without any quoting (e.g., `SELECT * FROM ray.migration_record`). User migration SQL may still use quoted PascalCase if case-sensitivity is required for user-owned tables.

### DDL Transactions

PostgreSQL fully supports DDL in transactions:

```sql
BEGIN;
    CREATE TABLE test (id SERIAL);
    ALTER TABLE test ADD COLUMN name VARCHAR(100);
COMMIT;
```

### Schema Support

```sql
-- Create schema
CREATE SCHEMA IF NOT EXISTS myschema;

-- Use schema
CREATE TABLE myschema.my_table (...);
```

### Dollar Quoting and RAISE NOTICE

PostgreSQL templates use `DO $$` anonymous blocks for complex logic. Since `DO` blocks cannot return result sets, the PostgreSQL DAL uses `RAISE NOTICE` to communicate results from within `DO` blocks. The `DalPostgreSql.ExecuteScalarAsync` method captures `RAISE NOTICE` output via the `connection.Notice` event and returns it as the scalar result when `ExecuteScalarAsync` returns `null`.

```sql
DO $$
DECLARE
    v_result INT;
BEGIN
    -- Complex logic here
    v_result := 42;
    RAISE NOTICE '%,%', v_result, 'Success message';
END $$;

-- Followed by a SELECT for the actual return value
SELECT 'code,message';
```

### Schema Separation

Repository tables use a dedicated schema (e.g., `ray`) with unquoted snake_case table names: `ray.migration_record`. User migration tables typically use lowercase names in the `public` schema.

## MariaDB

### Statement Separator

MariaDB uses `;` for statements:

```sql
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100)
);

CREATE INDEX ix_users_name ON users(name);
```

### DELIMITER Command

For stored procedures, use DELIMITER:

```sql
DELIMITER //
CREATE PROCEDURE my_proc()
BEGIN
    SELECT * FROM users;
END //
DELIMITER ;
```

### Identifier Escaping

```sql
-- Use backticks
SELECT `Order`, `User Name`
FROM `My Table`
WHERE `Date` > '2024-01-01';
```

### Limited DDL Transactions

MariaDB has limited DDL transaction support. Some operations cause implicit commits:

```sql
-- This COMMITS the CREATE TABLE implicitly
START TRANSACTION;
    INSERT INTO existing_table VALUES (1);
    CREATE TABLE new_table (id INT);  -- Implicit COMMIT here!
    INSERT INTO new_table VALUES (1);
COMMIT;
```

### No Schema Support

MariaDB uses databases instead of schemas:

```sql
-- Create database
CREATE DATABASE mydb;

-- Use database
USE mydb;
CREATE TABLE my_table (...);
```

### Connection String Requirements

The MariaDB DAL automatically appends `AllowUserVariables=true` to the connection string. This is required because MariaDB templates use session variables (`@v_count`, `@dummy`, etc.) alongside command parameters.

### UUID Support

MariaDB 10.7+ has native UUID type:

```sql
CREATE TABLE items (
    id UUID DEFAULT UUID() PRIMARY KEY,
    name VARCHAR(100)
);
```

## MySQL

### Statement Separator

MySQL uses `;` for statements (same as MariaDB):

```sql
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100)
);

CREATE INDEX ix_users_name ON users(name);
```

### DELIMITER Command

For stored procedures, use DELIMITER (same as MariaDB):

```sql
DELIMITER //
CREATE PROCEDURE my_proc()
BEGIN
    SELECT * FROM users;
END //
DELIMITER ;
```

### Identifier Escaping

```sql
-- Use backticks (same as MariaDB)
SELECT `Order`, `User Name`
FROM `My Table`
WHERE `Date` > '2024-01-01';
```

### Limited DDL Transactions

MySQL has limited DDL transaction support (same as MariaDB). Some operations cause implicit commits:

```sql
-- This COMMITS the CREATE TABLE implicitly
START TRANSACTION;
    INSERT INTO existing_table VALUES (1);
    CREATE TABLE new_table (id INT);  -- Implicit COMMIT here!
    INSERT INTO new_table VALUES (1);
COMMIT;
```

### No Schema Support

MySQL uses databases instead of schemas (same as MariaDB):

```sql
-- Create database
CREATE DATABASE mydb;

-- Use database
USE mydb;
CREATE TABLE my_table (...);
```

### Connection String Requirements

Like MariaDB, the MySQL DAL automatically appends `AllowUserVariables=true` to the connection string, since MySQL templates also use session variables alongside command parameters.

### Expression Defaults

MySQL 8.0+ requires parentheses around expression defaults (`DEFAULT (expression)`), while MariaDB does not. RayMigrator avoids this divergence by using the bare scalar keyword `CURRENT_TIMESTAMP` on `TIMESTAMP` columns — valid on both engines without parentheses and semantically identical to `UTC_TIMESTAMP()` once the session is pinned to `time_zone='+00:00'`:

```sql
`CreatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
```

### User Management

MySQL 8.0+ forbids combining `GRANT` with `IDENTIFIED BY` in a single statement. User creation and privilege grants must be separate:

```sql
-- MySQL 8.0+: separate statements required
CREATE USER IF NOT EXISTS 'myuser'@'%' IDENTIFIED BY 'password';
GRANT ALL PRIVILEGES ON mydb.* TO 'myuser'@'%';

-- MariaDB: combined statement still works
GRANT ALL PRIVILEGES ON mydb.* TO 'myuser'@'%' IDENTIFIED BY 'password';
```

## SQLite

### Statement Separator

SQLite uses `;` for statements:

```sql
CREATE TABLE users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL
);

CREATE INDEX ix_users_name ON users(name);
```

### Identifier Escaping

```sql
-- Use double quotes (same as PostgreSQL)
SELECT "Order", "User Name"
FROM "My Table"
WHERE "Date" > '2024-01-01';
```

### DDL Transactions

SQLite fully supports DDL in transactions. However, RayMigrator templates use `CREATE TABLE IF NOT EXISTS` for idempotency:

```sql
BEGIN TRANSACTION;
    CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY);
    ALTER TABLE test ADD COLUMN name TEXT;
COMMIT;
```

### No Schema Support

SQLite does not support schemas. Tables exist in a single flat namespace:

```sql
CREATE TABLE my_table (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT
);
```

### No Session Variables

SQLite does not support session variables (`SET @var = ...`). Templates use a temporary table `_rc_state` to store intermediate state:

```sql
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT);
DELETE FROM "_rc_state";

INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('repository_version', '2026-04-17.1'),
    ('pre_table_count', CAST((SELECT COUNT(*) FROM sqlite_master
        WHERE type='table' AND name IN (...)) AS TEXT));
```

### SQLite-Specific Patterns

- **Timestamps**: `datetime('now')` for UTC timestamps
- **Existence checks**: `sqlite_master` table (instead of `information_schema`)
- **Idempotent DDL**: `CREATE TABLE IF NOT EXISTS`
- **Idempotent DML**: `INSERT OR IGNORE` / `INSERT OR REPLACE`
- **WAL mode**: Connection validation sets `PRAGMA journal_mode=WAL` for better concurrency
- **STRICT tables**: RayMigrator's repository and logging templates use `CREATE TABLE ... STRICT` (SQLite 3.37+, DAL-022). This enforces column types (`INTEGER`, `TEXT`) at INSERT time instead of relying on SQLite's type-affinity coercion. The bundled `Microsoft.Data.Sqlite` is well above the 3.37 floor; no DAL code change is required.
- **Datetime CHECK**: Every `TEXT` datetime column in the repository and logging schemas carries a CHECK constraint enforcing strict ISO-8601 (`YYYY-MM-DD HH:MM:SS`) via `datetime()` round-trip (DAL-021). Form for NOT NULL columns: `CHECK (datetime("X") IS NOT NULL AND datetime("X") = "X")`; nullable columns add `"X" IS NULL OR (...)`. The `IS NOT NULL` guard is required because SQLite's CHECK treats NULL as non-violation — without it, malformed input like `'yesterday'` would silently pass. All RayMigrator write paths use `datetime('now')` (no subsec, no `T` separator), so the strict CHECK never fires on Production inserts.
- **Primary use**: SQLite can be used as a migration repository and as a target database for migrations

## Migration File Considerations

### SplitSqlIntoBlocks

RayMigrator splits migration file SQL content into blocks using the database-specific `SqlBlockDelimiter`. The `SplitSqlIntoBlocks` method uses a regex that matches the delimiter on its own line (case-insensitive for `GO`):

- **SQL Server**: Splits on `GO` appearing alone on a line. Each block is executed separately.
- **PostgreSQL/MariaDB/MySQL/SQLite**: The delimiter is `;`, but since `;` is also a statement terminator, the entire file content is typically treated as a single block (individual statements separated by `;` within the block).

**Known limitation**: `GO` appearing inside multi-line comments or string literals in SQL Server migrations will still trigger a split (regex-based splitting cannot distinguish these contexts).

When all blocks are empty after splitting, the method falls back to returning the original content as a single block.

**CLI tool execution**: When a migration file or target group is configured to use CLI tool execution (via `UseCliToolAlias`), block splitting is skipped entirely. CLI tools execute the entire file as a single unit, so delimiter-based splitting is only needed for .NET DAL execution where each block is executed individually.

### Multi-Block Migrations

**SQL Server** - Use GO:
```sql
CREATE TABLE A (...);
GO
CREATE TABLE B (...);
GO
```

**PostgreSQL/MariaDB/MySQL/SQLite** - Separate statements:
```sql
CREATE TABLE A (...);
CREATE TABLE B (...);
```

### Stored Procedures

**SQL Server**:
```sql
CREATE PROCEDURE MyProc AS
BEGIN
    SELECT 1;
END
GO
```

**PostgreSQL**:
```sql
CREATE OR REPLACE FUNCTION my_proc()
RETURNS INTEGER AS $$
BEGIN
    RETURN 1;
END;
$$ LANGUAGE plpgsql;
```

**MariaDB**:
```sql
DELIMITER //
CREATE PROCEDURE MyProc()
BEGIN
    SELECT 1;
END //
DELIMITER ;
```

**MySQL**:
```sql
DELIMITER //
CREATE PROCEDURE MyProc()
BEGIN
    SELECT 1;
END //
DELIMITER ;
```

### Transaction Handling

Configure in TOML metadata:

```sql
/*
[RayMigrator]
UseTransaction = true   -- SQL Server, PostgreSQL, SQLite
-- UseTransaction = false -- MariaDB/MySQL for DDL
*/
```

## Template Dialect Patterns

The RayMigrator repository templates use specific patterns per database dialect:

### SQL Server Template Pattern

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    -- Logic with DECLARE, IF/ELSE, INSERT...OUTPUT
    SELECT 'code,message';
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK TRANSACTION;
    ;THROW;
END CATCH;
```

- **Variables**: `DECLARE @v_xxx TYPE;`
- **Timestamps**: `SYSUTCDATETIME()`
- **Duration**: `DATEDIFF(MILLISECOND, @StartedAt, SYSUTCDATETIME())`
- **Identifiers**: `[{CFG:SchemaName}].[{CFG:TableBaseName}TableName]`
- **String concat**: `+`

### PostgreSQL Template Pattern

```sql
DO $$
DECLARE
    v_xxx TYPE;
BEGIN
    -- Logic with IF/THEN/END IF
    RAISE NOTICE 'code,message';
END $$;

SELECT 'code,message';
```

- **Variables**: `DECLARE v_xxx TYPE;` inside DO blocks
- **Timestamps**: `NOW()` (audit columns are `TIMESTAMPTZ`; `NOW()` returns `TIMESTAMPTZ` in the session timezone, correctly stored as UTC)
- **Duration**: `EXTRACT(EPOCH FROM (NOW() - started_at)) * 1000`
- **Identifiers**: `{CFG:SchemaName}.{CFG:TableBaseName}table_name` (unquoted snake_case)
- **String concat**: `||`
- **Auto-increment**: `GENERATED ALWAYS AS IDENTITY`
- **Return from DO**: `RAISE NOTICE` inside DO, followed by `SELECT` outside

### MariaDB/MySQL Template Pattern

MariaDB and MySQL share the same template structure (with minor SQL syntax differences noted above). They use standalone DML/DDL statements followed by a final `SELECT CASE` for the result. DDL and DML cannot be used as subquery expressions in MariaDB/MySQL.

```sql
-- Variables from queries
SET @v_xxx = (SELECT ...);

-- Standalone DML (not wrapped in SELECT/subquery) — DAL-018: unquoted snake_case identifiers
INSERT INTO table_name (...) SELECT ... FROM DUAL WHERE condition;
SET @v_new_id = LAST_INSERT_ID();

-- Or for UPDATE/DELETE
UPDATE table_name SET ... WHERE condition;
SET @v_affected = ROW_COUNT();

-- Final result SELECT
SELECT CASE
    WHEN @v_affected = 0 THEN CONCAT('-1,Error message')
    ELSE CONCAT(CAST(@v_new_id AS CHAR), ',Success message')
END;
```

- **Variables**: `SET @v_xxx = (SELECT ...);`
- **Timestamps**: `CURRENT_TIMESTAMP` (audit columns are `TIMESTAMP`; the DAL enforces session `time_zone='+00:00'` on every connection open, so `CURRENT_TIMESTAMP` writes UTC)
- **Duration**: `TIMESTAMPDIFF(MICROSECOND, StartedAt, CURRENT_TIMESTAMP) / 1000`
- **Identifiers**: `{CFG:TableBaseName}table_name` (unquoted snake_case; no schema prefix — DAL-018)
- **String concat**: `CONCAT(a, b, c)`
- **Auto-increment**: `AUTO_INCREMENT`
- **Standalone DML**: DML/DDL statements run as separate statements, never as subquery expressions
- **Last insert ID**: `LAST_INSERT_ID()` after INSERT to retrieve auto-increment value
- **Affected rows**: `ROW_COUNT()` after UPDATE/DELETE to check affected rows
- **Conditional INSERT**: `INSERT INTO ... SELECT ... FROM DUAL WHERE condition`
- **Idempotent DDL**: `CREATE TABLE IF NOT EXISTS` with inline FK constraints
- **Idempotent DML**: `INSERT IGNORE` for master data
- **Existence check**: `information_schema.TABLES` with `DATABASE()`
- **Multi-statement**: Templates use multiple SET/DML/DDL statements; MySqlConnector's `ExecuteScalarAsync` automatically advances past intermediate SET statements (which produce no result sets) and returns the scalar value from the final `SELECT`

### SQLite Template Pattern

SQLite templates use a temporary table `_rc_state` for intermediate state storage (since SQLite lacks session variables) and standard SQL with `SELECT` for returning results.

```sql
-- State storage via temp table
CREATE TEMP TABLE IF NOT EXISTS "_rc_state" ("key" TEXT PRIMARY KEY, "val" TEXT);
DELETE FROM "_rc_state";

INSERT OR REPLACE INTO "_rc_state" ("key", "val") VALUES
    ('repository_version', '2026-04-17.1'),
    ('pre_table_count', CAST((SELECT COUNT(*) FROM sqlite_master
        WHERE type='table' AND name IN (...)) AS TEXT));

-- DDL with IF NOT EXISTS
CREATE TABLE IF NOT EXISTS "{CFG:TableBaseName}TableName" (
    "Id" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    PRIMARY KEY ("Id")
);

-- Idempotent master data
INSERT OR IGNORE INTO "{CFG:TableBaseName}TableName" ("Id", "Name")
VALUES (10, 'SomeValue');

-- Final result SELECT
SELECT CASE
    WHEN ... THEN 'code,Success message'
    ELSE '-1,Error message'
END;
```

- **Variables**: Temp table `_rc_state` with key-value pairs (no session variables)
- **Timestamps**: `datetime('now')`
- **Identifiers**: `"{CFG:TableBaseName}TableName"` (double-quoted, no schema prefix)
- **String concat**: `||`
- **Auto-increment**: `INTEGER PRIMARY KEY AUTOINCREMENT`
- **Existence check**: `sqlite_master` table with `type='table'`
- **Idempotent DDL**: `CREATE TABLE IF NOT EXISTS`
- **Idempotent DML**: `INSERT OR IGNORE` for master data, `INSERT OR REPLACE` for state
- **WAL mode**: Set via `PRAGMA journal_mode=WAL` on connection validation

## Parameter Substitution

All DALs except SQL Server use manual parameter substitution (replacing `@paramName` placeholders with formatted values) instead of ADO.NET parameterized queries. This is necessary because:

- **PostgreSQL**: `DO $$` anonymous blocks treat `@paramName` as literal text, not as parameterized query placeholders.
- **MariaDB/MySQL**: Templates use session variables (`@v_count`, `@dummy`) alongside command parameters (`@ProductId`). Manual substitution prevents conflicts between session variables and command parameters.
- **SQLite**: Multi-statement scripts where standard parameterized queries would not work across statement boundaries.
- **SQL Server**: Uses native ADO.NET parameterized queries (`SqlParameter`) since T-SQL fully supports them.

## Naming Conventions per Engine

RayMigrator's internal repository tables, columns, constraints, and indexes follow engine-specific naming conventions.

### PostgreSQL (DAL-017), MariaDB and MySQL (DAL-018)

PostgreSQL, MariaDB, and MySQL all use **unquoted snake_case** for repository identifiers. For PostgreSQL this follows the community convention; MariaDB and MySQL adopted the same convention in DAL-018. Queries against a PostgreSQL repository work without any quoting, e.g. `SELECT * FROM ray.migration_record`. MariaDB and MySQL do not have a separate schema namespace — repository tables live directly in the configured database.

**Mechanical conversion rule:** Every ASCII uppercase letter that is immediately preceded by a lowercase letter receives a preceding `_`, then the entire string is lowercased. For example:

| PascalCase | snake_case |
|---|---|
| `MigrationRecord` | `migration_record` |
| `MigrationRunResultId` | `migration_run_result_id` |
| `FileUpBlocksTotal` | `file_up_blocks_total` |
| `CreatedAt` | `created_at` |

**Product-name exception:** The brand token `RayMigrator` is treated as a single word rather than two (`Ray` + `Migrator`). It maps to `raymigrator`, not `ray_migrator`. This is the only exception to the mechanical rule.

| PascalCase | snake_case |
|---|---|
| `RayMigratorVersion` | `raymigrator_version` |
| `RayMigratorHostMode` | `raymigrator_host_mode` |
| `CreatedByRayMigratorVersion` | `created_by_raymigrator_version` |

The `RepositoryQueryHelper.ToSnakeCase` helper in the Testing project implements this exception via a sentinel pre-pass (`RayMigrator` → `Raymigrator` before mechanical splitting) and is covered by `P1_RepositoryQueryHelperToSnakeCaseTests`. The same `ToSnakeCase` logic is used when `RepositoryQueryHelper` formats table and column names for MariaDB and MySQL queries (DAL-018).

**`TableBaseName` must be lowercase for PostgreSQL, MariaDB, and MySQL.** The `RayMigratorOptionsValidator` rejects any uppercase character in `Repository.TableBaseName` or `DatabaseLogging.TableBaseName` when `DatabaseType` is `"PostgreSQL"`, `"MariaDb"`, or `"MySql"`. Rationale: PostgreSQL folds unquoted identifiers to lowercase; the MariaDB/MySQL snake_case repository schema (DAL-018) is stored as lowercase; any uppercase prefix character would break the `information_schema.tables` existence checks in `Repository_CheckCreate` and `DatabaseLogging_CheckCreate`.

### MariaDB and MySQL (DAL-018)

After DAL-018, MariaDB and MySQL use the same **unquoted snake_case** convention as PostgreSQL. All 36 MySQL and MariaDB SQL templates were converted from backtick-quoted PascalCase (`` `MigrationRecord` ``) to unquoted snake_case (`migration_record`). The final on-disk table and column names match PostgreSQL exactly, including the `RayMigrator` brand-token exception (`created_by_raymigrator_version`).

### SQL Server and SQLite

These engines retain PascalCase identifiers for repository tables and columns. SQL Server and SQLite are case-insensitive on identifiers by default. No identifier-casing change is planned for either engine.

### Reader SELECT aliases

The three SELECT templates that feed `ExecuteReaderAsync` for PostgreSQL (`Repository_Migration_Select.sql`, `Repository_MigrationRun_Select.sql`, `Repository_MigrationRun_SelectOrphaned.sql`) emit `AS "PascalCase"` aliases on every output column. The equivalent MariaDB and MySQL templates emit `` AS `PascalCase` `` (backtick-quoted) aliases for the same 36 cross-engine reader keys. This keeps the C# consumers in `TemplateExecutor` and `MigrationService` cross-engine consistent — they continue to read `row["MigrationRunResultId"]` without per-engine branching. The aliases affect only the query result set, not storage.

### Why not enforce one style globally?

Two reasons the repository does **not** pick a single convention and push it across all engines:

1. **Integration with third-party tools.** Each engine's ecosystem (ORMs, admin GUIs, backup utilities, CLI clients) expects its own convention. Forcing snake_case on SQL Server, or PascalCase on PostgreSQL, causes friction in tooling that outweighs the benefit of a uniform naming scheme inside this codebase.
2. **Existing deployments.** SQL Server and SQLite repositories already ship with PascalCase. Forcing snake_case across the board would be a breaking change for every existing user on those engines — with no tool-integration upside to justify it.

The outcome: RayMigrator follows each engine community's idiomatic default (unquoted snake_case for PostgreSQL / MariaDB / MySQL per DAL-017 and DAL-018; PascalCase for SQL Server and SQLite) rather than imposing a house style.

### Policy for new DAL plugins

Third-party DAL plugin authors (starting from `Raycoon.RayMigrator.Database.Example/`) should:

1. **Follow the engine's community convention** for repository-table identifiers. Examples: `UPPER_CASE` for Oracle, `snake_case` for Snowflake / BigQuery, PascalCase for any case-insensitive engine without a stronger convention.
2. **Document the convention in the plugin's README.** A future reader of your templates should not need to reverse-engineer the decision.
3. **Implement the bridge in the DAL, not the other way around.** The framework's C# consumers (`TemplateExecutor`, `MigrationService`) read repository rows via stable PascalCase keys (e.g., `row["MigrationRunResultId"]`). Your reader templates (`Repository_Migration_Select.sql`, `Repository_MigrationRun_Select.sql`, `Repository_MigrationRun_SelectOrphaned.sql`) are responsible for emitting `AS "PascalCase"` / `` AS `PascalCase` `` / `AS [PascalCase]` aliases so the C# layer stays engine-agnostic. Do not push engine-native casing up to the C# layer.

### Historical decision record

Formalized as part of the DAL best-practices audit — see `Docs/todo/dal-best-practices-audit.md` items DAL-017 (PostgreSQL PascalCase → snake_case), DAL-018 (MariaDB / MySQL PascalCase → snake_case), DAL-019 (template-maintenance policy for MariaDB ↔ MySQL divergence), and DAL-025 (this cross-engine casing policy documentation).

## Best Practices

1. **Keep migrations database-agnostic** when possible
2. **Use separate migration files** per database type if needed
3. **Test DDL transactions** on MariaDB and MySQL (DDL causes implicit commits)
4. **Use appropriate escaping** for each database
5. **Use unquoted snake_case** for PostgreSQL, MariaDB, and MySQL repository tables and columns; user migration SQL may still quote PascalCase for case-sensitive user-owned tables
6. **Document dialect-specific SQL** in migration files

## Related Documentation

- [DAL Architecture](dal-architecture.md) - Connection creators
- [Template System](template-system.md) - Templates per database
- [Block Execution](../04-service-layer/block-execution.md) - SQL block parsing, `SplitSqlIntoBlocks`, and block-level execution
- [Adding New Database](adding-new-database.md) - Dialect configuration
