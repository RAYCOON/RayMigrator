# 7. The Validate, Simulate, Migrate Workflow

RayMigrator provides three execution modes so you can verify, preview, and execute migrations with confidence. This chapter explains each mode, when to use it, and how to integrate all three into a deployment pipeline.

---

## The Three Run Modes

The `--run-mode` (alias `-rm`) option on `Migrate-Up` and `Migrate-Down` controls how far RayMigrator goes when processing migrations.

| Mode | DB Connections | SQL Executed | Repository Records Written | Use Case |
|------|:-------------:|:-----------:|:--------------------------:|----------|
| Validate | No | No | No | CI checks, pre-deployment verification |
| Simulate | Yes (validation only) | No | No | Staging dry runs, pipeline testing |
| Migrate | Yes | Yes | Yes | Production deployment |

---

## Validate Mode

Validate scans your migration files locally and reports what **would** be executed. It makes no changes whatsoever -- no SQL runs on target databases, no records are written to the repository, and no database connections are established at all.

```bash
RayMigrator Migrate-Up -p BookStore -env Production -rm Validate
```

### What Validate Checks

- **File discovery and filtering** -- Finds all migration files matching the product, environment, and target configuration.
- **Hash computation** -- Computes file hashes, config hashes, and SQL block hashes for all discovered files. Note: comparing hashes against the repository requires the separate `Validate-Hash` command or running in Simulate/Migrate mode, since Validate does not connect to the repository.
- **Missing rollback files** -- If `RequireRollbackFile` is enabled, validates that every migration has a corresponding rollback file.
- **TOML metadata parsing** -- Verifies that all migration file headers contain valid TOML metadata.

### When to Use Validate

- On every commit in a CI/CD pipeline
- Before deploying to any environment
- When reviewing migration files during code review
- To confirm that configuration changes do not break migration discovery

> **Tip:** Validate is fast and safe. There is no reason not to run it on every build.

---

## Simulate Mode

Simulate does everything Validate does, then goes further: it establishes actual connections to both the repository and target databases, reads existing migration records to determine what is already migrated, and validates connectivity. No SQL migration blocks are executed on target databases and no repository records are written.

```bash
RayMigrator Migrate-Up -p BookStore -env Staging -rm Simulate
```

### What Simulate Does Beyond Validate

- **Connects to target databases** -- Opens a real connection to each configured target to validate connectivity. This confirms that connection strings are correct and the databases are reachable.
- **Connects to the repository** -- Reads existing migration records, enabling hash comparison and out-of-order detection that Validate alone cannot perform.
- **Exercises the full pipeline** -- File discovery, ordering, filtering, and repository read logic all run as they would in Migrate mode.
- **Gracefully handles non-existent repository** -- If the repository does not exist yet, Simulate treats all migrations as pending and continues without error.

### What Simulate Does NOT Do

- No SQL migration blocks are executed on target databases
- No schema changes are made to target databases
- No data is inserted, updated, or deleted on targets
- No repository records are written (no MigrationRun, no Migration records)
- No database log entries are written (DatabaseLogging sink is inactive)

### When to Use Simulate

- In a staging environment before production deployment
- To verify that repository interactions work correctly
- To test the end-to-end flow without risk
- When onboarding a new target group and want to verify configuration

> **Note:** Simulate reads existing repository records to determine what is already migrated, but does not write any new records. Subsequent Migrate-Up calls will still see all pending migrations as pending, making Simulate fully side-effect-free with respect to the repository.

---

## Migrate Mode

Migrate is the full execution mode. It discovers pending migrations, executes the SQL on target databases, and creates repository records reflecting the actual results.

