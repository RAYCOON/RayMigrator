# Error Scenarios and Recovery

This document provides a comprehensive matrix of all error scenarios — covering every combination of `MigrationErrorAction`, error position, rollback chain behavior, multi-target modes, and `RunAlways` — with concrete status outcomes and step-by-step recovery procedures.

**When to consult this document:**

- A migration run has failed and you need to determine the exact database and repository state
- You want to understand what happens for a specific `MigrationErrorAction` before configuring it
- You need a step-by-step recovery procedure for a specific failure scenario

For the conceptual overview of error handling strategies, see [Error Handling](error-handling.md). For state transition rules, see [Migration State Machine](migration-state-machine.md).

---

## Key System Behaviors

Understanding these five rules is essential for interpreting the scenario matrix below.

### 1. Failed Files Are Auto-Retried

When `migrate-up` runs again after a failure, files with status `Failed` (30) or `NotMigrated` (50) are automatically re-executed. **No manual status change is needed.** Simply fix the root cause (SQL error, missing dependency, etc.) and re-run `migrate-up`.

### 2. Resume Mechanism (FindResumableBlock)

If a migration file was partially executed (some SQL blocks succeeded before the error), RayMigrator tracks the block progress (`FileUpBlocksMigrated` / `FileUpBlocksTotal`). On the next run, if the file content hash matches (`FileUpBlocksHash`), execution resumes from block N+1 rather than re-executing the entire file. If the file was modified (hash changed), it is re-executed from block 1.

### 3. Hash Mismatch Behavior

Two distinct cases:

- **Failed file modified and re-run**: RayMigrator detects the hash mismatch, logs a warning, but **proceeds with re-execution** from block 1. The hash is updated after successful completion. This is the normal fix-and-retry workflow.

- **Already-Migrated file modified**: If a file with status `Migrated` (100) has its content changed, it will be **re-executed** on the next `migrate-up`. This can cause errors (e.g., duplicate `CREATE TABLE`). **Never modify an already-migrated file without running `migrate-down` first** or using `update-hash` to acknowledge the change.

### 4. Fix Command Scope

The `fix` command only handles **orphaned runs** — `MigrationRun` entries stuck in `Running` (10) status with no process behind them. It does **not** change individual `Migration` statuses from `Failed` to `NotMigrated`. That is not needed because failed files are auto-retried (see rule 1).

### 5. MigrateDown Only Rolls Back Migrated Files

The `migrate-down` command only processes files with status `Migrated` (100). Files with status `Failed` (30), `NotMigrated` (50), or `NoRecord` (-1) are skipped. This means `migrate-down` cannot be used to "clean up" after a failed run — it is designed for intentional rollbacks of successfully applied migrations.

---

## Three Types of Inconsistency

After a failed migration run, one or more of these inconsistency types may exist:

### Repository Inconsistency

The repository status does not reflect the actual database state. For example, a file is marked as `Migrated` but its rollback failed, leaving partial changes in the database.

**Resolution**: Usually requires manual SQL to align the database with what the repository records, or manual repository updates to match the actual database state.

### Database Inconsistency

The target database is in a partial state — some changes from a migration file were applied but not all (e.g., table created but data insert failed when `UseTransaction = false`).

**Resolution**: Manually complete or revert the partial changes in the database, then re-run `migrate-up` or use `update-hash`.

### Logical Inconsistency

The repository status is technically correct but the system is in an undesirable state. For example, after `Ignore`, a file is correctly marked as `Failed` while later files are `Migrated` — the repository is accurate, but the database may have dependent data without the prerequisite schema.

**Resolution**: Fix the failed file and re-run `migrate-up`, or manually apply the missing changes and use `update-hash`.

---

## Scenario Categories

All scenarios use a standard 3-release, 9-file migration set unless otherwise noted:

| Release | Files | Purpose |
|---------|-------|---------|
| Release 1.0 | `01_CreateTableA.sql`, `02_CreateTableB.sql`, `03_InsertDataA.sql` | Schema + data |
| Release 2.0 | `01_CreateTableC.sql`, `02_InsertDataB.sql`, `03_AddColumnA.sql` | Schema + data + DDL |
| Release 3.0 | `01_CreateTableD.sql`, `02_InsertDataC.sql`, `03_InsertDataD.sql` | Schema + data |

