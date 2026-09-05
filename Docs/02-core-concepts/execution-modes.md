# Execution Modes

RayMigrator supports different execution modes that control how migrations are applied to target databases. This covers three orthogonal dimensions:

1. **Operating Mode** (`OperatingMode` enum) — How the CLI connects to infrastructure (Standalone, ManagedLocal, or ManagedRemote)
2. **Target Migration Order** — How files and targets are iterated within a TargetGroup (Simultaneously or Successively)
3. **Run Mode** (`MigrationRunMode` enum) — Whether SQL is actually executed (Validate, Simulate, or Migrate)

## Operating Mode

The `OperatingMode` enum defines three possible modes for RayMigrator:

| Mode | Description |
|------|-------------|
| **Standalone** | All configuration (products, targets, repository, etc.) loaded from `appsettings.json` files. This is the default and currently the only active mode. |
| **ManagedLocal** | Configuration loaded from a local Admin-DB. Products, environments, targets, and repository config come from the Admin-DB; Serilog config still from `appsettings.json`. |
| **ManagedRemote** | CLI operates as a thin client, sending HTTP requests to a remote RayMigrator API server instead of accessing databases directly. |

Source: `Raycoon.RayMigrator.Core/Configuration/Enums/OperatingMode.cs`

> **Implementation Status**: The Engine's `Program.Main` only wires up **Standalone** mode — it always creates a `JsonOptionsSource` and calls `RunDirectMode()`. The `ManagedLocal` and `ManagedRemote` enum values and supporting types (`AdminDbOptions`, `RayMigratorBootstrapOptions`) are consumed by **RayMigrator Studio** (a separate repository) for its Admin-DB and API hosting. These types are part of Engine's Core NuGet contract.

## Release-Based Execution Order

Migrations are executed **release by release**. Within each release, TargetGroups are processed in their configuration order. Within each TargetGroup, the `TargetMigrationOrder` setting controls the inner file/target iteration.

**Outer loop (always)**:
```
Release 1.0 → Release 1.1 → Release 1.2 → ...
```

**Within each release**:
```
TargetGroup 1 → TargetGroup 2 → ...
```

By default, TargetGroups are processed in their configuration array order. This order can be overridden via:

1. **CLI**: `--TargetGroup-MigrationOrder "Frontend, Backend"` / `-tgmo` (highest priority)
2. **migsettings TOML** (release-level): `TargetGroupMigrationOrder = ["Frontend", "Backend"]`
3. **appsettings JSON** (product-level): `"TargetGroupMigrationOrder": "Frontend, Backend"`

