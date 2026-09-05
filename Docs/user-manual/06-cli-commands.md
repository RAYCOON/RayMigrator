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

Key options: `--run-mode` (Validate/Simulate/Migrate), `--to-release`, `--target-group`, `--allow-out-of-order`, `--target-group-migration-order` (`-tgmo`), `--stop-rollback-on-missing-rollback-file` (`-sromrf`). See [migrate-up Reference](../08-cli-reference/migrate-up.md) for the full option table.

> **Tip:** Use `--run-mode validate` first to preview which migrations would be applied without touching the database or repository. See [Chapter 7 -- Execution Modes](07-execution-modes.md) for the recommended workflow.

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

> **Tip:** Run validate-hash in your CI/CD pipeline on every commit to catch unauthorized changes to migration files early.

---

## update-hash -- Update Stored Hashes

Update the hashes stored in the repository when migration files have been intentionally modified.

```bash
raymigrator update-hash -p BookStore -env Production
```

Key options: `--target-group`. See [update-hash Reference](../08-cli-reference/update-hash.md) for the full option table.

> **Warning:** Only use update-hash after carefully verifying that the file changes are intentional.

---

## Info -- Display Migration Status

Show which migrations have been applied, which are pending, and the overall state.

```bash
raymigrator info -p BookStore -env Production
```

---

## Baseline -- Mark Existing Database as Migrated

Record existing migrations as "already applied" without executing any SQL. Use this when onboarding an existing database into RayMigrator.

```bash
raymigrator baseline -p BookStore -env Production
```

Key options: `--to-release`, `--target-group`, `--target-group-migration-order` (`-tgmo`). See [Baseline Reference](../08-cli-reference/command-reference.md#baseline) for the full option table.

---

## Fix -- Repair Repository Issues

Clean up orphaned migration runs and fix inconsistencies in the repository.

```bash
raymigrator fix -p BookStore -env Production --dry-run
```

Key options: `--scope` (OrphanedRuns/All), `--older-than`, `--dry-run`, `--last-migration-status`. See [Fix Reference](../08-cli-reference/command-reference.md#fix) for the full option table.

> **Tip:** Always use `--dry-run` first to preview what Fix would change.

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

## Exit Codes

→ See [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes) for the complete exit code table.

---

For the complete technical reference with option tables, property mappings, and enum values, see [CLI Reference](../08-cli-reference/command-reference.md).

**Next:** [Execution Modes -- The Validate, Simulate, Migrate Workflow](07-execution-modes.md)
