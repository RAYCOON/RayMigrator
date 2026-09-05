# Command Reference

Complete reference of all RayMigrator CLI commands, options, valid values, and defaults.

## Quick Reference Matrix

Which options are available for each command. **R** = Required, **O** = Optional, **—** = Not applicable.

### Migration Commands

| Option | Migrate-Up | Migrate-Down | Validate-Hash | Update-Hash | Info | Baseline | Fix |
|--------|:----------:|:------------:|:-------------:|:-----------:|:----:|:--------:|:---:|
| `--product` | R | R | R | R | R | R | R |
| `--environment` | R | R | R | R | R | R | R |
| `--run-mode` | O | O | — | — | — | — | — |
| `--to-release` | O | R | — | — | — | O | — |
| `--target-group` | O | O | O | O | — | O | — |
| `--allow-out-of-order` | O | — | — | — | — | — | — |
| `--stop-rollback-on-missing-rollback-file` | O | — | — | — | — | — | — |
| `--TargetGroup-MigrationOrder` | O | — | — | — | — | O | — |
| `--scope` | — | — | O | — | — | — | O |
| `--older-than` | — | — | — | — | — | — | O |
| `--dry-run` | — | — | — | — | — | — | O |
| `--last-migration-status` | — | — | — | — | — | — | O |
| `--config-dir` | O | O | O | O | O | O | O |
| `--startup-info` | O | O | O | O | O | O | O |
| `--reveal-sensitive-data` | O | O | O | O | O | O | O |

---

## Global Options

→ See [Global Options](global-options.md) for full documentation of `--startup-info`, `--reveal-sensitive-data`, and `--config-dir`.

---

## Migrate-Up

Apply pending migrations forward.

### Synopsis

```bash
raymigrator Migrate-Up --product <alias> --environment <env> [--run-mode <mode>] [--to-release <version>] [--target-group <group>...] [--allow-out-of-order] [--stop-rollback-on-missing-rollback-file <bool>] [--TargetGroup-MigrationOrder <order>] [global-options]
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
| `--TargetGroup-MigrationOrder` | `-tgmo` | `string?` | No | `null` (config/migsettings order) | Comma-separated list of all TargetGroup aliases |

### Option Details

**--product**: Matched case-sensitively against product aliases in the configuration. If the value does not match but a case-insensitive match is found, RayMigrator suggests the correct casing before exiting with an error.

**--environment**: Matched case-sensitively. Used to load `appsettings.{Environment}.json` and `appsettings.{Product}.{Environment}.json` configuration files.

**--run-mode**: Validated case-insensitively. Accepts `"Migrate"`, `"Simulate"`, and `"Validate"`; any other value produces a validation error. Parsed via `ParseRunMode()` into the corresponding `MigrationRunMode` enum value.

**--target-group**: Filters execution to specific target groups. Can be specified multiple times to select a subset (e.g., `-tg Backend -tg Frontend`). If omitted, all target groups are processed. Alias matching is case-insensitive. An error is thrown if a specified alias does not exist in the product configuration.

**--allow-out-of-order**: When `true`, allows execution of migration files that precede already-executed files. See [Execution Modes — Out-of-Order](../02-core-concepts/execution-modes.md#out-of-order-migration) for details.

**--stop-rollback-on-missing-rollback-file**: Overrides the `StopRollbackOnMissingRollbackFile` configuration value for this run. Only effective when `RequireRollbackFile=false`. When `true`, the error-recovery rollback chain stops at the first missing rollback file (warning logged, record status unchanged). When `false`, the chain continues past missing rollback files. Has no effect on Migrate-Down. When omitted, the value from configuration is used.

**--TargetGroup-MigrationOrder**: Comma-separated string listing every TargetGroup alias of the product in the desired execution order. Whitespace around each alias is trimmed. All aliases must be present; partial lists, duplicates, and unknown aliases are rejected. Matching is case-sensitive; a case-insensitive-only match produces a specific error with the correct casing as a hint. Takes precedence over both the product-level appsettings value and any release-level migsettings value. Only applies to `Migrate-Up` and `Baseline`; not used for `Migrate-Down` (rollback order is derived from repository records).

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
| `--TargetGroup-MigrationOrder` | `TargetGroupMigrationOrder` | `TargetGroupMigrationOrder` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteMigrateUpAsync()` → `IMigrationService.MigrateUpAsync(MigrateUpRequest)`

