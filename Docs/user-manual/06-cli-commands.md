# 6. CLI Command Reference

In the previous chapters, you configured the BookStore product and wrote your first migration files. Now it is time to learn the commands that drive RayMigrator. This chapter introduces each command with practical examples. For full option tables, property mappings, and enum values, see the [CLI Reference](../08-cli-reference/command-reference.md).

---

## Overview

RayMigrator is invoked as:

```bash
raymigrator <Command> [options]
```

Available commands:

| Command | Purpose |
|---------|---------|
| `migrate-up` | Apply pending migrations |
| `migrate-down` | Roll back migrations to a specific release |
| `validate-hash` | Verify migration file integrity |
| `update-hash` | Update stored hashes after intentional file changes |
| `info` | Display current migration status |
| `baseline` | Mark an existing database as already migrated |
| `fix` | Repair repository inconsistencies |

Most commands require `--product` (`-p`) and `--environment` (`-env`). For global options (`--startup-info` / `-si`, `--reveal-sensitive-data` / `-rsd`, `--config-dir` / `-cd`), see [Global Options](../08-cli-reference/global-options.md).

---

## Shortening the Command with a Shell Alias

`RayMigrator` is a long name to type. If you run it interactively rather than from a pipeline, define a short shell alias. `raymig` is the suggested short form:

**bash / zsh** -- add to `~/.bashrc` or `~/.zshrc`:

```bash
alias raymig=raymigrator
```

**PowerShell** -- add to your profile (open it with `notepad $PROFILE`):

```powershell
Set-Alias raymig raymigrator
```

**Windows CMD** -- valid for the current session; put it in a `doskey` AutoRun script to persist:

```bat
doskey raymig=raymigrator $*
```

Reload the shell (or `source ~/.bashrc` / `. $PROFILE`), then verify:

```bash
raymig --version
```

The alias passes every argument through unchanged, so `raymig migrate-up -p BookStore -env Production -rm migrate` behaves exactly like the full command.

> **Note:** The alias is a local convenience that you create yourself -- it is **not** shipped with RayMigrator, and it exists only in the shell where you defined it. Use the full name in CI pipelines, scripts, and container entrypoints, where the alias is not present and the explicit name keeps the command portable and reviewable. All examples in this manual use the full name.

---

## migrate-up -- Apply Pending Migrations

Discover and execute pending migration files on target databases.

```bash
raymigrator migrate-up -p BookStore -env Production -rm migrate
```

