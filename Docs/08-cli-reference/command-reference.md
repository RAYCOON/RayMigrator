# Command Reference

Complete reference of all RayMigrator CLI commands, options, valid values, and defaults.

## Quick Reference Matrix

Which options are available for each command. **R** = Required, **O** = Optional, **—** = Not applicable.

### Migration Commands

| Option | migrate-up | migrate-down | validate-hash | update-hash | info | baseline | fix |
|--------|:----------:|:------------:|:-------------:|:-----------:|:----:|:--------:|:---:|
| `--product` | R | R | R | R | R | R | R |
| `--environment` | R | R | R | R | R | R | R |
| `--run-mode` | O | O | — | — | — | — | O |
| `--to-release` | O | R | — | — | — | O | — |
| `--target-group` | O | O | O | O | — | O | — |
| `--allow-out-of-order` | O | — | — | — | — | — | — |
| `--stop-rollback-on-missing-rollback-file` | O | — | — | — | — | — | — |
| `--target-group-migration-order` | O | — | — | — | — | O | — |
| `--cancelled-action` | O | O | — | — | — | O | — |
| `--scope` | — | — | O | — | — | — | O |
| `--older-than` | — | — | — | — | — | — | O |
| `--last-migration-status` | — | — | — | — | — | — | O |
| `--config-dir` | O | O | O | O | O | O | O |
| `--startup-info` | O | O | O | O | O | O | O |
| `--reveal-sensitive-data` | O | O | O | O | O | O | O |

---

## Global Options

→ See [Global Options](global-options.md) for full documentation of `--startup-info`, `--reveal-sensitive-data`, and `--config-dir`.

---

## migrate-up

Apply pending migrations forward.

### Synopsis

```bash
raymigrator migrate-up --product <alias> --environment <env> [--run-mode <mode>] [--to-release <version>] [--target-group <group>...] [--allow-out-of-order] [--stop-rollback-on-missing-rollback-file <bool>] [--target-group-migration-order <order>] [--cancelled-action <action>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--run-mode` | `-rm` | `string` | No | `"Migrate"` | `Migrate`, `Simulate`, `Validate` |
| `--to-release` | `-tr` | `string?` | No | `null` (latest) | Any release version string |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |
| `--allow-out-of-order` | `-ooo` | `bool` | No | `false` | `true`, `false` |
| `--stop-rollback-on-missing-rollback-file` | `-sromrf` | `bool?` | No | `null` (uses config) | `true`, `false` |
| `--target-group-migration-order` | `-tgmo` | `string?` | No | `null` (config/migsettings order) | Comma-separated list of all TargetGroup aliases |
| `--cancelled-action` | `-ca` | `string` | No | `"terminate"` | `terminate`, `rollback-file`, `rollback-run`, `rollback-release` |

### Option Details

**--product**: Matched case-sensitively against product aliases in the configuration. If the value does not match but a case-insensitive match is found, RayMigrator suggests the correct casing before exiting with an error. The run loads only the shared sections and this product of the merged configuration (ADR-026): the other products are neither validated nor resolved, so a fault in one of them does not stop the run. A value that matches none of the configured aliases, in any case, fails while the files are loaded, with the aliases they name.

**--environment**: Matched case-sensitively. Used to load `appsettings.{Environment}.json` and `appsettings.{Product}.{Environment}.json` configuration files.

