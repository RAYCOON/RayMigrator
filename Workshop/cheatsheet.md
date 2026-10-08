# RayMigrator cheat sheet (0.15.0)

## Commands

Every command needs `-p <Product>` and `-env <Environment>` (or `DOTNET_ENVIRONMENT`). Values are case-sensitive.

| Command | Does | Database access | Useful options |
|---|---|---|---|
| `migrate-up` | applies pending files | targets + repository | `-rm migrate\|simulate\|validate`, `-tr <release>` stop after, `-tg <group>` (repeatable), `-ooo` allow out-of-order, `-tgmo A,B` group order |
| `migrate-down -tr <release>` | rolls back everything above `<release>`; `<release>` stays applied | targets + repository | `-rm simulate\|validate`, `-tg` |
| `validate-hash` | compares stored hashes with the files; exit 1 on Modified or Missing | repository, read-only | `--scope file\|sqlblocks\|disabled`, `-tg` |
| `update-hash` | accepts intentional edits (rewrites the stored hashes) | repository | `-tg` |
| `info` | current release, pending count, last ten runs | repository, read-only | |
| `baseline` | records files as Migrated without executing them | repository | `-tr`, `-tg`, `-tgmo` |
| `fix` | repairs orphaned runs (crashed, still Running) | repository | `-rm simulate`, `-ot <minutes>` (default 60), `-lms` |

Global options: `--config-dir`/`-cd <folder>`, `--startup-info`/`-si false`, `--reveal-sensitive-data`/`-rsd true`. Any option value may be `{ENV:NAME}`.

## Run modes

| `-rm` | Connects to targets | Reads repository | Executes SQL | Writes records |
|---|---|---|---|---|
| `validate` | no | no (every file counts as pending) | no | no |
| `simulate` | yes (connectivity) | yes | no | no |
| `migrate` (default) | yes | yes | yes | yes |

## Exit codes

`0` success · `1` run failed (Error, Recovered or PartialSuccess), hash mismatch, refused parallel run, startup error · `2` `-env` conflicts with `DOTNET_ENVIRONMENT` · `3` no environment · `4` no configuration or no `Serilog` section · `5` command-line parse error · `100` unexpected exception

## Files

```text
{MigrationFilesRootDirectory}/
└── {Release}/                  any name; sorted as text, so zero-pad ("Release 02.0")
    ├── migsettings.txt         optional folder defaults (TOML, [RayMigrator] header line required)
    └── {TargetGroupAlias}/     must equal the alias in the configuration
        ├── NNN_Name.sql                 prefix is only a sort key
        ├── NNN_Name.rollback.sql        required when RequireRollbackFile = true (default)
        └── NNN_Name.{Environment}.sql   runs only in that environment (+ .{Environment}.rollback.sql)
```

Blocks: `GO` alone on a line (SQL Server), `;` alone on a line (PostgreSQL, MariaDB, MySQL, SQLite). A block is the unit of transaction, progress, retry and resume.

## TOML header

```sql
/*
[RayMigrator]
Description = "..."
Environments = ["Production"]      # default: all
Targets = ["MainDB"]               # default: all targets of the group
UseTransaction = true              # per block
RunAlways = false                  # true: re-run every time, must be idempotent, keep in the newest release
RequireRollbackFile = true
MigrationErrorAction = "Terminate" # Terminate | Rollback | RollbackErrorOnly | RollbackRelease | Ignore
RollbackErrorAction = "Terminate"  # Terminate | Ignore
UseCliToolAlias = ""               # empty: built-in execution
*/
```

Precedence: file header › `migsettings.{Environment}.txt` › `migsettings.txt` (nearest folder wins) › product › `ProductDefaults`.

## Configuration

Files in the config folder, merged in this order (later wins; arrays with an `Alias` merge by alias):
`appsettings.json` → `appsettings.{Environment}.json` → `appsettings.{Product}.json` → `appsettings.{Product}.{Environment}.json`

```text
RayMigrator
├── Repository            DatabaseType, ConnectionString, SchemaName (required on SQL Server and PostgreSQL), TableBaseName
├── ProductDefaults       MigrationErrorAction, RollbackErrorAction, RequireRollbackFile, file extension and encoding
│   └── TargetGroupDefaults   TargetMigrationOrder (TargetByTarget | FileByFile), HashValidationScope (File | SqlBlocks | Disabled)
│       └── TargetDefaults    DbCommandTimeoutInSeconds, DbCommandMaxRetries, DbCommandWaitTimeInMsBeforeRetry
├── Products[]            Alias, MigrationFilesRootDirectory (absolute or {ENV:...})
│   └── TargetGroups[]    Alias (= folder name), DatabaseType (SqlServer | PostgreSQL | MariaDb | MySql | Sqlite)
│       └── Targets[]     Alias, ConnectionString
├── CliTools[]            Alias, ExecutablePath, ArgumentTemplate, InputMode (File | Stdin), SuccessExitCodes
├── DatabaseLogging       optional log database
└── Serilog               required
```

## Repository (schema `ray`)

`MigrationRun` (one row per run, also the lock) · `MigrationRunMeta` · `MigrationRecord` (one row per file and target) · `MigrationRecordHistory` · `Product` · `Environment` · `MigratorMeta` · lookups `MigrationRunMode`, `MigrationOperation`, `MigrationRunResult`, `MigrationStatus`

| `MigrationStatusId` | | `MigrationRunResultId` | exit |
|---|---|---|---|
| 10 Pending | | 100 Ok | 0 |
| 20 Executing | | 50 PartialSuccess (Ignore skipped files) | 1 |
| 30 Failed | | 80 Recovered (rollback succeeded) | 1 |
| 50 NotMigrated | | 90 Error | 1 |
| 100 Migrated | | 10 Running (unfinished or orphaned) | |

## Do not

- Edit an applied migration file without `update-hash`: `migrate-up` would run it again.
- Use a relative `MigrationFilesRootDirectory`: it resolves against the executable's folder.
- Put extra dots into file names: everything after a dot is treated as an environment suffix.
- Expect `migrate-down` to clean up a failed run: it only touches `Migrated` records.
- Run two migrations for the same product and environment at once: the second is refused.
- Use `HashValidationScope = Disabled` or `MigrationErrorAction = Ignore` in production.