**Status abbreviations**: M = Migrated (100), NM = NotMigrated (50), F = Failed (30), -- = NoRecord (-1)

---

### Category 1: Happy Path

#### S01 — All Migrations Succeed

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| Error Position | None |
| MigrationRunResult | Ok (100) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | M |
| R1F2 `02_CreateTableB.sql` | M |
| R1F3 `03_InsertDataA.sql` | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | M |
| R2F3 `03_AddColumnA.sql` | M |
| R3F1 `01_CreateTableD.sql` | M |
| R3F2 `02_InsertDataC.sql` | M |
| R3F3 `03_InsertDataD.sql` | M |

**DB State**: All tables created, all data inserted.

**Inconsistency**: None.

**Recovery**: N/A — this is the desired end state.

---

### Category 2: Terminate (No Rollback)

#### S02 — Error in First File (R1F1)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| Error Position | R1F1 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | F |
| R1F2–R3F3 | -- |

**DB State**: No tables created (transaction rolled back the failed DDL).

**Inconsistency**: None — repository and database are aligned.

**Recovery**: Fix the SQL error in `01_CreateTableA.sql`, then re-run `migrate-up`. The failed file is auto-retried.

#### S03 — Error in Middle (R2F2)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| Error Position | R2F2 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | M |
| R1F2 `02_CreateTableB.sql` | M |
| R1F3 `03_InsertDataA.sql` | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | F |
| R2F3–R3F3 | -- |

**DB State**: TableA (2 rows), TableB (0 rows — INSERT rolled back by transaction), TableC exists (0 rows).

**Inconsistency**: None — repository and database are aligned. The `Failed` file prevented data insertion, but the transaction ensured no partial data.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. Files R1F1–R2F1 are skipped (already `Migrated`), R2F2 is retried, R2F3–R3F3 execute normally.

#### S04 — Error in Last File (R3F3)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| Error Position | R3F3 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R3F2 | M |
| R3F3 `03_InsertDataD.sql` | F |

**DB State**: All tables exist. TableD has 0 rows (INSERT rolled back by transaction).

**Inconsistency**: None.

**Recovery**: Fix `03_InsertDataD.sql`, re-run `migrate-up`.

#### S05 — Error with UseTransaction=false (R2F2)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| UseTransaction | false (on R2F2) |
| Error Position | R2F2 (block 2 of 2 fails) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R2F1 | M |
| R2F2 `02_InsertDataB.sql` | F |
| R2F3–R3F3 | -- |

**DB State**: TableB has 1 row — block 1 was committed (no transaction), block 2 failed.

**Inconsistency**: **Database inconsistency** — partial data in TableB. The first SQL block was committed without a transaction, so it persists despite the file failing.

**Recovery**:
1. Manually clean up the partial data: `DELETE FROM TableB WHERE ...`
2. Fix the SQL error in block 2 of `02_InsertDataB.sql`
3. Re-run `migrate-up`

> **Important**: This scenario demonstrates why `UseTransaction = true` (the default) is strongly recommended. Without transactions, partial block execution creates database inconsistencies that require manual cleanup.

---

### Category 3: Rollback (Entire Run)

#### S06 — Error in R3F3, All Rollbacks Succeed

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| Error Position | R3F3 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R3F2 | NM |
| R3F3 `03_InsertDataD.sql` | NM |

**DB State**: All tables dropped by rollback. Database is back to pre-run state.

**Inconsistency**: None — clean rollback.

**Recovery**: Fix `03_InsertDataD.sql`, re-run `migrate-up`. All files re-execute from scratch.

#### S07 — Error in R2F2, All Rollbacks Succeed

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| Error Position | R2F2 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R2F1 | NM |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: All tables dropped by rollback. Database is back to pre-run state.