Key options: `--run-mode` (Validate/Simulate/Migrate), `--to-release`, `--target-group`, `--allow-out-of-order`, `--target-group-migration-order` (`-tgmo`), `--stop-rollback-on-missing-rollback-file` (`-sromrf`), `--cancelled-action` (`-ca`, see [Stopping a Command with Ctrl+C](#stopping-a-command-with-ctrlc)). See [migrate-up Reference](../08-cli-reference/migrate-up.md) for the full option table.

> **Tip:** Use `--run-mode validate` first to preview which migrations would be applied without running SQL on the targets or writing the repository. See [Chapter 7 -- Execution Modes](07-execution-modes.md) for the recommended workflow.

---

## migrate-down -- Rollback Migrations

Roll back migrations to a specific release using rollback files.

```bash
raymigrator migrate-down -p BookStore -env Development -tr "Release 1.0" -rm migrate
```

> **Important:** `--to-release "Release 1.0"` means "roll back TO this release." The specified release remains applied; all later releases are rolled back.

Key options: `--to-release` (required), `--run-mode`, `--target-group`. See [migrate-down Reference](../08-cli-reference/migrate-down.md) for the full option table.

---

## validate-hash -- Verify File Integrity

Compare current migration file hashes against the hashes stored in the repository. Detects unauthorized or accidental modifications.

```bash
raymigrator validate-hash -p BookStore -env Production
```

Key options: `--scope` (File/SqlBlocks/Disabled), `--target-group`. See [validate-hash Reference](../08-cli-reference/validate-hash.md) for the full option table.

Database access: repository only, read-only (target databases do not have to be reachable). Audit log (DatabaseLogging): no.

Exit code contract: `0` when every migrated file is `Valid` (files that are not migrated yet are listed as `New` and do not affect the exit code); `1` when at least one file is `Modified` or `Missing`. Pipelines can gate on the exit code alone.

> **Tip:** Run validate-hash in your CI/CD pipeline on every commit to catch unauthorized changes to migration files early.

---

## update-hash -- Update Stored Hashes

Update the hashes stored in the repository when migration files have been intentionally modified.

```bash
raymigrator update-hash -p BookStore -env Production
```

Key options: `--target-group`. See [update-hash Reference](../08-cli-reference/update-hash.md) for the full option table.

Database access: repository only, writes hashes (target databases do not have to be reachable). Audit log (DatabaseLogging): yes.

> **Warning:** Only use update-hash after carefully verifying that the file changes are intentional.

---

## Info -- Display Migration Status

Show which migrations have been applied, which are pending, and the overall state.

```bash
raymigrator info -p BookStore -env Production
```

Database access: repository only, read-only (target databases do not have to be reachable). Audit log (DatabaseLogging): no. The run history lists each run's **Operation** (`MigrateUp`, `MigrateDown`, `Baseline`) next to its RunMode.

The output (shown here without timestamps and level prefixes) starts with the status and lists the files the next `migrate-up` runs, one line per file and target in the order it runs them; then come the target groups and the last ten runs:

```
--- Migration Status for product BookStore, environment Production ---
  Current Release:     Release 1.1
  Total Executed:      6
  Pending Migrations:  1

    Release      Target group  Target  File                     Reason
    ───────────  ────────────  ──────  ───────────────────────  ───────────────────────
    Release 2.0  Backend       MainDB  001_RefactorAuthors.sql  Resumable after block 2
--- Target Groups ---
  [Backend] Type=SqlServer, Release=Release 1.1, Executed=6, Pending=1, LastMigration=2026-10-15 16:40:12, Targets=[MainDB]

--- Last 3 Migration Runs ---
    RunId  Operation     RunMode     Result     # Migrations  StartedAt            FinishedAt           DurationInMs
  ───────  ────────────  ──────────  ─────────  ────────────  ───────────────────  ───────────────────  ────────────
        3  MigrateUp     Migrate     Cancelled             1  2026-10-16 09:14:02  2026-10-16 09:14:05          3120
        2  MigrateUp     Migrate     Ok                    2  2026-10-15 16:40:11  2026-10-15 16:40:12          1034
        1  MigrateUp     Migrate     Ok                    4  2026-10-14 10:02:31  2026-10-14 10:02:33          1870
```

The **Reason** column says why a file is pending: `NotMigrated` (it has not run on the target, or it was rolled back), `Failed` (the last attempt failed and left nothing to resume), `Resumable after block n` (blocks 1 to n committed and `migrate-up` continues after them, here after the run that Ctrl+C stopped; when n is the block count of the file, it only completes the record), `RunAlways` (the file runs on every `migrate-up`, always in full from its first block, also when an earlier attempt committed some of its blocks), `HashMismatch` (the file changed after it was migrated, so `migrate-up` runs it again and logs a warning) or `RollbackIncomplete after k of n rollback blocks, run migrate-down` (a rollback of the file stopped after k of its n rollback blocks, for example when Ctrl+C stopped a `migrate-down`; `migrate-up` refuses to run until `migrate-down` has completed the rollback, see [Rolling Back Migrations](09-rollback-guide.md#block-level-rollback-tracking)). The order is that of a `migrate-up` without `--target-group-migration-order`; a file of an older release than the target has already migrated is listed as well, although `migrate-up` refuses it without `--allow-out-of-order`. The list shows the first 50 entries and ends with `… and n more` when there are more; the count above it is always complete. Every target group line names the number of its pending files (`Pending`) and when its last migration finished (`LastMigration`).

---

## Baseline -- Mark Existing Database as Migrated

Record existing migrations as "already applied" without executing any SQL. Use this when onboarding an existing database into RayMigrator.

```bash
raymigrator baseline -p BookStore -env Production
```

Key options: `--to-release`, `--target-group`, `--target-group-migration-order` (`-tgmo`). See [Baseline Reference](../08-cli-reference/command-reference.md#baseline) for the full option table.

Database access: repository only, writes MigrationRun/MigrationRecord rows stamped with operation `Baseline` (no SQL is executed on the targets, so they do not have to be reachable). Audit log (DatabaseLogging): yes.

---

## Fix -- Repair Repository Issues

Clean up orphaned migration runs and fix inconsistencies in the repository.

```bash
raymigrator fix -p BookStore -env Production --run-mode simulate
```

Key options: `--scope` (orphanedruns/all), `--older-than`, `--run-mode` (migrate/simulate), `--last-migration-status`. See [Fix Reference](../08-cli-reference/command-reference.md#fix) for the full option table.

Database access: repository only (target databases do not have to be reachable); `--run-mode simulate` is read-only. Audit log (DatabaseLogging): yes, but not in simulate mode.

> **Tip:** Always use `--run-mode simulate` first to preview what Fix would change.

---

## Tutorial: Run Commands on BookStore

With the BookStore product configured and migration files in place, here is the typical command sequence for a deployment:

```bash
# Step 1: Check what would be migrated (no changes made)
raymigrator migrate-up -p BookStore -env Development -rm validate

# Step 2: Apply all pending migrations
raymigrator migrate-up -p BookStore -env Development -rm migrate

# Step 3: Verify file integrity
raymigrator validate-hash -p BookStore -env Development

# Step 4: Check current status
raymigrator info -p BookStore -env Development
```

For a production deployment with rollback safety:

```bash
# Validate first
raymigrator migrate-up -p BookStore -env Production -rm validate

# Simulate in staging
raymigrator migrate-up -p BookStore -env Staging -rm simulate

# Deploy to production
raymigrator migrate-up -p BookStore -env Production -rm migrate

# If something goes wrong, roll back to the last known good release
raymigrator migrate-down -p BookStore -env Production -rm migrate -tr "Release 1.0"
```

---

## Stopping a Command with Ctrl+C

While a command runs, the first Ctrl+C (on Linux and macOS also a SIGTERM, as a container stop or a cancelled CI job sends it) stops it cleanly at its next checkpoint instead of killing the process. A `migrate-up` stops at the next block boundary, keeps the blocks that committed and records the run as `Cancelled`; the next `migrate-up` resumes the interrupted file. `--cancelled-action` chooses what a cancelled `migrate-up` does after it stopped:

```bash
# Roll back the interrupted file when the run is cancelled
raymigrator migrate-up -p BookStore -env Production -rm migrate --cancelled-action rollback-file
```

`terminate` (the default) does nothing else; `rollback-file`, `rollback-run` and `rollback-release` roll back like the error actions `RollbackErrorOnly`, `Rollback` and `RollbackRelease`. A command that stopped this way exits with `130`; when the rollback of a cancelled `migrate-up` does not complete cleanly, the run is `Error` and the exit code `1`, and so is a `migrate-up` that had already failed at a file and was in its error recovery when the signal came. A second Ctrl+C ends the process at once and leaves the run open until RayMigrator closes it as orphaned (see [Chapter 8](08-error-handling.md#stopping-a-run-with-ctrlc)). What each command does when it is cancelled is listed in [Cancelling a Run](../08-cli-reference/global-options.md#cancelling-a-run).

---

## Exit Codes

→ See [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes) for the complete exit code table. `130` means that Ctrl+C or SIGTERM cancelled the command and it stopped cleanly.

---

For the complete technical reference with option tables, property mappings, and enum values, see [CLI Reference](../08-cli-reference/command-reference.md).

**Next:** [Execution Modes -- The Validate, Simulate, Migrate Workflow](07-execution-modes.md)