**Internal state:** `Command` is set to `MigrationCommand.MigrateUp`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Basic migration
raymigrator Migrate-Up --product MyProduct --environment Development --run-mode Migrate

# Simulate (dry run)
raymigrator Migrate-Up -p MyProduct -env Staging -rm Simulate

# Migrate to a specific release
raymigrator Migrate-Up -p MyProduct -env Production -rm Migrate -tr "Release 2.0"

# Execute Frontend before Backend (overrides config array order)
raymigrator Migrate-Up -p MyProduct -env Production -rm Migrate -tgmo "Frontend, Backend"

# Debug mode with sensitive data
raymigrator Migrate-Up -p MyProduct -env Dev -rm Migrate --reveal-sensitive-data true

# Suppress startup banner
raymigrator Migrate-Up -p MyProduct -env Prod -rm Migrate --startup-info false
```

---

## Migrate-Down

Rollback to a previous version.

### Synopsis

```bash
raymigrator Migrate-Down --product <alias> --environment <env> --to-release <version> [--run-mode <mode>] [--target-group <group>...] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--to-release` | `-tr` | `string` | **Yes** | — | Any release version string |
| `--run-mode` | `-rm` | `string` | No | `"Migrate"` | `Migrate`, `Simulate`, `Validate` |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |

### Option Details

**--to-release**: Required for Migrate-Down. Specifies the target release version to roll back to.

**--run-mode**: Same validation as Migrate-Up. Accepts `"Migrate"`, `"Simulate"`, and `"Validate"`.

**--target-group**: Filters rollback to specific target groups. Can be specified multiple times. If omitted, all target groups are rolled back.

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `MigrateDownRequest` DTO Property |
|------------|--------------------------------------|----------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--to-release` | `TargetReleaseVersion` | `TargetReleaseVersion` |
| `--run-mode` | `RunMode` | `RunMode` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteMigrateDownAsync()` → `IMigrationService.MigrateDownAsync(MigrateDownRequest)`

**Internal state:** `Command` is set to `MigrationCommand.MigrateDown`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Rollback to Release 1.0
raymigrator Migrate-Down --product MyProduct --environment Production --to-release "Release 1.0"

# Simulate rollback
raymigrator Migrate-Down -p MyProduct -env Staging -rm Simulate -tr "Release 1.0"

# Short form
raymigrator Migrate-Down -p MyProduct -env Dev -rm Migrate -tr "Release 2.0"
```

---

## Validate-Hash

Verify migration file integrity by comparing stored hashes against current file content.

### Synopsis

```bash
raymigrator Validate-Hash --product <alias> --environment <env> [--scope <scope>] [--target-group <group>...] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--scope` | `-s` | `string` | No | (per-TargetGroup config) | `File`, `SqlBlock`, `SqlBlocks`, `Disabled` |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |

### Option Details

**--scope**: Validated case-insensitively. Accepts `"File"`, `"SqlBlock"`, `"SqlBlocks"`, and `"Disabled"`. Both `"SqlBlock"` and `"SqlBlocks"` map to `HashValidationScope.SqlBlocks`. Any other value produces a validation error. If omitted, each TargetGroup uses its configured `HashValidationScope` setting.

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `ValidateHashRequest` DTO Property |
|------------|--------------------------------------|------------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | — (not on DTO) |
| `--scope` | `HashValidationScope` | `HashValidationScope` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteValidateHashAsync()` → `IMigrationService.ValidateHashAsync(ValidateHashRequest)`

**Internal state:** `Command` is set to `MigrationCommand.ValidateHash`. `RunMode` is forced to `MigrationRunMode.Validate`. `TargetReleaseVersion` is set to `null`.

**Exit code logic:** Returns `1` if `result.InvalidFiles > 0` or `result.MissingFiles > 0`, otherwise `0`.

### Examples

```bash
# Validate file-level hashes
raymigrator Validate-Hash --product MyProduct --environment Production

# Validate SQL block-level hashes
raymigrator Validate-Hash -p MyProduct -env Prod -s SqlBlock