```bash
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

### What Migrate Does

- Everything that Validate and Simulate do
- **Executes SQL statements** on the configured target databases
- **Creates definitive repository records** with actual execution results, timing, and hashes
- **Handles transactions** according to the `UseTransaction` setting in each migration file
- **Triggers error handling** based on `MigrationErrorAction` (Terminate, Rollback, RollbackErrorOnly, RollbackRelease, Ignore)

### When to Use Migrate

- Production deployments
- Development environment setup
- Any time you want migrations to actually run

---

## The Recommended Workflow

The three modes form a progression from safe to impactful:

```
┌─────────┐     ┌──────────┐     ┌─────────┐
│ Validate │ ──> │ Simulate │ ──> │ Migrate │
│  (CI)    │     │ (Staging)│     │  (Prod) │
└─────────┘     └──────────┘     └─────────┘
```

1. **Validate in CI/CD** -- Run on every commit and pull request. Catch structural issues (invalid TOML, missing rollback files) before they reach any environment. Pair with `Validate-Hash` for hash integrity checks.
2. **Simulate in staging** -- Run in a staging environment to exercise the full pipeline without executing SQL. Confirm that file discovery, filtering, and repository logic work as expected.
3. **Migrate in production** -- Execute with confidence, knowing that Validate and Simulate have already verified the migration set.

---

## Tutorial: BookStore Workflow

Let us walk through all three modes using the BookStore product.

### Step 1: Validate -- Check for Issues

```bash
RayMigrator Migrate-Up -p BookStore -env Production -rm Validate
```

Validate scans the migration files and reports:
- How many migration files were discovered
- Whether any rollback files are missing
- Whether all TOML metadata headers are valid

If Validate succeeds, you know the migration files are structurally sound. Note that hash comparison against the repository and out-of-order detection require Simulate or Migrate mode (or the separate `Validate-Hash` command).

### Step 2: Simulate -- Dry Run with Repository Tracking

```bash
RayMigrator Migrate-Up -p BookStore -env Staging -rm Simulate
```

Simulate reads the repository and processes all migration files without executing SQL or writing any records. After this step, the console output shows:
- The correct migrations were identified
- The execution order matches expectations
- The target group filtering works as configured

### Step 3: Migrate -- Execute for Real

```bash
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

Migrate runs the actual SQL on target databases. After completion:
- All pending migrations have been applied
- Repository records reflect the execution results
- Hash values are stored for future integrity checks

---

## CI/CD Integration Pattern

Below is an example pipeline structure using the three run modes. Adapt this to your CI/CD platform (GitHub Actions, Azure DevOps, GitLab CI, Jenkins, etc.).

```yaml
# .github/workflows/migration-check.yml
name: Migration Pipeline

jobs:
  migration-validate:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Validate migrations
        run: RayMigrator Migrate-Up -p BookStore -env CI -rm Validate

      - name: Check hashes
        run: RayMigrator Validate-Hash -p BookStore -env CI

  migration-deploy-staging:
    needs: migration-validate
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Simulate in staging
        run: RayMigrator Migrate-Up -p BookStore -env Staging -rm Simulate

  migration-deploy-production:
    needs: migration-deploy-staging
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Migrate production
        run: RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

### Pipeline Design Principles

| Principle | Implementation |
|-----------|---------------|
| Fail fast | Validate runs first and blocks the pipeline on any issue |
| Hash integrity | Validate-Hash catches unauthorized file modifications |
| Safe preview | Simulate in staging before touching production |
| Gated deployment | Production migration requires staging success |

> **Tip:** Always run `Validate-Hash` in your CI pipeline alongside Validate. Hash validation catches unauthorized modifications to already-executed migration files -- a class of issue that Validate alone does not cover.

---

## Mode Comparison at a Glance

For the detailed comparison table including all aspects (file discovery, TOML parsing, hash comparison, repository records, SQL execution, transaction handling, error actions), see [Execution Modes](../02-core-concepts/execution-modes.md).

> **Note on Simulate repeatability:** Simulate is fully repeatable. Because it does not write any repository records, running it multiple times produces the same result: it always shows the same set of pending migrations, and no cleanup is needed between runs.

---

**Next:** [Error Handling](08-error-handling.md)
