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
| `Migrate-Up` | Apply pending migrations |
| `Migrate-Down` | Roll back migrations to a specific release |
| `Validate-Hash` | Verify migration file integrity |
| `Update-Hash` | Update stored hashes after intentional file changes |
| `Info` | Display current migration status |
| `Baseline` | Mark an existing database as already migrated |
| `Fix` | Repair repository inconsistencies |

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

The alias passes every argument through unchanged, so `raymig Migrate-Up -p BookStore -env Production -rm Migrate` behaves exactly like the full command.

> **Note:** The alias is a local convenience that you create yourself -- it is **not** shipped with RayMigrator, and it exists only in the shell where you defined it. Use the full name in CI pipelines, scripts, and container entrypoints, where the alias is not present and the explicit name keeps the command portable and reviewable. All examples in this manual use the full name.

---

## Migrate-Up -- Apply Pending Migrations

Discover and execute pending migration files on target databases.

```bash
raymigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

Key options: `--run-mode` (Validate/Simulate/Migrate), `--to-release`, `--target-group`, `--allow-out-of-order`, `--TargetGroup-MigrationOrder` (`-tgmo`), `--stop-rollback-on-missing-rollback-file` (`-sromrf`). See [Migrate-Up Reference](../08-cli-reference/migrate-up.md) for the full option table.

> **Tip:** Use `--run-mode Validate` first to preview which migrations would be applied without touching the database or repository. See [Chapter 7 -- Execution Modes](07-execution-modes.md) for the recommended workflow.

---

## Migrate-Down -- Rollback Migrations

Roll back migrations to a specific release using rollback files.

```bash
raymigrator Migrate-Down -p BookStore -env Development -tr "Release 1.0" -rm Migrate
```

> **Important:** `--to-release "Release 1.0"` means "roll back TO this release." The specified release remains applied; all later releases are rolled back.

Key options: `--to-release` (required), `--run-mode`, `--target-group`. See [Migrate-Down Reference](../08-cli-reference/migrate-down.md) for the full option table.

---

## Validate-Hash -- Verify File Integrity

Compare current migration file hashes against the hashes stored in the repository. Detects unauthorized or accidental modifications.

```bash
raymigrator Validate-Hash -p BookStore -env Production
```

Key options: `--scope` (File/SqlBlocks/Disabled), `--target-group`. See [Validate-Hash Reference](../08-cli-reference/validate-hash.md) for the full option table.

> **Tip:** Run Validate-Hash in your CI/CD pipeline on every commit to catch unauthorized changes to migration files early.

---

## Update-Hash -- Update Stored Hashes

Update the hashes stored in the repository when migration files have been intentionally modified.

```bash
raymigrator Update-Hash -p BookStore -env Production
```

Key options: `--target-group`. See [Update-Hash Reference](../08-cli-reference/update-hash.md) for the full option table.

> **Warning:** Only use Update-Hash after carefully verifying that the file changes are intentional.

---

## Info -- Display Migration Status

Show which migrations have been applied, which are pending, and the overall state.

```bash
raymigrator Info -p BookStore -env Production
```

---

## Baseline -- Mark Existing Database as Migrated

Record existing migrations as "already applied" without executing any SQL. Use this when onboarding an existing database into RayMigrator.

```bash
raymigrator Baseline -p BookStore -env Production
```

Key options: `--to-release`, `--target-group`, `--TargetGroup-MigrationOrder` (`-tgmo`). See [Baseline Reference](../08-cli-reference/command-reference.md#baseline) for the full option table.

---

## Fix -- Repair Repository Issues

Clean up orphaned migration runs and fix inconsistencies in the repository.

```bash
raymigrator Fix -p BookStore -env Production --dry-run
```

Key options: `--scope` (OrphanedRuns/All), `--older-than`, `--dry-run`, `--last-migration-status`. See [Fix Reference](../08-cli-reference/command-reference.md#fix) for the full option table.

> **Tip:** Always use `--dry-run` first to preview what Fix would change.

---

## Tutorial: Run Commands on BookStore

With the BookStore product configured and migration files in place, here is the typical command sequence for a deployment:

```bash
# Step 1: Check what would be migrated (no changes made)
raymigrator Migrate-Up -p BookStore -env Development -rm Validate

# Step 2: Apply all pending migrations
raymigrator Migrate-Up -p BookStore -env Development -rm Migrate

# Step 3: Verify file integrity
raymigrator Validate-Hash -p BookStore -env Development

# Step 4: Check current status
raymigrator Info -p BookStore -env Development
```

For a production deployment with rollback safety:

```bash
# Validate first
raymigrator Migrate-Up -p BookStore -env Production -rm Validate

# Simulate in staging
raymigrator Migrate-Up -p BookStore -env Staging -rm Simulate

# Deploy to production
raymigrator Migrate-Up -p BookStore -env Production -rm Migrate

# If something goes wrong, roll back to the last known good release
raymigrator Migrate-Down -p BookStore -env Production -rm Migrate -tr "Release 1.0"
```

---

## Exit Codes

→ See [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes) for the complete exit code table.

---

For the complete technical reference with option tables, property mappings, and enum values, see [CLI Reference](../08-cli-reference/command-reference.md).

**Next:** [Execution Modes -- The Validate, Simulate, Migrate Workflow](07-execution-modes.md)