# Quiet mode for CI/CD
raymigrator Validate-Hash -p MyProduct -env Prod --startup-info false
```

---

## Update-Hash

Update repository hashes after approved changes to migration files.

### Synopsis

```bash
raymigrator Update-Hash --product <alias> --environment <env> [--target-group <group>...] [global-options]
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

**Handler:** `RayMigratorService.ExecuteUpdateHashAsync()` → `IMigrationService.UpdateHashAsync(UpdateHashRequest)`

**Internal state:** `Command` is set to `MigrationCommand.UpdateHash`. `RunMode` is forced to `MigrationRunMode.Migrate`. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Update hashes after approved file changes
raymigrator Update-Hash --product MyProduct --environment Production

# Short form
raymigrator Update-Hash -p MyProduct -env Prod
```

---

## Info

Display migration status information for a product.

### Synopsis

```bash
raymigrator Info --product <alias> --environment <env> [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |

**Handler:** `RayMigratorService.ExecuteInfoAsync()` → `IMigrationService.GetStatusAsync(productAlias)` + `IMigrationService.GetHistoryAsync(productAlias, 10)`

**Internal state:** `Command` is set to `MigrationCommand.Info`. `RunMode` is set to `MigrationRunMode.Migrate`. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

### Example

```bash
raymigrator Info --product MyProduct --environment Production
```

---

## Baseline

Mark an existing database as migrated (all releases, or up to a specific release).

### Synopsis

```bash
raymigrator Baseline --product <alias> --environment <env> [--to-release <version>] [--target-group <group>...] [--TargetGroup-MigrationOrder <order>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--to-release` | `-tr` | `string` | No | — | Any release version string (omit to baseline all releases) |
| `--target-group` | `-tg` | `string[]` | No | `null` (all) | Target group aliases (can be specified multiple times) |
| `--TargetGroup-MigrationOrder` | `-tgmo` | `string?` | No | `null` (config/migsettings order) | Comma-separated list of all TargetGroup aliases |

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `BaselineRequest` DTO Property |
|------------|--------------------------------------|-------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--to-release` | `TargetReleaseVersion` | `TargetReleaseVersion` |
| `--target-group` | `TargetGroupAliases` | `TargetGroupAliases` |
| `--TargetGroup-MigrationOrder` | `TargetGroupMigrationOrder` | `TargetGroupMigrationOrder` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteBaselineAsync()` → `IMigrationService.BaselineAsync(BaselineRequest)`

**Internal state:** `Command` is set to `MigrationCommand.Baseline`. `RunMode` is set to `MigrationRunMode.Migrate`. `HashValidationScope` is set to `null`.

### Usage Scenarios

**Simple onboarding** — database is fully up-to-date, baseline all releases:

```bash
raymigrator Baseline --product MyProduct --environment Production
```

**Partial onboarding** — database is at Release 2.0, newer releases should still be executed:

```bash
raymigrator Baseline --product MyProduct --environment Production --to-release "Release 2.0"
```

**Multi-environment** — Dev is at Release 3.0, Prod at Release 2.0, same migration files:

```bash
raymigrator Baseline --product MyProduct --environment Dev --to-release "Release 3.0"
raymigrator Baseline --product MyProduct --environment Prod --to-release "Release 2.0"
```

---

## Fix

Fix repository inconsistencies such as orphaned migration runs (process crashed while Running).

### Synopsis

```bash
raymigrator Fix --product <alias> --environment <env> [--scope <scope>] [--older-than <minutes>] [--dry-run] [--last-migration-status <status>] [global-options]
```

### Options

| Option | Alias | Type | Required | Default | Valid Values |
|--------|-------|------|----------|---------|-------------|
| `--product` | `-p` | `string` | Yes | — | Any product alias from configuration |
| `--environment` | `-env` | `string` | Yes | — | Any environment name |
| `--scope` | `-s` | `string` | No | `"OrphanedRuns"` | `OrphanedRuns`, `All` |
| `--older-than` | `-ot` | `int` | No | `60` | Minutes threshold for orphan detection |
| `--dry-run` | — | `bool` | No | `false` | `true`, `false` |
| `--last-migration-status` | `-lms` | `string` | No | `"not-migrated"` | `migrated`, `not-migrated` |

### Option Details

**--scope**: Validated case-insensitively. Only `"All"` and `"OrphanedRuns"` are accepted. Parsed via `ParseFixIssuesScope()` into `FixIssues.All` or `FixIssues.OrphanedRuns`.

**--older-than**: Minimum age in minutes for a MigrationRun to be considered orphaned. Default is 60 minutes.

**--dry-run**: When `true`, reports what would be fixed without making changes.

**--last-migration-status**: Determines the `MigrationStatus` to assign to orphaned Migration records during fix. `"not-migrated"` maps to `MigrationStatus.NotMigrated`, `"migrated"` maps to `MigrationStatus.Migrated`.

### Property Mapping

| CLI Option | `RayMigratorConsoleOptions` Property | `FixIssuesRequest` DTO Property |
|------------|--------------------------------------|--------------------------------|
| `--product` | `Product` | `ProductAlias` |
| `--environment` | `Environment` | `Environment` |
| `--scope` | `FixIssues` | `Scope` |
| `--older-than` | `FixOlderThanMinutes` | `OlderThanMinutes` |
| `--dry-run` | `FixDryRun` | `DryRun` |
| `--last-migration-status` | `FixAssumedMigrationStatus` | `AssumedMigrationStatus` |
| `--startup-info` | `ShowStartupInfo` | `ShowInfo` |
| `--reveal-sensitive-data` | `RevealSensitiveData` | `RevealSensitiveData` |

**Handler:** `RayMigratorService.ExecuteFixIssuesAsync()` → `IMigrationService.FixIssuesAsync(FixIssuesRequest)`

### Internal State

`Command` is set to `MigrationCommand.FixIssues`. `RunMode` is set to `MigrationRunMode.Migrate`. `TargetReleaseVersion` is set to `null`. `HashValidationScope` is set to `null`.

### Examples

```bash
# Fix orphaned runs (default scope)
raymigrator Fix --product MyProduct --environment Production

# Fix all repository issues
raymigrator Fix -p MyProduct -env Prod -s All

# Dry run to see what would be fixed
raymigrator Fix -p MyProduct -env Prod --dry-run true

# Fix with custom age threshold and migration status
raymigrator Fix -p MyProduct -env Prod -ot 120 -lms migrated
```

---

## Enum Reference

### MigrationCommand

Represents the CLI command to execute.

| Name | Value | CLI Command |
|------|-------|-------------|
| `None` | 0 | — |
| `MigrateUp` | 1 | `Migrate-Up` |
| `MigrateDown` | 2 | `Migrate-Down` |
| `ValidateHash` | 3 | `Validate-Hash` |
| `UpdateHash` | 4 | `Update-Hash` |
| `Info` | 5 | `Info` |
| `Baseline` | 6 | `Baseline` |
| `FixIssues` | 7 | `Fix` |

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

### HashValidationScope

Controls the granularity of hash validation. See [Hash Validation](../02-core-concepts/hash-validation.md) for the detailed reference.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — scope has not been set |
| `File` | 1 | Compare hash of entire migration file |
| `SqlBlocks` | 2 | Compare hash of SQL content only (excluding TOML metadata) |
| `Disabled` | 3 | Skip hash validation |

CLI accepts `"SqlBlock"` (singular) as an alias for `SqlBlocks`.

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/HashValidationScope.cs`

### MigrationErrorAction

Controls error handling behavior when a migration fails.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — not set |
| `Terminate` | 10 | Stop immediately, no rollback |
| `Rollback` | 20 | Rollback all migrations in current run |
| `RollbackErrorOnly` | 21 | Rollback only the failed migration |
| `RollbackRelease` | 22 | Rollback all migrations from the failed release |
| `Ignore` | 30 | Ignore error, continue with next file |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationErrorAction.cs`

See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior of each mode.

### RollbackErrorAction

Controls error handling behavior when a rollback SQL block fails. Applies both during explicit `Migrate-Down` execution and during error recovery rollback triggered by `MigrationErrorAction` (Rollback, RollbackErrorOnly, or RollbackRelease) in `Migrate-Up`.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set (resolves to Terminate at runtime) |
| `Terminate` | 10 | Abort the entire rollback chain immediately (default) |
| `Ignore` | 30 | Skip the failed block, continue with remaining blocks, mark file as Failed, then continue with next file |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/RollbackErrorAction.cs`

See [Migrate-Down -- RollbackErrorAction](migrate-down.md#rollbackerroraction) and [Error Handling](../02-core-concepts/error-handling.md) for details.

### TargetMigrationOrder

Controls target iteration order within a TargetGroup.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — not set |
| `Simultaneously` | 1 | Execute on all targets per migration (file → target) |
| `Successively` | 2 | Complete all migrations per target (target → file) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/TargetMigrationOrder.cs`

See [Execution Modes](../02-core-concepts/execution-modes.md#target-migration-order) for detailed behavior and examples.

### FixIssues

Controls the scope of repository issue resolution.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid — scope has not been set |
| `All` | 1 | Fix all known problems in repository |
| `OrphanedRuns` | 2 | Fix only orphaned MigrationRun entries |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/FixIssues.cs`

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

Represents the status of a single migration record in the repository. The `--last-migration-status` option on the Fix command accepts `migrated` and `not-migrated` to set the assumed status for orphaned migrations.

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Pending` | 10 | Migration record created, execution has not started yet |
| `Executing` | 20 | SQL blocks are currently being executed |
| `Failed` | 30 | Execution failed, database state is unclear |
| `NotMigrated` | 50 | File is not deployed on target database (rolled back or never executed) |
| `Migrated` | 100 | File is successfully deployed on target database |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationStatus.cs`

### OperatingMode

Determines how RayMigrator loads configuration and executes commands. See [Execution Modes](../02-core-concepts/execution-modes.md#operating-mode) for details.

| Name | Description |
|------|-------------|
| `Standalone` | All configuration loaded from JSON files (`appsettings.json` hierarchy). No Admin-DB. This is the default mode. |
| `ManagedLocal` | Configuration loaded from a local Admin-DB. Products, Environments, Targets, and Repository config come from the Admin-DB. Serilog configuration still read from `appsettings.json`. |
| `ManagedRemote` | CLI operates as a Thin Client, sending HTTP requests to a remote RayMigrator API server instead of accessing databases directly. |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/OperatingMode.cs`

### MigrationOperation

Represents the type of migration operation (displayed in the Info command's migration run history table).

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Rollback` | 5 | Performing rollback (error recovery during Migrate-Up) |
| `MigrateDown` | 50 | Performing down-migration (explicit Migrate-Down command) |
| `MigrateUp` | 100 | Performing up-migration (Migrate-Up command) |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationOperation.cs`

### MigrationRunResult

Represents the final result of a MigrationRun record (displayed by the Info command).

| Name | Value | Description |
|------|-------|-------------|
| `Undefined` | 0 | Invalid -- not set |
| `Running` | 10 | Migration process is currently running |
| `Error` | 90 | Migration(s) stopped due to error(s) |
| `Ok` | 100 | Migration(s) successfully executed and finished |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationRunResult.cs`

---

## Environment Handling

### Environment Resolution

The `--environment` option is **required** for all migration commands (Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash, Info, Baseline, Fix). If neither `--environment` nor `DOTNET_ENVIRONMENT` is provided for a command that requires it, RayMigrator exits with code 3.

### Environment Sources

The environment can come from two sources:

1. **CLI argument**: `--environment` / `-env`
2. **Environment variable**: `DOTNET_ENVIRONMENT`

**Resolution rules:**

- If only `--environment` is provided → use that value.
- If only `DOTNET_ENVIRONMENT` is set → use that value.
- If both are set with **the same value** → use that value.
- If both are set with **different values** → **exit code 2** with error message.
- If neither is set → **exit code 3** with error message.

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
| [Migrate-Up](migrate-up.md) | Detailed Migrate-Up command page with execution flow |
| [Migrate-Down](migrate-down.md) | Detailed Migrate-Down command page with rollback flow |
| [Validate-Hash](validate-hash.md) | Detailed Validate-Hash command page |
| [Update-Hash](update-hash.md) | Detailed Update-Hash command page |
| [Global Options](global-options.md) | Global options, logging, and best practices |
| [Configuration System](../02-core-concepts/configuration-system.md) | Options pattern hierarchy |
| [Environment Variables](../06-configuration-reference/environment-variables.md) | `{ENV:}` placeholder reference |
| [Error Handling](../02-core-concepts/error-handling.md) | Error actions and strategies |
