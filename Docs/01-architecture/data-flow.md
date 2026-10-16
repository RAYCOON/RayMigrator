# Data Flow

This document describes how data flows through RayMigrator during migration operations.

## Migration Execution Flow

```mermaid
sequenceDiagram
    participant User
    participant CLI as Console Layer
    participant Bridge as RayMigratorService
    participant Svc as MigrationService
    participant Ctx as MigrationContext
    participant Tmpl as TemplateExecutor
    participant Repo as Repository DB
    participant Target as Target DB

    User->>CLI: raymigrator migrate-up -p X -env Y

    CLI->>CLI: Parse arguments
    CLI->>Bridge: ExecuteMigrateUpAsync(cancellation)

    Bridge->>Bridge: Create MigrateUpRequest (Cancellation = RunCancellation of the process)
    Bridge->>Svc: MigrateUpAsync(request, cancellation.Token)

    Note over Svc: Phase 1 - Initialization
    Svc->>Ctx: Validate request, set MigrationRunResult=Running,<br/>MigrationOperation=MigrateUp
    Svc->>Tmpl: RepositoryCheckCreateAsync()
    Tmpl->>Repo: Schema steps via RepositorySchemaExecutor:<br/>Repository_CheckCreate, missing Repository_Upgrade_n,<br/>Repository_MigratorMeta_CheckInsert
    Repo-->>Tmpl: MigratorMetaId

    Svc->>Tmpl: RepositoryProductCheckInsertAsync()
    Tmpl->>Repo: Ensure product exists
    Repo-->>Tmpl: ProductId

    Svc->>Tmpl: RepositoryEnvironmentCheckInsertAsync()
    Tmpl->>Repo: Ensure environment exists
    Repo-->>Tmpl: EnvironmentId

    Svc->>Tmpl: RepositoryMigrationGetInterruptedAsync(token)
    Tmpl->>Repo: Check for interrupted migrations

    Svc->>Svc: BuildMigrationRunSettingsJson()
    Svc->>Tmpl: RepositoryMigrationRunInsertAsync(settingsJson)
    Tmpl->>Repo: Create migration run with settings snapshot
    Repo-->>Tmpl: MigrationRunId

    Note over Svc: Phase 2 - File Discovery & Preparation
    Svc->>Svc: DiscoverAndPrepareMigrationFiles()
    Svc->>Tmpl: RepositoryMigrationSelectAsync(token)
    Tmpl->>Repo: Query existing records
    Svc->>Svc: FilterAlreadyMigratedFiles()
    Svc->>Svc: FilterByTargetRelease()
    Svc->>Svc: ValidateTargetGroupAliases()
    Svc->>Svc: FilterByTargetGroups()
    Svc->>Svc: DetectOutOfOrderFiles()
    Svc->>Svc: FindIncompleteRollbacks()
    Note over Svc: refuses the run when a file it would run<br/>stopped in the middle of its rollback

    Note over Svc: Phase 3 - Execute Migrations<br/>(a cancel stops at the checkpoint before the next file, target or block)
    loop For each Release
        Svc->>Svc: ResolveTargetGroupMigrationOrder()<br/>(CLI > migsettings > appsettings > config array order)
        loop For each TargetGroup (in resolved order)
            alt TargetMigrationOrder = FileByFile
                Svc->>Svc: ExecuteTargetGroupFileByFile()<br/>(File → Target loop)
            else TargetMigrationOrder = TargetByTarget
                Svc->>Svc: ExecuteTargetGroupTargetByTarget()<br/>(Target → File loop)
            end
            Svc->>Tmpl: RepositoryMigrationInsertAsync()
            Svc->>Target: Execute SQL blocks (with the token of the run)
            Target-->>Svc: Result
            Svc->>Tmpl: RepositoryMigrationUpdateAsync()
        end
    end

    Note over Svc: Phase 5 - Finalization
    Svc->>Tmpl: RepositoryMigrationRunUpdateAsync(Ok/PartialSuccess/Recovered/Error/Cancelled)
    Svc-->>Bridge: MigrationOperationResult
    Bridge-->>CLI: Exit code (0/1/130)
    CLI-->>User: Console output
```

