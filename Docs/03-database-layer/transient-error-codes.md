# Transient Error Codes

Every DAL retries a database command when the provider reports an error it classifies as transient; the retry settings (`DbCommandMaxRetries`, `DbCommandWaitTimeInMsBeforeRetry`) are described in [Resilience](../02-core-concepts/resilience.md). Which error codes count as transient is a text file next to the DAL, so an operator can adjust the list without waiting for a release (ADR-022).

## Location

`DataAccessLayers/{DatabaseType}/TransientErrorCodes.txt`, next to the DAL assembly and the SQL templates below the folder of the `raymigrator` executable. For NuGet consumers such as RayMigrator Studio the file is part of the package's `contentFiles` and lands in the `DataAccessLayers/` folder of the application output. RayMigrator ships one file per built-in engine:

| DatabaseType | File |
|---|---|
| SqlServer | `DataAccessLayers/SqlServer/TransientErrorCodes.txt` |
| PostgreSQL | `DataAccessLayers/PostgreSQL/TransientErrorCodes.txt` |
| MariaDb | `DataAccessLayers/MariaDb/TransientErrorCodes.txt` |
| MySql | `DataAccessLayers/MySql/TransientErrorCodes.txt` |
| Sqlite | `DataAccessLayers/Sqlite/TransientErrorCodes.txt` |

## Format

```text
# RayMigrator transient error codes for SqlServer.
# One code per line. A line starting with # is a comment; a trailing # comment is allowed. This file is the
# complete list: a code not listed here is not retried. Delete the file to fall back to the built-in list.
-2       # Timeout expired
233      # Connection closed during initialization
4021     # Pooled session reset failed after ALTER LOGIN
596      # Session is in the kill state
```

- One code per line. Every line is trimmed and a `#` comment (whole line or trailing) is stripped; blank lines are ignored.
- Codes are compared case-insensitively, so PostgreSQL SQLSTATEs such as `57P01` may be written in either case.
- A code must not contain whitespace. A line such as `4021 x` aborts the start with a `ConfigurationValidationException` that names the file and the line.

## Semantics

- **Replace, not merge.** The file is the complete list. A code that is not listed is not retried, even when the built-in list contains it.
- **Missing file.** The DAL keeps its built-in list (`DalBase.DefaultTransientErrorCodes`). Delete the file to return to the defaults.
- **Empty file.** An existing file without codes means that no database error is retried; RayMigrator logs a Warning at start.
- **When it is read.** `DalFactory` reads the file once when it creates the DAL instance, i.e. at application start. A change needs a restart.
- **Logging.** `DirectModePipeline` logs the source once per DatabaseType: Information when the file was loaded (the full path only with `--reveal-sensitive-data`), Debug for the built-in list, Warning when the effective list is empty.
- **Upgrades.** Every publish or upgrade overwrites `DataAccessLayers/`, so edits must be re-applied after an update, exactly like edited SQL templates.

## Built-in lists

The shipped files equal the built-in lists; `P1_DalShippedTransientErrorCodesTests` guards against drift between file and code.

| Engine (code source) | Codes |
|---|---|
| SQL Server (`SqlException.Number`) | `-2`, `20`, `64`, `233`, `10053`, `10054`, `10060`, `40197`, `40501`, `40613`, `49918`, `49919`, `49920`, `4021`, `596` |
| PostgreSQL (`PostgresException.SqlState`) | `08000`, `08003`, `08006`, `08001`, `08004`, `57P01`, `57P02`, `57P03`, `40001`, `40P01` |
| MariaDB (`MySqlException.Number`) | `1040`, `1205`, `1213`, `1614`, `2002`, `2003`, `2006`, `2013`, `2055` |
| MySQL (`MySqlException.Number`) | the same values as MariaDB, kept as a separate file so the two dialects can diverge |
| SQLite (`SqliteException.SqliteErrorCode`) | `5` (SQLITE_BUSY), `6` (SQLITE_LOCKED) |

`TimeoutException` is transient for every engine regardless of the list, and inner exceptions are inspected recursively (`DalBase.IsTransient`).

## Not configurable: the SQL Server pool-clearing list

Errors 4021 and 596 additionally make the SQL Server DAL clear its connection pool (`DalSqlServer.PoolInvalidatingCodes`). That list is a mechanism, not a retry policy, and stays in code; see [Login Altered by a Migration](../appendix/troubleshooting.md#login-altered-by-a-migration-sql-server-error-4021).

## Exit codes on a malformed file

A malformed or unreadable file aborts the start. For the repository and target DALs the error surfaces as `ConfigurationValidationException` with exit code `100`; for the DatabaseLogging DAL, which is created before the pipeline logger exists, it is wrapped into `ApplicationStartupException` and exits with `1`. Both messages name the file and the line.

## For DAL authors

A plugin overrides `DefaultTransientErrorCodes` with its built-in list and classifies the provider's error code through `IsTransientCode` in its `IsTransient` override; `DalFactory` then loads an optional `TransientErrorCodes.txt` from the plugin folder automatically. See [External DAL Development](../09-extending/external-dal-development.md#overriding-istransient).

## Related Documentation

- [Resilience](../02-core-concepts/resilience.md) - Retry settings and behaviour
- [Error Handling](../02-core-concepts/error-handling.md#transient-error-retry) - Transient error retry
- [DAL Architecture](dal-architecture.md) - `DalBase` and `RetryHelper`
- [Troubleshooting](../appendix/troubleshooting.md#transient-database-error-after-retries) - Diagnosing exhausted retries
- ADR-022 in [Architecture Decisions](../arc42/09-Architecture-Decisions.md)