**Inconsistency**: None.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`.

#### S08 — Error in R1F1 (Degenerate Case)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| Error Position | R1F1 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | NM |
| R1F2–R3F3 | -- |

**DB State**: No tables created. Only the failed file's rollback was needed.

**Inconsistency**: None.

**Recovery**: Fix `01_CreateTableA.sql`, re-run `migrate-up`.

#### S09 — R2F2 Fails, R2F2 Rollback Also Fails (RollbackErrorAction=Terminate)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RollbackErrorAction | Terminate |
| Error Position | R2F2 (rollback of R2F2 also fails) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | F |
| R2F3–R3F3 | -- |

**DB State**: R1 tables exist with data. TableC exists. The rollback chain was aborted at R2F2 (its rollback failed), so R2F1 and R1 were never rolled back.

**Inconsistency**: **Repository inconsistency** — R1F1–R2F1 are marked `Migrated` which is correct (they were never rolled back), but the intent was to rollback everything. The `Failed` status on R2F2 is also correct.

**Recovery**:
1. Manually fix the database state for R2F2 if needed
2. Fix the rollback file for R2F2 if the issue is in the rollback SQL
3. Fix the forward migration `02_InsertDataB.sql`
4. Re-run `migrate-up` — R2F2 is retried, R2F3–R3F3 execute normally

#### S10 — R2F2 Fails, R2F2 Rollback OK, R2F1 Rollback Fails (RollbackErrorAction=Terminate)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RollbackErrorAction | Terminate |
| Error Position | R2F2 fails, R2F1 rollback fails |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | F |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: R1 tables exist. TableC still exists (R2F1 rollback failed). R2F2 was successfully rolled back.

**Inconsistency**: **Repository inconsistency** — R2F1 is `Failed` because its rollback failed, but the table it created still exists. R1 files are `Migrated` which is correct (rollback chain aborted before reaching R1).

**Recovery**:
1. Manually drop TableC or fix the rollback SQL for R2F1
2. Fix `02_InsertDataB.sql` (the original error)
3. Re-run `migrate-up` — R2F1 (Failed) and R2F2 (NotMigrated) are retried

#### S11 — R2F2 Fails, R2F1 Rollback Fails with RollbackErrorAction=Ignore

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RollbackErrorAction | Ignore |
| Error Position | R2F2 fails, R2F1 rollback fails |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | NM |
| R2F1 `01_CreateTableC.sql` | F |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: R1 tables dropped (R1 rollbacks succeeded). TableC still exists (R2F1 rollback failed but was ignored, chain continued).

**Inconsistency**: **Repository + Database inconsistency** — R2F1 is `Failed` but its table still exists. R1 files are `NotMigrated` and their tables are correctly dropped. The difference from S10 is that `RollbackErrorAction=Ignore` allowed the chain to continue past R2F1's failure, so R1 was rolled back.

**Recovery**:
1. Manually drop TableC
2. Fix `02_InsertDataB.sql`
3. Fix the R2F1 rollback file if needed
4. Re-run `migrate-up` — all files with `Failed` or `NotMigrated` are retried

---

### Category 4: RollbackErrorOnly

#### S12 — Only Failed File Rolled Back

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackErrorOnly |
| Error Position | R2F2 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: R1 tables with data. TableC exists. R2F2 was rolled back to `NotMigrated`.

**Inconsistency**: None — repository and database are aligned. Only the failed file was rolled back as intended.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. R2F2 is retried, R2F3–R3F3 execute normally.

#### S13 — Rollback of Failed File Also Fails

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackErrorOnly |
| RollbackErrorAction | Terminate |
| Error Position | R2F2 (rollback also fails) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R2F1 | M |
| R2F2 `02_InsertDataB.sql` | F |
| R2F3–R3F3 | -- |

**DB State**: Same as S09. R2F2 stays `Failed` because its rollback also failed.

**Inconsistency**: Depends on what R2F2 did before failing. If `UseTransaction = true`, the database is clean. If `UseTransaction = false`, partial data may exist.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`.

---

### Category 5: RollbackRelease

#### S15 — Error in R2, Only R2 Rolled Back

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackRelease |
| Error Position | R2F2 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | NM |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: R1 tables intact with data. TableC dropped (R2 rolled back). R3 never started.

**Inconsistency**: None — Release 1.0 is fully intact, Release 2.0 is cleanly rolled back.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. R1 is skipped, R2–R3 execute from scratch.

#### S16 — Error in R3, R1+R2 Stay Intact

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackRelease |
| Error Position | R3F3 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R2F3 | M |
| R3F1 `01_CreateTableD.sql` | NM |
| R3F2 `02_InsertDataC.sql` | NM |
| R3F3 `03_InsertDataD.sql` | NM |