## Console Startup Flow

```mermaid
flowchart TD
    A[CLI Entry: Main] --> B[Parse command-line arguments]
    B --> E[Resolve environment<br/>CLI arg, cross-checked against DOTNET_ENVIRONMENT]

    E --> F["RunDirectMode(JsonOptionsSource)"]
    F --> G["JsonOptionsSource.LoadAsync()<br/>Load appsettings.json hierarchy"]
    G --> H["DirectModePipeline.ExecuteAsync()<br/>DI host, service execution"]
```

## Configuration Loading Flow (Standalone Mode)

```mermaid
flowchart LR
    subgraph "JsonOptionsSource"
        A1[appsettings.json]
        A2["appsettings.{Environment}.json"]
        A3["appsettings.{Product}.json"]
        A4["appsettings.{Product}.{Environment}.json"]
    end

    subgraph "Processing (DirectModePipeline)"
        B1[Configuration Builder]
        B2[Environment Variable Resolver]
        B3[Options Binding + DataAnnotations]
        B4[ProductDefaultsPostConfigureOptions<br/>merges defaults into Products]
    end

    subgraph "Result"
        C1[RayMigratorOptions]
        C2[MigrationContext]
    end

    A1 --> B1
    A2 --> B1
    A3 --> B1
    A4 --> B1
    B1 --> B2
    B2 -- "{ENV:VAR}" --> B2
    B2 --> B3
    B3 --> B4
    B4 --> C1
    C1 --> C2
```

## Template Execution Flow

```mermaid
sequenceDiagram
    participant Svc as Service
    participant Exec as TemplateExecutor
    participant Cache as TemplateCache
    participant DB as Database

    Note over Cache: All templates loaded at startup<br/>{ENV:*} already replaced during init

    Svc->>Exec: RepositoryProductCheckInsertAsync()

    Exec->>Cache: GetRepositoryTemplate(templateType, repositoryOptions)
    Note over Cache: Replace {CFG:*} via<br/>reflection-based property matching
    Cache-->>Exec: Template (with CFG+ENV resolved)

    Exec->>Exec: Build DalParameterList with @Parameters
    Exec->>DB: ExecuteScalarAsync via IDal
    DB-->>Exec: "ResultCode,ResultMessage"

    Exec->>Exec: Parse into TemplateResponse
    Exec-->>Svc: TemplateResponse (ResultCode, ResultMessage)
```