**--run-mode**: Validated case-insensitively. Accepts `"Migrate"`, `"Simulate"`, and `"Validate"`; any other value produces a validation error. Parsed via `ParseRunMode()` into the corresponding `MigrationRunMode` enum value; `ParseRunMode()` itself throws a `ConfigurationValidationException` for an unknown value instead of silently falling back to `Migrate` (#6).

**--target-group**: Filters execution to specific target groups. Can be specified multiple times to select a subset (e.g., `-tg Backend -tg Frontend`). If omitted, all target groups are processed. Alias matching is case-insensitive. An error is thrown if a specified alias does not exist in the product configuration.

**--allow-out-of-order**: When `true`, allows execution of migration files that precede already-executed files. See [Execution Modes — Out-of-Order](../02-core-concepts/execution-modes.md#out-of-order-migration) for details.

**--stop-rollback-on-missing-rollback-file**: Overrides the `StopRollbackOnMissingRollbackFile` configuration value for this run. Only effective when `RequireRollbackFile=false`. When `true`, the error-recovery rollback chain stops at the first missing rollback file (warning logged, record status unchanged). When `false`, the chain continues past missing rollback files. Has no effect on migrate-down. When omitted, the value from configuration is used.

**--cancelled-action**: What the run does when the first Ctrl+C or SIGTERM cancels it. The run stops at the next block boundary and records `Cancelled`; `terminate` leaves the interrupted file `Failed` with its committed blocks for the next migrate-up, `rollback-file`, `rollback-run` and `rollback-release` then roll back like the `MigrationErrorAction` of the same number. Validated case-insensitively and parsed via `ParseCancelledAction()` into `MigrationCancelledAction`; an unknown value is a parse error. See [Cancelling a Run](global-options.md#cancelling-a-run).

**--target-group-migration-order**: Comma-separated string listing every TargetGroup alias of the product in the desired execution order. Whitespace around each alias is trimmed. All aliases must be present; partial lists, duplicates, and unknown aliases are rejected. Matching is case-sensitive; a case-insensitive-only match produces a specific error with the correct casing as a hint. Takes precedence over both the product-level appsettings value and any release-level migsettings value. Only applies to `migrate-up` and `baseline`; not used for `migrate-down` (rollback order is derived from repository records).

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `MigrateUpRequest` DTO Property |
|------------|--------------------------------------|--------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--run-mode` | `RunMode` | `RunMode` |
| `--to-release` | `TargetReleaseVersion` | `TargetReleaseVersion` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--allow-out-of-order` | `AllowOutOfOrder` | `AllowOutOfOrder` |
| `--stop-rollback-on-missing-rollback-file` | `StopRollbackOnMissingRollbackFile` | — (read directly from context) |
| `--target-group-migration-order` | `TargetGroupMigrationOrder` | `TargetGroupMigrationOrder` |
| `--cancelled-action` | `CancelledAction` | — (the action of `Cancellation.Cancel` on the first Ctrl+C or SIGTERM) |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

`MigrateUpRequest.Cancellation` is the `RunCancellation` of the process, and its token is the `CancellationToken` of the call.

**Handler:** `RayMigratorService.ExecuteMigrateUpAsync()` → `IMigrationService.MigrateUpAsync(MigrateUpRequest, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.MigrateUp`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Basic migration
raymigrator migrate-up --product MyProduct --environment Development --run-mode migrate

# Simulate (dry run)
raymigrator migrate-up -p MyProduct -env Staging -rm simulate

# Migrate to a specific release
raymigrator migrate-up -p MyProduct -env Production -rm migrate -tr "Release 2.0"

# Execute Frontend before Backend (overrides config array order)
raymigrator migrate-up -p MyProduct -env Production -rm migrate -tgmo "Frontend, Backend"

# Debug mode with sensitive data
raymigrator migrate-up -p MyProduct -env Dev -rm migrate --reveal-sensitive-data true

# Suppress startup banner
raymigrator migrate-up -p MyProduct -env Prod -rm migrate --startup-info false
```

---

## migrate-down

Rollback to a previous version.

### Synopsis

```bash
raymigrator migrate-down --product <alias> --environment <env> --to-release <version> [--run-mode <mode>] [--target-group <group>...] [--cancelled-action <action>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--to-release` | `-tr` | `string` | **Yes** | — | Any release version string |
| `--run-mode` | `-rm` | `string` | No | `"Migrate"` | `Migrate`, `Simulate`, `Validate` |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |
| `--cancelled-action` | `-ca` | `string` | No | `"terminate"` | `terminate`, `rollback-file`, `rollback-run`, `rollback-release` |

### Option Details

**--to-release**: Required for migrate-down. Specifies the target release version to roll back to.

**--run-mode**: Same validation as migrate-up. Accepts `"Migrate"`, `"Simulate"`, and `"Validate"`.

**--target-group**: Filters rollback to specific target groups. Can be specified multiple times. If omitted, all target groups are rolled back.

**--cancelled-action**: Accepted with the values of migrate-up so that a script can pass the same options to every run command, and ignored (logged when it is not `terminate`): a cancelled migrate-down stops at the next rollback block, leaves the record in progress `Failed` with its rollback blocks and records the run as `Cancelled`; the next migrate-down continues it, unless its first rollback block failed under `RollbackErrorAction=Ignore`. See [Cancelling a Run](global-options.md#cancelling-a-run).

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `MigrateDownRequest` DTO Property |
|------------|--------------------------------------|----------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--to-release` | `TargetReleaseVersion` | `TargetReleaseVersion` |
| `--run-mode` | `RunMode` | `RunMode` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--cancelled-action` | `CancelledAction` | — (ignored by migrate-down) |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

`MigrateDownRequest.Cancellation` is the `RunCancellation` of the process, and its token is the `CancellationToken` of the call.

**Handler:** `RayMigratorService.ExecuteMigrateDownAsync()` → `IMigrationService.MigrateDownAsync(MigrateDownRequest, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.MigrateDown`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Rollback to Release 1.0
raymigrator migrate-down --product MyProduct --environment Production --to-release "Release 1.0"

# Simulate rollback
raymigrator migrate-down -p MyProduct -env Staging -rm simulate -tr "Release 1.0"

# Short form
raymigrator migrate-down -p MyProduct -env Dev -rm migrate -tr "Release 2.0"
```

---

## validate-hash

Verify migration file integrity by comparing stored hashes against current file content.

### Synopsis

```bash
raymigrator validate-hash --product <alias> --environment <env> [--scope <scope>] [--target-group <group>...] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--scope` | `-s` | `string` | No | (per-TargetGroup config) | `File`, `SqlBlocks`, `Disabled` |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |

### Option Details

**--scope**: Validated case-insensitively. Accepts `"File"`, `"SqlBlocks"` and `"Disabled"`; the former spelling `"SqlBlock"` is rejected (#21). Any other value produces a validation error. If omitted, each TargetGroup uses its configured `HashValidationScope` setting.

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `ValidateHashRequest` DTO Property |
|------------|--------------------------------------|------------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | — (not on DTO) |
| `--scope` | `HashValidationScope` | `HashValidationScope` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteValidateHashAsync()` → `IMigrationService.ValidateHashAsync(ValidateHashRequest, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.ValidateHash`. `RunMode` is set to `MigrationRunMode.Migrate` like for every non-migrate command (#6; it used to be `Validate`). `TargetReleaseVersion` is set to `null`.

**Side effects:** repository read-only, no target connections, no DatabaseLogging rows (see the command profile matrix in [Execution Modes](../02-core-concepts/execution-modes.md#run-mode)).

**Exit code logic:** Returns `1` if `result.InvalidFiles > 0` or `result.MissingFiles > 0`, otherwise `0`; `130` when Ctrl+C or SIGTERM interrupted it.

### Examples

```bash
# Validate file-level hashes
raymigrator validate-hash --product MyProduct --environment Production

# Validate SQL block-level hashes
raymigrator validate-hash -p MyProduct -env Prod -s sqlblocks

# Quiet mode for CI/CD
raymigrator validate-hash -p MyProduct -env Prod --startup-info false
```

---

## update-hash

Update repository hashes after approved changes to migration files.

### Synopsis

```bash
raymigrator update-hash --product <alias> --environment <env> [--target-group <group>...] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `UpdateHashRequest` DTO Property |
|------------|--------------------------------------|----------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | — (not on DTO) |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteUpdateHashAsync()` → `IMigrationService.UpdateHashAsync(UpdateHashRequest, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.UpdateHash`. `RunMode` is set to `MigrationRunMode.Migrate`. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

**Side effects:** repository read/write, no target connections, DatabaseLogging rows are written.

### Examples

```bash
# Update hashes after approved file changes
raymigrator update-hash --product MyProduct --environment Production

# Short form
raymigrator update-hash -p MyProduct -env Prod
```

---

## Info

Display migration status information for a product.

### Synopsis

```bash
raymigrator info --product <alias> --environment <env> [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |

**Handler:** `RayMigratorService.ExecuteInfoAsync()` → `IMigrationService.GetStatusAsync(productAlias, CancellationToken)` + `IMigrationService.GetHistoryAsync(productAlias, 10, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.Info`. `RunMode` is set to `MigrationRunMode.Migrate`. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

**Side effects:** repository read-only (product and environment are looked up, not registered), no target connections, no DatabaseLogging rows. The run history column **Operation** shows `MigrateUp`, `MigrateDown` or `Baseline`.

### Example

```bash
raymigrator info --product MyProduct --environment Production
```

### Output

`info` prints the status of the product in the environment, the files the next migrate-up runs, the target groups and the last ten runs. Every log event starts with the prefix of the console sink, left out here; each of the two tables is one event, so only the empty line above it carries the prefix:

```
--- Migration Status for product MyProduct, environment Production ---
  Current Release:     Release 1.2
  Total Executed:      12
  Pending Migrations:  3

    Release      Target group  Target  File               Reason
    ───────────  ────────────  ──────  ─────────────────  ───────────────────────
    Release 1.2  Backend       Main    010_AddColumn.sql  Resumable after block 2
    Release 1.2  Backend       Main    020_Seed.sql       NotMigrated
    Release 1.2  Frontend      Web     010_Views.sql      HashMismatch
--- Target Groups ---
  [Backend] Type=SqlServer, Release=Release 1.1, Executed=8, Pending=2, LastMigration=2026-10-14 09:12:44, Targets=[Main]
  [Frontend] Type=PostgreSQL, Release=Release 1.2, Executed=4, Pending=1, LastMigration=2026-10-14 09:13:02, Targets=[Web]

--- Last 2 Migration Runs ---
    RunId  Operation     RunMode     Result     # Migrations  StartedAt            FinishedAt           DurationInMs
  ───────  ────────────  ──────────  ─────────  ────────────  ───────────────────  ───────────────────  ────────────
       42  MigrateUp     Migrate     Cancelled             1  2026-10-15 17:40:02  2026-10-15 17:40:09          7012
       41  MigrateUp     Migrate     Ok                   12  2026-10-14 09:12:31  2026-10-14 09:13:02         31188
```

- **Pending Migrations** counts one entry per file and target (`MigrationStatusInfo.PendingMigrations`). The list below it comes from `MigrationStatusInfo.PendingFiles` and is in the order of the next migrate-up without `--target-group-migration-order`: releases, the target groups of a release in their `TargetGroupMigrationOrder`, then file by file or target by target. It shows the first 50 entries and ends with `… and n more` when there are more; a host reads the whole list from `PendingFiles`.
- **Reason** says why the file runs: `NotMigrated` (no record, a rolled back one, or a record of a run that is still open without a committed block), `Failed` (a failed attempt without a resume point), `Resumable after block n` (blocks 1 to n committed; migrate-up continues with block n + 1, or only finalizes the record when every block committed), `RunAlways` (the file runs on every migrate-up, always in full from block 1, also after blocks that an earlier attempt committed), `HashMismatch` (a migrated file whose hash no longer matches), or `RollbackIncomplete after k of n rollback blocks, run migrate-down` (the rollback of the file stopped after k of its n rollback blocks, for example after a cancelled migrate-down; migrate-up refuses to run while such an entry is listed, and migrate-down continues the rollback at block k + 1).
- A run with `--to-release` or `--target-group` runs a part of the list, and files of a release older than what a target has migrated are listed although migrate-up refuses them without `--allow-out-of-order`.
- Every target group line names its pending count and its last migration, the end of its `Migrated` record that finished last (`—` when none has an end, for example with an external DAL whose selects lack `FinishedAt`).
- While a run that ended abruptly is still open, the lines read its records as the orphan repair of the next migrate-up will leave them, and **Reason** classifies them as that migrate-up treats such records: a record with every block committed counts as migrated for `Current Release`, `Total Executed` and its target group; the last migration moves only when the repair has written the end of the record.
- **# Migrations** of the run history counts the records the run brought to a terminal state, read from `MigrationRecordHistory`; a run whose history rows a retention policy deleted shows `0` ([ADR-027](../arc42/09-Architecture-Decisions.md#adr-027-the-engine-deletes-log-and-history-rows-on-a-hosts-request-and-never-reads-them-for-a-decision)).
- A Ctrl+C or SIGTERM ends `info` at once with exit code `130` ([Cancelling a Run](global-options.md#cancelling-a-run)).

---

## Baseline

Mark an existing database as migrated (all releases, or up to a specific release).

### Synopsis

```bash
raymigrator baseline --product <alias> --environment <env> [--to-release <version>] [--target-group <group>...] [--target-group-migration-order <order>] [--cancelled-action <action>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--to-release` | `-tr` | `string` | No | — | Any release version string (omit to baseline all releases) |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |
| `--target-group-migration-order` | `-tgmo` | `string?` | No | `null` (config/migsettings order) | Comma-separated list of all TargetGroup aliases |
| `--cancelled-action` | `-ca` | `string` | No | `"terminate"` | `terminate`, `rollback-file`, `rollback-run`, `rollback-release` |

### Option Details

**--cancelled-action**: Accepted with the values of migrate-up so that a script can pass the same options to every run command, and ignored (logged when it is not `terminate`): a cancelled baseline stops before the next file, keeps the records it wrote and records the run as `Cancelled`. See [Cancelling a Run](global-options.md#cancelling-a-run).

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `BaselineRequest` DTO Property |
|------------|--------------------------------------|-------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--to-release` | `TargetReleaseVersion` | `TargetReleaseVersion` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--target-group-migration-order` | `TargetGroupMigrationOrder` | `TargetGroupMigrationOrder` |
| `--cancelled-action` | `CancelledAction` | — (ignored by baseline) |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

`BaselineRequest.Cancellation` is the `RunCancellation` of the process, and its token is the `CancellationToken` of the call.

**Handler:** `RayMigratorService.ExecuteBaselineAsync()` → `IMigrationService.BaselineAsync(BaselineRequest, CancellationToken)`

**Internal state:** `Command` is set to `MigrationCommand.Baseline`. `RunMode` is set to `MigrationRunMode.Migrate`. `HashValidationScope` is set to `null`.

**Side effects:** repository read/write (MigrationRun and MigrationRecord rows stamped `MigrationOperation.Baseline`), no target connections, DatabaseLogging rows are written.

### Usage Scenarios

**Simple onboarding** — database is fully up-to-date, baseline all releases:

```bash
raymigrator baseline --product MyProduct --environment Production
```

**Partial onboarding** — database is at Release 2.0, newer releases should still be executed:

```bash
raymigrator baseline --product MyProduct --environment Production --to-release "Release 2.0"
```

**Multi-environment** — Dev is at Release 3.0, Prod at Release 2.0, same migration files:

```bash
raymigrator baseline --product MyProduct --environment Dev --to-release "Release 3.0"
raymigrator baseline --product MyProduct --environment Prod --to-release "Release 2.0"
```

---

## Fix

Fix repository inconsistencies such as orphaned migration runs (process crashed while Running).

### Synopsis

```bash
raymigrator fix --product <alias> --environment <env> [--scope <scope>] [--older-than <minutes>] [--run-mode <mode>] [--last-migration-status <status>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--scope` | `-s` | `string` | No | `"OrphanedRuns"` | `OrphanedRuns`, `All` |
| `--older-than` | `-ot` | `int` | No | `2` | Minutes since the last heartbeat (or the start) for orphan detection |
| `--run-mode` | `-rm` | `string` | No | `"migrate"` | `migrate`, `simulate` |
| `--last-migration-status` | `-lms` | `string` | No | — | `migrated`, `not-migrated` |

### Option Details

**--scope**: Validated case-insensitively. Only `"All"` and `"OrphanedRuns"` are accepted. Parsed via `ParseFixScope()` into `FixScope.All` or `FixScope.OrphanedRuns`; `All` runs every known repair (currently the orphaned-run repair) and the result lists the repairs that ran (#16).

**--older-than**: Minimum age of the last heartbeat (or of the start, for a run that never wrote one) in minutes for an open MigrationRun to be considered orphaned; `0` repairs every open run. Default is 2 minutes, the `Repository.OrphanedRunThresholdInMinutes` default. A run whose heartbeat is younger is alive and left open even when the age is passed on the command line.

**--run-mode**: `migrate` repairs the orphaned runs; `simulate` lists the runs that would be repaired, writes nothing and leaves no DatabaseLogging rows. `validate` is rejected for `fix` (#22).

**--last-migration-status**: Without the option every open (`Pending`, `Executing`) record of an orphaned run keeps its progress: `Failed` after committed blocks, so that the next migrate-up resumes after them, `Migrated` when every block committed, `NotMigrated` without progress; a rollback that was running ends `Failed` with its rollback blocks, which migrate-down continues, or `NotMigrated` when every rollback block ran. `migrated` (`MigrationStatus.Migrated`) and `not-migrated` (`MigrationStatus.NotMigrated`) set that status on every open record of the run, for an operator who has checked the target. The counters of the records stay; the repair sets `FinishedAt` and leaves `DurationInMs` empty.

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `FixIssuesRequest` DTO Property |
|------------|--------------------------------------|--------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--scope` | `FixScope` | `Scope` |
| `--older-than` | `FixOlderThanMinutes` | `OlderThanMinutes` |
| `--run-mode` | `RunMode` | `RunMode` |
| `--last-migration-status` | `FixAssumedMigrationStatus` | `AssumedMigrationStatus` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteFixIssuesAsync()` → `IMigrationService.FixIssuesAsync(FixIssuesRequest, CancellationToken)`

### Internal State

`Command` is set to `MigrationCommand.FixIssues`. `RunMode` is `MigrationRunMode.Migrate` unless `--run-mode simulate` is given. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

**Side effects:** repository read/write, no target connections, DatabaseLogging rows are written. With `--run-mode simulate` the command is read-only and writes no DatabaseLogging rows.

### Examples

```bash
# Fix orphaned runs (default scope)
raymigrator fix --product MyProduct --environment Production

# Fix all repository issues
raymigrator fix -p MyProduct -env Prod -s all

# Simulate to see what would be fixed
raymigrator fix -p MyProduct -env Prod --run-mode simulate

# Fix with custom age threshold and migration status
raymigrator fix -p MyProduct -env Prod -ot 120 -lms migrated
```

---

## Enum Reference

### MigrationCommand

Represents the CLI command to execute.

| Name | Value | CLI Command |
|------|-------|-------------|
| `None` | 0 | — |
| `MigrateUp` | 1 | `migrate-up` |
| `MigrateDown` | 2 | `migrate-down` |
| `ValidateHash` | 3 | `validate-hash` |
| `UpdateHash` | 4 | `update-hash` |
| `info` | 5 | `info` |
| `baseline` | 6 | `baseline` |
| `FixIssues` | 7 | `fix` |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationCommand.cs`

### MigrationRunMode

Controls the execution behavior.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — RunMode has not been set properly |
| `Validate` | 10 | Validates configuration and migration files; does not connect to target databases or repository database |
| `Simulate` | 20 | Validates, checks DB connectivity, reads repository records; does not write repository records or execute SQL against target databases |
| `Migrate` | 100 | Validates, then performs actual migrations against target databases |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationRunMode.cs`

`--run-mode` exists on `migrate-up`, `migrate-down` and `fix` (`fix` accepts `migrate` and `simulate`). Every other command runs in `Migrate` mode and decides its side effects through its `CommandProfile` (`MigrationCommandExtensions.GetProfile()`, see [Execution Modes](../02-core-concepts/execution-modes.md#run-mode)). A `MigrationContext` rejects `Undefined` (and `MigrationCommand.None`) with a `ConfigurationValidationException`.

### HashValidationScope

Controls the granularity of hash validation. See [Hash Validation](../02-core-concepts/hash-validation.md) for the detailed reference.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — scope has not been set |
| `File` | 1 | Compare hash of entire migration file |
| `SqlBlocks` | 2 | Compare hash of SQL content only (excluding TOML metadata) |
| `Disabled` | 3 | Skip hash validation |

The CLI accepts only these three names, case-insensitively; the former spelling `SqlBlock` is rejected (#21).

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/HashValidationScope.cs`

### MigrationErrorAction

Controls error handling behavior when a migration fails.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — not set |
| `Terminate` | 10 | Stop immediately, no rollback |
| `Rollback` | 20 | Rollback all migrations in current run |
| `RollbackErrorOnly` | 21 | Rollback only the failed migration |
| `RollbackRelease` | 22 | Rollback the current run's migrations of the failed release |
| `Ignore` | 30 | Ignore error, continue with next file |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationErrorAction.cs`

See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior of each mode.

### MigrationCancelledAction

What a cancelled migrate-up does after it stopped at the next block boundary (`--cancelled-action`). A value of the run, not a configuration option; the numbers are those of the `MigrationErrorAction` whose rollback chain it runs.

| Name | Value | CLI value | Description |
|------|-------|-----------|-------------|
| `Undefined` | 0 | — | Invalid — not set |
| `Terminate` | 10 | `terminate` | Stop and do nothing else; the next migrate-up resumes the interrupted file (default) |
| `RollbackRun` | 20 | `rollback-run` | Roll back the interrupted file and every file the run migrated before it, like `Rollback` |
| `RollbackFile` | 21 | `rollback-file` | Roll back the interrupted file only, like `RollbackErrorOnly` |
| `RollbackRelease` | 22 | `rollback-release` | Roll back the interrupted file and the files of its release the run migrated, like `RollbackRelease` |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationCancelledAction.cs`

See [Global Options — Cancelling a Run](global-options.md#cancelling-a-run).

### RollbackErrorAction

Controls error handling behavior when a rollback SQL block fails. Applies both during explicit `migrate-down` execution and during error recovery rollback triggered by `MigrationErrorAction` (Rollback, RollbackErrorOnly, or RollbackRelease) in `migrate-up`.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set (resolves to Terminate at runtime) |
| `Terminate` | 10 | Abort the entire rollback chain immediately (default) |
| `Ignore` | 30 | Skip the failed block, continue with remaining blocks, mark file as Failed, then continue with next file |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/RollbackErrorAction.cs`

See [migrate-down -- RollbackErrorAction](migrate-down.md#rollbackerroraction) and [Error Handling](../02-core-concepts/error-handling.md) for details.

### TargetMigrationOrder

Controls target iteration order within a TargetGroup.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — not set |
| `FileByFile` | 1 | Execute on all targets per migration (file → target) |
| `TargetByTarget` | 2 | Complete all migrations per target (target → file) |

The former names `Simultaneously` and `Successively` (before 0.13.0) are rejected with the usual `Allowed values: [FileByFile, TargetByTarget]` error; rename them in existing configuration files (#19).

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/TargetMigrationOrder.cs`

See [Execution Modes](../02-core-concepts/execution-modes.md#target-migration-order) for detailed behavior and examples.

### FixScope

Controls the scope of repository issue resolution.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — scope has not been set |
| `All` | 1 | Fix all known problems in repository |
| `OrphanedRuns` | 2 | Fix only orphaned MigrationRun entries |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/FixScope.cs`

### CliToolInputMode

Determines how the migration SQL file is passed to an external CLI tool when CLI tool integration is enabled via `UseCliToolAlias` configuration.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Not explicitly set; falls back to `File` behavior at runtime |
| `File` | 1 | The file path is passed as a command-line argument via the `{FilePath}` placeholder in `ArgumentTemplate` |
| `Stdin` | 2 | The file content is piped to the process via standard input (`Process.StandardInput`) |

`File` is used by tools like `sqlcmd` (`-i`), `psql` (`-f`), `sqlite3` (`-init`). `Stdin` is used by tools like `mysql` and `mariadb` that read SQL from stdin.

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/CliToolInputMode.cs`

### MigrationStatus

Represents the status of a single migration record in the repository. The `--last-migration-status` option on the Fix command accepts `migrated` and `not-migrated` to set that status on every open record of an orphaned run; without it each record keeps the progress of its file.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Pending` | 10 | Migration record created, execution has not started yet |
| `Executing` | 20 | SQL blocks are currently being executed |
| `Failed` | 30 | Execution failed, database state is unclear |
| `NotMigrated` | 50 | File is not deployed on target database (rolled back or never executed) |
| `Migrated` | 100 | File is successfully deployed on target database |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationStatus.cs`

### MigrationOperation

Represents the type of migration operation (displayed in the Info command's migration run history table).

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Rollback` | 5 | Performing rollback (error recovery during migrate-up) |
| `MigrateDown` | 50 | Performing down-migration (explicit migrate-down command) |
| `MigrateUp` | 100 | Performing up-migration (migrate-up command) |
| `Baseline` | 110 | Marking migration files as migrated without executing them (baseline command) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationOperation.cs`

### MigrationRunResult

Represents the final result of a MigrationRun record (displayed by the Info command).

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Running` | 10 | Migration process is currently running |
| `PartialSuccess` | 50 | Finished, but at least one file was skipped or left Failed (migrate-up with `Ignore`, migrate-down that skipped a missing rollback file or ignored a failed rollback) |
| `Cancelled` | 70 | Stopped on request (Ctrl+C or SIGTERM in the CLI) and the `MigrationCancelledAction` completed; exit code 130 |
| `Recovered` | 80 | Failed, but the configured error recovery (`Rollback`, `RollbackRelease`, `RollbackErrorOnly`) rolled back without a failure or warning; repository and database are consistent. Exit code stays 1 |
| `Error` | 90 | Migration(s) stopped due to error(s), or the error recovery did not complete cleanly, also when Ctrl+C or SIGTERM stopped it (with the code of the failure, exit code 1), or the rollback of a cancelled migrate-up did not complete cleanly (engine code `1005`, exit code 1) |
| `Ok` | 100 | Migration(s) successfully executed and finished |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationRunResult.cs`

---

## Environment Handling

### Environment Resolution

The `--environment` option is **required** for all commands (migrate-up, migrate-down, validate-hash, update-hash, info, baseline, fix); omitting it is a parse error and returns the parser's exit code `1`. `DOTNET_ENVIRONMENT` is cross-checked against the option value: when both are set, they must match. The variable never replaces the option.

### Environment Sources

`EnvironmentResolver` takes the environment from one source and checks it against the other:

1. **CLI argument**: `--environment` / `-env` (the environment of the run)
2. **Environment variable**: `DOTNET_ENVIRONMENT` (cross-check only)

**Resolution rules:**

- `--environment` has a value and `DOTNET_ENVIRONMENT` is not set → use the option value.
- Both are set with **the same value** → use that value.
- Both are set with **different values** → **exit code 2** with an error message.
- `--environment` is empty (or whitespace) → **exit code 3** with an error message, whether or not `DOTNET_ENVIRONMENT` is set. The message names the empty argument and, when the variable is set, states that it does not replace the argument.

To take the environment from a variable, pass it through the placeholder: `-env {ENV:DOTNET_ENVIRONMENT}` (the launch profiles do this). The parser resolves the placeholder, so the value counts as given on the command line.

---

## Environment Variable Substitution

→ See [Global Options — Environment Variable Support](global-options.md#environment-variable-support) for `{ENV:VARIABLE_NAME}` syntax.

## Exit Codes

→ See [Global Options — Exit Codes](global-options.md#exit-codes) for the complete exit code table.

## Configuration File Loading Order

→ See [Configuration Hierarchy](../06-configuration-reference/appsettings-hierarchy.md) for the 4-level file merge order and merge rules.

---

## Related Documentation

| Document | Description |
|----------|-------------|
| [migrate-up](migrate-up.md) | Detailed migrate-up command page with execution flow |
| [migrate-down](migrate-down.md) | Detailed migrate-down command page with rollback flow |
| [validate-hash](validate-hash.md) | Detailed validate-hash command page |
| [update-hash](update-hash.md) | Detailed update-hash command page |
| [Global Options](global-options.md) | Global options, logging, and best practices |
| [Configuration System](../02-core-concepts/configuration-system.md) | Options pattern hierarchy |
| [Environment Variables](../06-configuration-reference/environment-variables.md) | `{ENV:}` placeholder reference |
| [Error Handling](../02-core-concepts/error-handling.md) | Error actions and strategies |