**DB State**: R1 and R2 fully intact. TableD dropped. TableC has 0 rows (data was inserted by R3F2, which was rolled back).

**Inconsistency**: None.

**Recovery**: Fix `03_InsertDataD.sql`, re-run `migrate-up`.

#### S17 — Error in R1F3 (Equivalent to Full Rollback)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackRelease |
| Error Position | R1F3 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | NM |
| R1F2 `02_CreateTableB.sql` | NM |
| R1F3 `03_InsertDataA.sql` | NM |
| R2F1–R3F3 | -- |

**DB State**: All tables dropped. When the error occurs in the first release, `RollbackRelease` is equivalent to a full `Rollback`.

**Inconsistency**: None.

**Recovery**: Fix `03_InsertDataA.sql`, re-run `migrate-up`.

#### S18 — R2F3 Fails, R2F1 Rollback Fails (RollbackErrorAction=Terminate)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackRelease |
| RollbackErrorAction | Terminate |
| Error Position | R2F3 fails, R2F1 rollback fails |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | F |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3 `03_AddColumnA.sql` | NM |
| R3F1–R3F3 | -- |

**DB State**: R1 intact. TableC still exists (rollback failed). R2F2 and R2F3 were successfully rolled back.

**Inconsistency**: **Repository inconsistency** — R2F1 is `Failed` but its table still exists.

**Recovery**:
1. Manually drop TableC or fix R2F1's rollback file
2. Fix `03_AddColumnA.sql` (original error)
3. Re-run `migrate-up`

---

### Category 6: Ignore

#### S19 — Error Skipped, Execution Continues

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Ignore |
| Error Position | R2F2 |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | F |
| R2F3 `03_AddColumnA.sql` | M |
| R3F1–R3F3 | M |

**DB State**: All tables exist. TableB has 0 rows (INSERT transaction rolled back). TableD has 1 row. All other operations succeeded.

**Inconsistency**: **Logical inconsistency** — R2F2 failed but later files that may depend on R2F2's data executed successfully. The `MigrationRunResult` is `Error` (90) even though most files succeeded.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. Only the `Failed` file is retried; all `Migrated` files are skipped.

> **Note**: With `Ignore`, the `MigrationRunResult` is always `Error` (90) when any file failed, even though execution continued and completed for all other files.

---

### Category 7: Missing Rollback Files

#### S24 — RequireRollbackFile=true: Validation Fails Before Execution

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RequireRollbackFile | true |
| Error | R2F1 has no rollback file |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| All files | -- (no records) |

**DB State**: No tables created. Validation failed before any SQL was executed.

**Inconsistency**: None — the run was prevented entirely.

**Recovery**: Create the missing rollback file `01_CreateTableC.rollback.sql`, re-run `migrate-up`.

> **Important**: With `RequireRollbackFile = true`, missing rollback files are caught during the file classification phase (before any SQL execution). This is a structural validation, not a runtime error.

#### S25 — RequireRollbackFile=false, StopRollbackOnMissingRollbackFile=false: Missing Rollback, Chain Continues

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RequireRollbackFile | false |
| StopRollbackOnMissingRollbackFile | false |
| Error Position | R2F2 (R2F1 has no rollback file) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | NM |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**DB State**: R1 tables dropped (rollback succeeded). TableC still exists (rollback file missing, skipped with warning, chain continued).

**Inconsistency**: **Repository + Database inconsistency** — R2F1 remains `Migrated` (status unchanged because rollback file was missing) but its table still exists in the database.

**Recovery**:
1. Manually drop TableC
2. Create the missing rollback file for R2F1
3. Fix `02_InsertDataB.sql`
4. Re-run `migrate-up`

> **Note**: With the default `StopRollbackOnMissingRollbackFile = true`, the rollback chain would have stopped at R2F1 (R1 files would remain `Migrated`). Set `StopRollbackOnMissingRollbackFile = false` explicitly to get the chain-continues behavior shown here.

#### S27 — Multiple Missing Rollback Files (StopRollbackOnMissingRollbackFile=false)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RequireRollbackFile | false |
| StopRollbackOnMissingRollbackFile | false |
| Error Position | R3F3 (R1F1, R2F1, R3F1 have no rollback files) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | M |
| R1F2 `02_CreateTableB.sql` | NM |
| R1F3 `03_InsertDataA.sql` | NM |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3 `03_AddColumnA.sql` | NM |
| R3F1 `01_CreateTableD.sql` | M |
| R3F2 `02_InsertDataC.sql` | NM |
| R3F3 `03_InsertDataD.sql` | NM |