The schema steps behind `RepositoryCheckCreateAsync()` take the same path inside `RepositorySchemaExecutor`, which executes through the static `TemplateExecutor.ExecuteScalarAsync` and needs no migration context; `IRepositorySchemaService` runs them for a host without a run. The retention templates take that path as well: `IRepositoryMaintenanceService` runs them through `RepositoryMaintenanceExecutor` with the repository options alone ([Retention](../03-database-layer/repository-schema.md#retention)).

## Migration File Discovery Flow

```mermaid
flowchart TD
    A[Start: DiscoverAndPrepareMigrationFiles] --> A1["ValidateTargetGroupAliasCasing()<br/>Detect case-mismatched TargetGroup<br/>subdirectory names"]
    A1 --> B[Load migsettings defaults]
    B --> C["Recursive scan of MigrationFilesRootDirectory<br/>for *.{fileExtension} files, sorted by relative path"]
    C --> D{For each file}
    D --> E{Is rollback file?}
    E -->|Yes| D
    E -->|No| F{Is migsettings file?}
    F -->|Yes| D
    F -->|No| G{Is environment-specific<br/>and not for current env?}
    G -->|Yes| D
    G -->|No| H["ParseMigrationFile()<br/>(read content, extract TOML metadata,<br/>apply migsettings defaults,<br/>split SQL blocks, compute hashes)"]
    H --> I{TOML Environments filter<br/>excludes current env?}
    I -->|Yes| D
    I -->|No| J[Add to migration list]
    J --> D

    D -->|All files processed| K{RequireRollbackFile<br/>validation}
    K -->|Missing rollback files| L[Throw MigrationFileParsingException]
    K -->|OK| N{Single TargetGroup product?}
    N -->|Yes| O["ValidateFlatLayoutAmbiguity()<br/>Reject mixed flat/traditional<br/>layout within same release"]
    N -->|No| M[Return ordered migration files]
    O --> M
```

## Hash Validation Flow

```mermaid
flowchart TD
    A[Load Migration File] --> B{Hash Scope?}

    B -->|File| C[Calculate SHA-256 of entire file]
    B -->|SqlBlocks| D[Calculate SHA-256 of SQL blocks only]
    B -->|Disabled| E[Skip validation]

    C --> F[Compare with stored hash]
    D --> G[Compare with stored blocks hash]

    F --> H{Match?}
    G --> H

    H -->|Yes| I[Proceed with migration]
    H -->|No| J[Reject - unauthorized modification]

    E --> I
```

## Error Handling Flow

```mermaid
flowchart TD
    A[Migration Error] --> B{MigrationErrorAction?}

    B -->|Terminate| C[Stop immediately]
    B -->|Rollback| D[Get all migrations in current run]
    B -->|RollbackErrorOnly| E[Get failed migration only]
    B -->|RollbackRelease| R[Get migrations from failed release only]
    B -->|Ignore| T[Skip failed blocks, mark file as Failed,<br/>continue with next file]

    C --> F[Update MigrationRunResult to Error]
    D --> G[Execute rollback scripts in reverse]
    E --> H[Execute single rollback script]
    R --> S[Execute rollback scripts for release in reverse]

    G --> F
    H --> F
    S --> F
    T --> U[Continue migration run]

    F --> I[Return error result]
```

A cancelled migrate-up takes the same rollback chains with its `MigrationCancelledAction` (`RollbackFile`, `RollbackRun` and `RollbackRelease` like `RollbackErrorOnly`, `Rollback` and `RollbackRelease`; `Terminate` rolls nothing back) and records `Cancelled`, or `Error` when that rollback did not complete cleanly. See [Cancellation](../02-core-concepts/error-handling.md#cancellation).

## Request/Response Pattern

### MigrateUpRequest
```json
{
  "productAlias": "RayMigratorTests",
  "environment": "Docker",
  "runMode": "Migrate",
  "targetReleaseVersion": "Release 1.2",
  "showInfo": true,
  "revealSensitiveData": false,
  "allowOutOfOrder": false,
  "targetGroupAliases": null,
  "targetGroupMigrationOrder": null
}
```

### MigrationOperationResult
```json
{
  "success": true,
  "runId": "guid",
  "productAlias": "RayMigratorTests",
  "environment": "Docker",
  "operation": "MigrateUp",
  "result": "Ok",
  "totalMigrations": 5,
  "successfulMigrations": 5,
  "failedMigrations": 0,
  "currentRelease": "Release 1.2",
  "migrationResults": [
    {
      "fileName": "001_CreateTable.sql",
      "releaseVersion": "Release 1.0",
      "targetGroup": "Backend",
      "success": true,
      "errorMessage": null,
      "executedAt": "2026-02-20T12:00:00Z",
      "duration": "00:00:01.234"
    }
  ],
  "errorMessage": null,
  "errorCode": null,
  "messages": [
    "Successfully executed 5 migration(s)"
  ],
  "executedAt": "2026-02-20T12:00:00Z",
  "duration": "00:00:05.678"
}
```

## MigrationContext State Transitions

`MigrationState` carries two persisted state enums: `MigrationStatus` (per migration record, `MigrationRecord.MigrationStatusId`) and `MigrationRunResult` (per run, `MigrationRun.MigrationRunResultId`). Both are written by `MigrationService` through `TemplateExecutor`.

### MigrationStatus (per record)

```mermaid
stateDiagram-v2
    [*] --> Pending: RepositoryMigrationInsertAsync()

    Pending --> Executing: First SQL block or CLI tool starts - RepositoryMigrationUpdateAsync(Executing)
    Executing --> Executing: Next block - RepositoryMigrationUpdateAsync(Executing, blocksMigrated)
    Executing --> Migrated: All blocks executed - RepositoryMigrationUpdateAsync(Migrated)
    Executing --> Failed: Block error, exception or cancel - RepositoryMigrationUpdateAsync(Failed)

    Migrated --> Executing: Rollback starts (migrate-down, error recovery or cancel action) - RepositoryMigrationUpdateRollbackAsync(Executing)
    Failed --> Executing: Rollback of the failed file (error recovery, cancel action or migrate-down) - RepositoryMigrationUpdateRollbackAsync(Executing)
    Executing --> NotMigrated: All rollback blocks executed - RepositoryMigrationUpdateRollbackAsync(NotMigrated)
    Executing --> Failed: Rollback block error, or a cancel before the next rollback block - RepositoryMigrationUpdateRollbackAsync(Failed)

    Executing --> Migrated: Interrupted record with all blocks done - TryFinalizeCompletedMigrationAsync()
    Pending --> NotMigrated: fix (orphaned run) - RepositoryMigrationRecordFixOrphanedAsync(assumed status)
    Executing --> NotMigrated: fix (orphaned run) - RepositoryMigrationRecordFixOrphanedAsync(assumed status)
```

`fix` writes the status of `--last-migration-status` (`NotMigrated` or `Migrated`) to every `Pending`/`Executing` record of an orphaned run; the auto-fix in `RepositoryMigrationRunInsertWithAutoFix()` always uses `NotMigrated`. `Undefined` (0) is never persisted. A cancelled file keeps the blocks that committed before the cancel (none on the atomic path), so the next migrate-up resumes it like a failed file; under `MigrationErrorAction=Ignore` the record stores the block count of the file instead, and the next migrate-up runs the whole file again.

### MigrationRunResult (per run)

```mermaid
stateDiagram-v2
    [*] --> Running: RepositoryMigrationRunInsertAsync()

    Running --> Ok: All files migrated / rolled back, or nothing to do
    Running --> PartialSuccess: migrate-up continued past a Failed file (MigrationErrorAction=Ignore), or migrate-down skipped a missing rollback file / ignored a failed rollback block
    Running --> Cancelled: the run was cancelled and stopped at the next block or file boundary, and the rollback of its MigrationCancelledAction (if any) completed cleanly
    Running --> Recovered: migrate-up failed and the error-recovery rollback (Rollback / RollbackRelease / RollbackErrorOnly) completed without failure or warning
    Running --> Error: migrate-up aborted (Terminate, or recovery not clean, also when a cancel stopped it), the rollback of a cancelled migrate-up did not complete cleanly, migrate-down rollback failed, unhandled exception, or fix closes an orphaned run

    Ok --> [*]
    PartialSuccess --> [*]
    Cancelled --> [*]
    Recovered --> [*]
    Error --> [*]
```

Every terminal value is written by `RepositoryMigrationRunUpdateAsync()`; `fix` and the auto-fix close an orphaned run with `RepositoryMigrationRunFixOrphanedAsync()` (result `Error`). A process that dies, or that a second Ctrl+C or SIGTERM ends at once, leaves its run `Running` until such a repair. See [Migration State Machine](../02-core-concepts/migration-state-machine.md) for the per-command flows.

## Related Documentation

- [Overview](overview.md) - Architecture overview
- [Component Responsibilities](component-responsibilities.md) - Component details
- [Patterns](patterns.md) - Implementation patterns
- [Execution Modes](../02-core-concepts/execution-modes.md) - Operating modes, migration order, run modes
- [Error Handling](../02-core-concepts/error-handling.md) - MigrationErrorAction and RollbackErrorAction strategies
- [Migration Service](../04-service-layer/migration-service.md) - Service implementation details