The override applies to `Migrate-Up` and `Baseline`. `Migrate-Down` always derives order from repository records and is not affected. See [Product Options — TargetGroupMigrationOrder](../06-configuration-reference/product-options.md#targetgroupmigrationorder) for validation rules and the full configuration reference.

**Within each TargetGroup** (controlled by `TargetMigrationOrder`):
- **Simultaneously**: file → target
- **Successively**: target → file

### Example: 2 Releases, 2 TargetGroups

```
Release 1.0:
  Backend (Simultaneously):  File1→T1, File1→T2, File2→T1, File2→T2
  Frontend (Successively):   T1→File3, T1→File4, T2→File3, T2→File4
Release 1.1:
  Backend (Simultaneously):  File5→T1, File5→T2
  Frontend (Successively):   T1→File6, T2→File6
```

This ensures that all TargetGroups complete a release before any TargetGroup starts the next release.

## Target Migration Order

The `TargetMigrationOrder` setting controls the sequence of execution across multiple targets within a TargetGroup for a given release.

### Enum Values

| Value | Name | Description |
|-------|------|-------------|
| 0 | Undefined | Not set |
| 1 | Simultaneously | Execute on all targets per migration |
| 2 | Successively | Complete all migrations per target |

### Simultaneously

Execute each migration on all targets before moving to the next migration.

```mermaid
sequenceDiagram
    participant M1 as Migration 1
    participant M2 as Migration 2
    participant T1 as Target 1
    participant T2 as Target 2

    M1->>T1: Execute
    M1->>T2: Execute
    M2->>T1: Execute
    M2->>T2: Execute
```

**Execution Order**:
1. Migration 1 → Target 1
2. Migration 1 → Target 2
3. Migration 2 → Target 1
4. Migration 2 → Target 2

**Configuration**:
```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "TargetMigrationOrder": "Simultaneously"
  }]
}
```

**Use Cases**:
- Tightly coupled databases
- Sharded databases requiring consistent schema
- Replicated database systems

**Advantages**:
- All targets stay in sync
- Easier to reason about state
- Schema consistency guaranteed

**Disadvantages**:
- Error affects all targets
- All-or-nothing for each migration
- Can't partially succeed

**Complete Example** (based on the RayMigratorTests product):

The test product has 2 TargetGroups: **Backend** (`Simultaneously`, 2 targets) and **Frontend** (`Successively`, 1 target). Here is the full execution order across all 4 releases:

```
Release 1.0:
  Backend (Simultaneously — file → target):
    10_CreateDataModel.sql       → Backend1
    10_CreateDataModel.sql       → Backend2
    20_InsertMasterData.sql      → Backend1
    20_InsertMasterData.sql      → Backend2
  Frontend (Successively — target → file):
    00_CreateDataModel.sql       → Frontend

Release 1.1:
  Backend (Simultaneously):
    01_InsertDynamicData.sql     → Backend1
    01_InsertDynamicData.sql     → Backend2
  Frontend (Successively):
    01_InsertDynamicData.sql     → Frontend

Release 1.2:
  Backend (Simultaneously):
    00_AddSexOther.sql           → Backend1
    00_AddSexOther.sql           → Backend2
    01_AddLoginPersonOther.sql   → Backend1
    01_AddLoginPersonOther.sql   → Backend2
  Frontend (Successively):
    01_AddUserProfileAndUserPreferences.sql → Frontend

Release 1.3:
  Backend (Simultaneously):
    01_AddAlexLee2.sql           → Backend1
    01_AddAlexLee2.sql           → Backend2
  Frontend (Successively):
    01_AddAlexLee2ProfileAndUserPreferences-Error.sql → Frontend  ← ERROR
```

Key observations:
- The **outer loop** is always Release → TargetGroup (config order). Backend completes before Frontend starts within each release.
- **Backend** (Simultaneously): The inner loop is file → target. Each file is applied to all targets before the next file. Backend1 and Backend2 always have the same schema state.
- **Frontend** has only 1 target, so `Simultaneously` vs `Successively` makes no difference here (see [Single Target Note](#single-target-note) below).

### Successively (Default)

Complete all migrations on one target before moving to the next target.

```mermaid
sequenceDiagram
    participant M1 as Migration 1
    participant M2 as Migration 2
    participant T1 as Target 1
    participant T2 as Target 2

    M1->>T1: Execute
    M2->>T1: Execute
    Note over T1: All done
    M1->>T2: Execute
    M2->>T2: Execute
    Note over T2: All done
```

**Execution Order**:
1. Migration 1 → Target 1
2. Migration 2 → Target 1
3. Migration 1 → Target 2
4. Migration 2 → Target 2

**Configuration**:
```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "TargetMigrationOrder": "Successively"
  }]
}
```

**Use Cases**:
- Independent databases
- Blue/green deployments
- Staged rollouts

**Advantages**:
- Better for independent systems
- Each target gets all its migrations before next target starts

**Disadvantages**:
- Temporary inconsistency between targets
- Targets may be at different schema levels during execution

**Complete Example** (same product, but hypothetical: Frontend gets a 2nd target):

To make the difference between both modes visible, imagine the Frontend TargetGroup also has 2 targets (Frontend1, Frontend2). Now both TargetGroups have multiple targets but use different `TargetMigrationOrder` settings:

```
Release 1.2:
  Backend (Simultaneously — file → target):
    00_AddSexOther.sql           → Backend1
    00_AddSexOther.sql           → Backend2
    01_AddLoginPersonOther.sql   → Backend1
    01_AddLoginPersonOther.sql   → Backend2
  Frontend (Successively — target → file):
    01_AddUserProfileAndUserPreferences.sql → Frontend1
    01_AddUserProfileAndUserPreferences.sql → Frontend2
```

With only 1 file per TargetGroup per release, the difference is not visible. Release 1.0 has 2 Backend files — here the contrast becomes clear:

```
Release 1.0:
  Backend (Simultaneously — file → target):
    10_CreateDataModel.sql       → Backend1
    10_CreateDataModel.sql       → Backend2
    20_InsertMasterData.sql      → Backend1
    20_InsertMasterData.sql      → Backend2
  Frontend (Successively — target → file):
    00_CreateDataModel.sql       → Frontend1
    00_CreateDataModel.sql       → Frontend2
```

If Frontend also had 2 files and 2 targets, the difference would be:

```
Simultaneously (file → target):  FileA→T1, FileA→T2, FileB→T1, FileB→T2
Successively   (target → file):  T1→FileA, T1→FileB, T2→FileA, T2→FileB
```

The inner loop is **target → file**: one target receives all migration files for the release before the next target starts.

### Single Target Note

> **When a TargetGroup has only 1 target**, the `TargetMigrationOrder` setting has no effect on execution order. Both `Simultaneously` and `Successively` produce the same result because the inner loop (file→target or target→file) only has one element on one side. For example, the Frontend TargetGroup in the test product has a single target — switching it between `Simultaneously` and `Successively` would not change anything.

## Run Mode

The `MigrationRunMode` enum controls whether changes are actually applied. Available modes: `Migrate`, `Simulate`, `Validate` (enum values: `Undefined=0`, `Validate=10`, `Simulate=20`, `Migrate=100`).

The behavior of each mode is determined by extension methods on `MigrationRunMode` (defined in `MigrationRunModeExtensions`):

| Extension Method | Validate | Simulate | Migrate |
|-----------------|----------|----------|---------|
| `ShouldExecuteSql()` | false | false | **true** |
| `ShouldWriteRepository()` | false | false | **true** |
| `ShouldReadRepository()` | false | **true** | **true** |

### Migrate (Default)

Execute actual database changes.

```bash
raymigrator Migrate-Up --product MyProduct --environment Production
# or explicitly:
raymigrator Migrate-Up --product MyProduct --environment Production --run-mode Migrate
```

### Simulate

Validate and process everything, connect to target databases for connectivity validation, but do not execute SQL on target databases.

```bash
raymigrator Migrate-Up --product MyProduct --environment Production --run-mode Simulate
```

**Simulate Mode**:
- Parses all migration files
- Validates TOML metadata
- Checks environment/target filters
- Calculates hashes
- Connects to target databases (validates connectivity)
- Reads repository records to determine what is already migrated (same as Migrate mode)
- Does NOT write repository records
- Does NOT execute SQL on targets
- Does NOT write database log entries (DatabaseLogging sink is inactive)

**Use Cases**:
- Dry run before production deployment
- CI/CD pipeline validation
- Testing migration order

### Validate

Validate configuration, migration files, and rollback files without any database operations.

```bash
raymigrator Migrate-Up --product MyProduct --environment Production --run-mode Validate
raymigrator Migrate-Down --product MyProduct --environment Production --to-release "Release 1.0" --run-mode Validate
```

**Validate Mode**:
- Parses all migration files (TOML metadata, SQL blocks)
- Checks environment/target filters
- Calculates file hashes
- For Migrate-Down: validates rollback file existence and parseability
- Does NOT connect to target databases
- Does NOT connect to repository database
- Does NOT execute SQL
- Does NOT create repository records
- Processes ALL files regardless of prior migration status

**Use Cases**:
- CI/CD pre-deployment checks (no database access needed)
- Validating migration file syntax on developer machines
- Checking rollback file completeness
- Environments where database access is not available

> **Note**: The `Validate-Hash` command is a separate command that validates file hashes against repository records. The `--run-mode Validate` option for `Migrate-Up`/`Migrate-Down` is a different feature that validates file structure without any database access.

## Execution Flow Comparison

### Simultaneously + Migrate

```mermaid
flowchart TD
    A[Start Migration Run] --> B[Load Migration 1]
    B --> C[Execute on Target 1]
    C --> D[Execute on Target 2]
    D --> E[Load Migration 2]
    E --> F[Execute on Target 1]
    F --> G[Execute on Target 2]
    G --> H[Complete]

    C -->|Error| I[Handle Error]
    D -->|Error| I
    F -->|Error| I
    G -->|Error| I
```

### Successively + Migrate

```mermaid
flowchart TD
    A[Start Migration Run] --> B[Target 1]
    B --> C[Execute Migration 1]
    C --> D[Execute Migration 2]
    D --> E[Target 1 Complete]

    E --> F[Target 2]
    F --> G[Execute Migration 1]
    G --> H[Execute Migration 2]
    H --> I[Target 2 Complete]

    I --> J[All Complete]

    C -->|Error| K[Handle Error]
    D -->|Error| K
    G -->|Error| K
    H -->|Error| K
```

> **Note**: Both Simultaneously and Successively modes abort the TargetGroup and the entire migration run on error, **unless** `MigrationErrorAction` is `Ignore`. With `Ignore`, the failed file is marked as `Failed` and execution continues with the next file. For all other error actions (Terminate, Rollback, RollbackErrorOnly, RollbackRelease), the TargetGroup is aborted immediately and the caller handles rollback or termination.

## Target Group Scope

Migration order is configured per **TargetGroup**, not globally:

```json
{
  "Products": [{
    "Alias": "MyProduct",
    "TargetGroups": [
      {
        "Alias": "MainDatabases",
        "TargetMigrationOrder": "Simultaneously",
        "Targets": [
          { "Alias": "Primary" },
          { "Alias": "Secondary" }
        ]
      },
      {
        "Alias": "AnalyticsDatabases",
        "Targets": [
          { "Alias": "Reporting" },
          { "Alias": "Warehouse" }
        ]
      }
    ]
  }]
}
```

In this example:
- Main databases migrate simultaneously (stay in sync)
- Analytics databases migrate successively (independent)

## Best Practices

### Use Simultaneously When:
- Databases must be schema-consistent
- Application queries span multiple databases
- Sharded or replicated systems
- Schema changes have cross-database dependencies

### Use Successively When:
- Databases are independent
- Risk mitigation is priority
- Blue/green deployment patterns
- Staged rollout is desired

### Use Simulate When:
- Before production deployments
- Testing migration sequences
- CI/CD validation
- Documentation generation

## Out-of-Order Migration

### Overview

By default, RayMigrator executes migration files strictly in order — based on release directory sorting and filename sorting within each release. Any migration file that was added after a later migration has already been executed will be skipped or flagged as a conflict.

**Out-of-Order Migration** relaxes this constraint: it allows executing migration files that were added between already-executed migrations, without treating this as an error.

### Use Case

In teams with multiple developers working on different features in parallel, migration files may be merged into the main branch in a different order than their filename/release sequence:

```
Timeline:
  Dev A creates: Release 2.0/Backend/003_AddIndex.sql
  Dev B creates: Release 2.0/Backend/002_AddColumn.sql  (merged later)

Repository state after Dev A's migration:
  001_CreateTable.sql  → Migrated
  003_AddIndex.sql     → Migrated
  002_AddColumn.sql    → Not yet migrated (added later, out of order)
```

Without out-of-order support, `002_AddColumn.sql` would be skipped or cause a validation error because it precedes an already-executed migration (`003_AddIndex.sql`).

With out-of-order support enabled, RayMigrator would detect `002_AddColumn.sql` as a pending migration and execute it normally.

### Configuration

Out-of-Order Migration is controlled via the CLI parameter:

```bash
# One-time opt-in for a specific migration run
raymigrator Migrate-Up -p MyProduct -env Production -rm Migrate --allow-out-of-order
```

| Parameter | Short | Default | Description |
|-----------|-------|---------|-------------|
| `--allow-out-of-order` | `-ooo` | `false` | Allow execution of migration files that precede already-executed migrations |

This is a deliberate, per-run decision — not a permanent setting.

**Typical workflow:**

1. Normal run detects gap: "Migration 002 precedes already-executed 003"
2. Developer/DBA reviews the situation
3. Re-run with `--allow-out-of-order` — explicit, one-time approval

### Behavior

| Scenario | Out-of-Order disabled | Out-of-Order enabled |
|----------|------------------------|----------------------|
| New file after all executed | Execute normally | Execute normally |
| New file before executed files | Skip or error | Execute as pending |
| New file between executed files | Skip or error | Execute as pending |

### Considerations

- **Hash validation**: Out-of-order files still undergo hash validation if enabled
- **Dependencies**: Files executed out of order may reference objects created by later-numbered files — developers must ensure correctness
- **Audit trail**: Repository records should clearly indicate that a migration was executed out of order
- **Rollback**: Rolling back to a release that was partially executed out of order requires careful handling

## Implementation Details

The execution order logic is implemented in `MigrationService.cs` (Services project) using the following internal types and methods:

### TargetGroupExecutionResult

Internal result type returned by both `ExecuteTargetGroupSimultaneously` and `ExecuteTargetGroupSuccessively`:

```csharp
internal class TargetGroupExecutionResult
{
    public bool Success { get; set; } = true;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public MigrationFileInfo? FailedFile { get; set; }
    public int FailedMigrationRecordId { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### ExecuteTargetGroupSimultaneously

Executes migrations in file-then-target order (`foreach file -> foreach target`). On error with `MigrationErrorAction.Ignore`, marks the file as `Failed` and continues to the next file. For all other error actions, aborts the TargetGroup immediately and returns the result to the caller for rollback handling.

### ExecuteTargetGroupSuccessively

Executes migrations in target-then-file order (`foreach target -> foreach file`). Error handling is identical to `ExecuteTargetGroupSimultaneously`.

### MigrateUpAsync Phase 3

The main migration loop in `MigrateUpAsync` dispatches to the appropriate method per TargetGroup:

```
foreach release in orderedReleases:
    orderedTargetGroups = resolve from: CLI --TargetGroup-MigrationOrder
                                      → release migsettings TargetGroupMigrationOrder
                                      → product appsettings TargetGroupMigrationOrder
                                      → productOptions.TargetGroups (config array order)
    foreach targetGroup in orderedTargetGroups:
        if targetGroup.TargetMigrationOrder == Simultaneously:
            ExecuteTargetGroupSimultaneously(...)
        else:
            ExecuteTargetGroupSuccessively(...)
        if result failed → HandleMigrationError → abort entire run
```

### BaselineAsync Phase 4

`BaselineAsync` uses the same Release -> TargetGroup -> TargetMigrationOrder dispatch pattern (including the same `TargetGroupMigrationOrder` resolution) but without executing SQL. It records each file as `Migrated` in the repository using a local `BaselineFile()` function:

```
foreach release in orderedReleases:
    orderedTargetGroups = resolve from: CLI → release migsettings → appsettings → config order
    foreach targetGroup in orderedTargetGroups:
        if Simultaneously: foreach file -> foreach target -> BaselineFile()
        else:              foreach target -> foreach file -> BaselineFile()
```

### Static Helpers (for Unit Testing)

Two pure static helper methods expose the execution order logic without requiring `TemplateExecutor` or database access:

- **`GetExecutionOrder(files, targetGroup)`** — Returns a `List<(int FileOrderId, string TargetAlias)>` representing the inner file/target iteration order for a single TargetGroup. Uses `Simultaneously` (file -> target) when `TargetMigrationOrderEnum == TargetMigrationOrder.Simultaneously`, otherwise falls back to `Successively` (target -> file), including for `Undefined`.

- **`GetFullExecutionOrder(files, targetGroups, targetGroupMigrationOrder?)`** — Returns a `List<(int FileOrderId, string TargetGroupAlias, string TargetAlias)>` representing the complete execution order across all releases and TargetGroups. Iterates Release -> TargetGroup (config order or explicit order when `targetGroupMigrationOrder` is provided) -> delegates to `GetExecutionOrder()` for the inner loop.

## Related Documentation

- [Error Handling](error-handling.md) - How errors are handled in each mode
- [Migration State Machine](migration-state-machine.md) - State transitions
- [Target Group Options](../06-configuration-reference/target-group-options.md) - TargetMigrationOrder configuration
- [Migration Service](../04-service-layer/migration-service.md) - Implementation details for ExecuteTargetGroupSimultaneously/Successively
- [Product Options](../06-configuration-reference/product-options.md) - MigrationErrorAction configuration