**DB State**: TableA, TableC, and TableD still exist (rollback files missing, skipped). All other rollbacks succeeded — data removed, columns dropped.

**Inconsistency**: **Repository + Database inconsistency** — three files remain `Migrated` (status unchanged because rollback files were missing) but their tables still exist in the database.

**Recovery**:
1. Manually drop TableA, TableC, TableD
2. Create the missing rollback files
3. Fix `03_InsertDataD.sql`
4. Re-run `migrate-up`

---

### Category 8: RunAlways

#### S28 — RunAlways File Re-Executed on Second Run

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| RunAlways | true (on R1F3) |

**Run 1**: All files succeed. MigrationRunResult = Ok.

**Run 2**: Only the `RunAlways` file re-executes. MigrationRunResult = Ok.

| File | Status After Run 2 |
|------|-------------------|
| R1F3 `03_InsertDataA.sql` | M (re-executed, same record updated) |
| All other files | M (skipped) |

**DB State**: TableA has 4+ rows (data re-inserted on second run). The `RunAlways` file updates the existing `Migration` record rather than creating a new one.

**Inconsistency**: None — this is the intended behavior for `RunAlways` files.

**Recovery**: N/A.

#### S29 — RunAlways File Fails on Re-Run (Terminate)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| RunAlways | true (on R1F3, designed to fail on second run) |

**Run 1**: All succeed. MigrationRunResult = Ok (100).

**Run 2**: RunAlways file fails (e.g., duplicate key). MigrationRunResult = Error (90).

**DB State**: Run 1's data is preserved. Run 2's MigrationRun is Error, but Run 1's MigrationRun result is untouched.

**Inconsistency**: None — the second run's failure does not affect the first run's successful state.

**Recovery**: Fix the `RunAlways` file to be idempotent (use `INSERT ... ON CONFLICT DO NOTHING` or equivalent), re-run.

#### S30 — RunAlways File Fails on Re-Run (Rollback)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RunAlways | true (on R1F3, designed to fail on second run) |

**Run 1**: All succeed. MigrationRunResult = Ok (100).

**Run 2**: RunAlways file fails. Rollback triggered. MigrationRunResult = Error (90). Run 1's result is untouched.

**Recovery**: Same as S29 — make the `RunAlways` file idempotent.

---

### Category 9: Multi-Target (Simultaneously)

In `Simultaneously` mode (file → target loop), each file is executed on all targets before moving to the next file. If a file fails on one target, remaining targets for that file are skipped.

#### S31 — Simultaneously + Ignore

| Setting | Value |
|---------|-------|
| TargetMigrationOrder | Simultaneously |
| MigrationErrorAction | Ignore |
| Error Position | R2F2 fails on Target1 |
| MigrationRunResult | Error (90) |

| File | Target1 | Target2 |
|------|---------|---------|
| R1F1–R1F3 | M | M |
| R2F1 `01_CreateTableC.sql` | M | M |
| R2F2 `02_InsertDataB.sql` | F | -- |
| R2F3 `03_AddColumnA.sql` | M | M |
| R3F1–R3F3 | M | M |

**DB State**: Target1 has all tables but R2F2 data missing. Target2 has all tables except R2F2 was skipped entirely (never executed on Target2 because it failed on Target1 first).

**Inconsistency**: **Logical inconsistency** — Target2 has no record for R2F2, and the file was never attempted there.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. R2F2 is retried on Target1 (Failed → retry) and executed for the first time on Target2 (NoRecord → new execution).

#### S32 — Simultaneously + Rollback

| Setting | Value |
|---------|-------|
| TargetMigrationOrder | Simultaneously |
| MigrationErrorAction | Rollback |
| Error Position | R2F2 fails on Target1 |
| MigrationRunResult | Error (90) |

| File | Target1 | Target2 |
|------|---------|---------|
| R1F1–R1F3 | NM | NM |
| R2F1 `01_CreateTableC.sql` | NM | NM |
| R2F2 `02_InsertDataB.sql` | NM | -- |

**DB State**: Both databases are clean — all tables dropped on both targets.

**Inconsistency**: None — full rollback on both targets succeeded. Note that R2F2 on Target2 is `NoRecord` (never executed), while on Target1 it is `NotMigrated` (executed, then rolled back).

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`.

---

### Category 10: Multi-Target (Successively)

In `Successively` mode (target → file loop), all files are executed on Target1 before moving to Target2 within each release.

#### S33 — Successively + Terminate

| Setting | Value |
|---------|-------|
| TargetMigrationOrder | Successively |
| MigrationErrorAction | Terminate |
| Error Position | R2F2 fails on Target1 |
| MigrationRunResult | Error (90) |

| File | Target1 | Target2 |
|------|---------|---------|
| R1F1–R1F3 | M | M |
| R2F1 `01_CreateTableC.sql` | M | -- |
| R2F2 `02_InsertDataB.sql` | F | -- |
| R2F3–R3F3 | -- | -- |

**DB State**: R1 complete on both targets. R2F1 executed only on Target1 (Target2 never reached R2). R2F2 failed on Target1.

**Inconsistency**: **Logical inconsistency** — targets are at different migration levels. Target1 has R2F1 applied, Target2 does not.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. Target1 retries R2F2, Target2 starts R2 from R2F1.

#### S34 — Successively + Ignore

| Setting | Value |
|---------|-------|
| TargetMigrationOrder | Successively |
| MigrationErrorAction | Ignore |
| Error Position | R2F2 (same broken SQL, fails on both targets) |
| MigrationRunResult | Error (90) |

| File | Target1 | Target2 |
|------|---------|---------|
| R1F1–R1F3 | M | M |
| R2F1 `01_CreateTableC.sql` | M | M |
| R2F2 `02_InsertDataB.sql` | F | F |
| R2F3–R3F3 | M | M |

**DB State**: Both targets have all tables but R2F2 data is missing on both.

**Inconsistency**: **Logical inconsistency** — same as S19 but on both targets.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`. R2F2 is retried on both targets.

#### S35 — Successively + Rollback

| Setting | Value |
|---------|-------|
| TargetMigrationOrder | Successively |
| MigrationErrorAction | Rollback |
| Error Position | R2F2 fails on Target1 |
| MigrationRunResult | Error (90) |

| File | Target1 | Target2 |
|------|---------|---------|
| R1F1–R1F3 | NM | NM |
| R2F1 `01_CreateTableC.sql` | NM | -- |
| R2F2 `02_InsertDataB.sql` | NM | -- |

**DB State**: Both databases are clean. Target1 had R1+R2F1+R2F2 rolled back. Target2 had R1 rolled back (R2 was never started on Target2).

**Inconsistency**: None.

**Recovery**: Fix `02_InsertDataB.sql`, re-run `migrate-up`.

---

### Category 11: Edge Cases

#### S40 — Nothing to Migrate (Idempotency)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Terminate |
| Error Position | None |

**Run 1**: All succeed. MigrationRunResult = Ok.

**Run 2**: No files to execute (all already Migrated). MigrationRunResult = Ok.

**DB State**: Unchanged between runs. Two `MigrationRun` entries, both Ok (100).

**Inconsistency**: None — this verifies that `migrate-up` is safe to run repeatedly.

#### S43 — Single File Fails with Rollback

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| Error Position | Only file (R1F1) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | NM |

**DB State**: No tables. The single file was rolled back.

**Inconsistency**: None.

**Recovery**: Fix the file, re-run.

#### S44 — Error at Release Boundary + RollbackRelease

| Setting | Value |
|---------|-------|
| MigrationErrorAction | RollbackRelease |
| Error Position | R2F1 (first file of Release 2.0) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | NM |
| R2F2–R3F3 | -- |

**DB State**: R1 fully intact. R2F1 was the only file in R2 that executed; it was rolled back.

**Inconsistency**: None.

**Recovery**: Fix `01_CreateTableC.sql`, re-run `migrate-up`. R1 is skipped, R2–R3 execute.

---

### Category 12: Rollback Chain Breaks

These scenarios test what happens when `RequireRollbackFile = false` and specific rollback files are missing in a full `Rollback` chain, with `StopRollbackOnMissingRollbackFile = true` (the default).

#### S45 — Chain Breaks at End (R1F1 Rollback Missing, Default StopRollbackOnMissingRollbackFile=true)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RequireRollbackFile | false |
| StopRollbackOnMissingRollbackFile | true (default) |
| Error Position | R3F3 (R1F1 has no rollback file) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | M |
| R1F2 `02_CreateTableB.sql` | NM |
| R1F3 `03_InsertDataA.sql` | NM |
| R2F1–R2F3 | NM |
| R3F1–R3F3 | NM |

**DB State**: TableA still exists (R1F1 rollback missing, chain stopped). All other tables dropped.

**Inconsistency**: **Repository + Database inconsistency** — R1F1 remains `Migrated` (status unchanged because the rollback file was missing and `StopRollbackOnMissingRollbackFile=true` stops the chain without updating the record) but TableA still exists.

**Recovery**:
1. Manually drop TableA
2. Create the missing rollback file for R1F1
3. Fix `03_InsertDataD.sql`
4. Re-run `migrate-up`

#### S46 — Chain Breaks in Middle (R2F1 Rollback Missing, Default StopRollbackOnMissingRollbackFile=true)

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback |
| RequireRollbackFile | false |
| StopRollbackOnMissingRollbackFile | true (default) |
| Error Position | R3F3 (R2F1 has no rollback file) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1–R1F3 | M |
| R2F1 `01_CreateTableC.sql` | M |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3 `03_AddColumnA.sql` | NM |
| R3F1–R3F3 | NM |

**DB State**: R1 tables intact (rollback chain stopped at R2F1 — missing rollback file stops the chain with `StopRollbackOnMissingRollbackFile = true`). TableC still exists.

**Inconsistency**: **Repository + Database inconsistency** — R1 files and R2F1 are all `Migrated` (chain stopped at R2F1 without changing its status), and TableC still exists because R2F1's rollback file was missing.

**Recovery**:
1. Manually drop TableC
2. Create the missing rollback file for R2F1
3. Fix `03_InsertDataD.sql`
4. Re-run `migrate-up`

---

### Category 13: Ignore + Rollback Interaction

#### S47 — Ignored File NOT Rolled Back When Later Rollback Triggers

| Setting | Value |
|---------|-------|
| MigrationErrorAction | Rollback (product-level), Ignore (per-file on R1F3) |
| Error Position | R1F3 fails (Ignore), R2F2 fails (Rollback) |
| MigrationRunResult | Error (90) |

| File | Status |
|------|--------|
| R1F1 `01_CreateTableA.sql` | NM |
| R1F2 `02_CreateTableB.sql` | NM |
| R1F3 `03_InsertDataA.sql` | F |
| R2F1 `01_CreateTableC.sql` | NM |
| R2F2 `02_InsertDataB.sql` | NM |
| R2F3–R3F3 | -- |

**Key insight**: R1F3 failed with `Ignore`, so it was NOT added to `successfullyMigratedRecords`. When R2F2 failed with `Rollback`, the rollback chain only rolled back files in `successfullyMigratedRecords` — R1F3 was excluded. R1F3 stays `Failed`.

**Inconsistency**: **Logical inconsistency** — R1F3 is `Failed` while R1F1 and R1F2 are `NotMigrated` (they were rolled back). The database has no R1 tables, but R1F3's data was never successfully applied anyway.

**Recovery**: Fix both `03_InsertDataA.sql` (R1F3) and `02_InsertDataB.sql` (R2F2), re-run `migrate-up`. All `Failed` and `NotMigrated` files are retried.

---

## Summary Decision Matrix

| Inconsistency Type | Auto-Recovery? | Action Required |
|--------------------|----------------|-----------------|
| None (clean Terminate/Rollback) | Yes | Fix the SQL error, re-run `migrate-up` |
| Database (partial data, `UseTransaction=false`) | No | Manually clean up partial data, then re-run |
| Repository (rollback failed, status ≠ DB state) | Partial | May need manual SQL to align DB with desired state, then re-run |
| Logical (Ignore left Failed + Migrated mix) | Yes | Fix the failed file, re-run `migrate-up` |
| Missing rollback files | No | Create the rollback files, manually fix DB if needed, then re-run |
| Orphaned run (stuck `Running`) | Yes | Wait 10 min (auto-fix) or run `fix` command |

---

## General Recovery Procedure

When a migration run fails, follow these steps in order:

1. **Read the logs** — Identify which file failed and why (SQL error, timeout, connection issue, etc.)

2. **Check the `MigrationRunResult`** — Use `info` command or query the repository:
   ```bash
   raymigrator info -p MyProduct -env Production
   ```

3. **Identify the inconsistency type** — Use the scenario matrix above to find your exact scenario and inconsistency type

4. **Check for orphaned runs** — If the process crashed:
   ```bash
   raymigrator fix -p MyProduct -env Production --dry-run
   ```

5. **Fix the root cause** — Correct the SQL error in the migration file

6. **Clean up database state if needed** — For database inconsistencies (partial data from `UseTransaction=false`, failed rollbacks leaving tables):
   ```sql
   -- Example: remove partial data
   DELETE FROM TableB WHERE <condition>;
   -- Example: drop table left by failed rollback
   DROP TABLE IF EXISTS TableC;
   ```

7. **Re-run `migrate-up`** — Failed and NotMigrated files are auto-retried:
   ```bash
   raymigrator migrate-up -p MyProduct -env Production -rm migrate
   ```

8. **Verify** — Check that all files are now `Migrated`:
   ```bash
   raymigrator info -p MyProduct -env Production
   raymigrator validate-hash -p MyProduct -env Production
   ```

9. **Update hashes if needed** — If you modified a previously executed migration file:
   ```bash
   raymigrator update-hash -p MyProduct -env Production
   ```

---

## Recommended Production Configuration

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| `MigrationErrorAction` | `Terminate` or `RollbackRelease` | See guidance below |
| `RollbackErrorAction` | `Terminate` | Stops rollback chain on failure rather than leaving more inconsistencies (cf. S11 vs S10) |
| `RequireRollbackFile` | `true` | Catches missing rollback files before any SQL executes — prevents S25, S27, S45, S46 entirely |
| `StopRollbackOnMissingRollbackFile` | `true` (default) | Prevents the rollback chain from skipping missing rollback files — safer than continuing with an incomplete rollback |
| `UseTransaction` | `true` | Prevents partial data on errors (except MariaDB/MySQL DDL) — prevents S05 |
| `DbCommandMaxRetries` | `3` | Handles transient network/connection errors |
| `DbCommandWaitTimeInMsBeforeRetry` | `500` | Linear backoff: 500ms, 1000ms, 1500ms |
| `TargetMigrationOrder` | `Successively` | Predictable target-by-target execution |

**Choosing `MigrationErrorAction` for production:**

- **`Terminate`** — Safest default. Preserves state for investigation; prevents cascading rollback failures. You can always roll back manually after understanding the failure. Rollback introduces risk: if a rollback file has a bug, the rollback itself fails, leaving the system in a worse state (see S09, S10, S18).

- **`RollbackRelease`** — Ideal for incremental deployments where releases are independent units. Protects previous releases while limiting rollback scope to the failed release. Best when your rollback files are thoroughly tested.

- **`Rollback`** — Use when all releases are strongly interdependent and a partial state makes no sense (e.g., initial schema migration of a new system).

- **`Ignore`** — Only for truly optional, independent files (seed data, permissions, views). **Never** for schema migrations or files in dependency chains (see S19 — logical inconsistency risk).

---

## Recovery Commands Reference

| Command | When to Use |
|---------|-------------|
| `migrate-up` | After fixing a failed migration — retries all Failed/NotMigrated files |
| `migrate-down` | Intentional rollback of Migrated files to a specific release |
| `fix` | Clean up orphaned runs (stuck in Running status) |
| `info` | Check current migration status per file |
| `validate-hash` | Verify file integrity after manual changes |
| `update-hash` | Update repository hashes after intentional file modifications |

---

## Related Documentation

- [Error Handling](error-handling.md) — Error action strategies and configuration hierarchy
- [Migration State Machine](migration-state-machine.md) — State transitions and status definitions
- [Execution Modes](execution-modes.md) — Simultaneously vs Successively mode details
- [Troubleshooting](../appendix/troubleshooting.md) — Common issues and solutions
- [Fix Command Reference](../08-cli-reference/command-reference.md#fix) — Fix command options
