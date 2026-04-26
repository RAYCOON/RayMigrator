# Command Activity Diagrams

Activity diagrams for the main migration commands. All flows are implemented in `MigrationService.cs` within the Services project.

## Migrate-Up

### Main Flow

```mermaid
flowchart TD
    Start([MigrateUpAsync]) --> P1["<b>Phase 1: Initialization</b><br/>MigrationRunResult = Running<br/>MigrationOperation = MigrateUp"]
    P1 --> ShouldWrite{RunMode = Migrate?}

    ShouldWrite -->|Yes| RepoInit["RepositoryCheckCreate()<br/>RepositoryProductCheckInsert()<br/>RepositoryEnvironmentCheckInsert()<br/>RepositoryMigrationGetInterrupted()<br/>BuildMigrationRunSettingsJson()<br/>RepositoryMigrationRunInsertWithAutoFix()"]
    ShouldWrite -->|No| P2
    RepoInit --> P2

    P2["<b>Phase 2: File Discovery</b><br/>DiscoverAndPrepareMigrationFiles()"] --> FilesExist{Files found?}
    FilesExist -->|No| EarlyOk1["RepositoryMigrationRunUpdate(Ok)<br/>Return Success"]

    FilesExist -->|Yes| ShouldRead{RunMode ≥ Simulate?}
    ShouldRead -->|Yes| QueryRepo["RepositoryMigrationSelect()<br/>FilterAlreadyMigratedFiles()"]
    ShouldRead -->|No/Validate| UseAll["Use all discovered files"]
    QueryRepo --> ApplyFilters
    UseAll --> ApplyFilters

    ApplyFilters["FilterByTargetRelease()<br/>ValidateTargetGroupAliases()<br/>FilterByTargetGroups()"] --> DetectOOO["DetectOutOfOrderFiles()"]
    DetectOOO --> HasOOO{Out-of-order?}
    HasOOO -->|Yes, no flag| ThrowOOO(["Throw: use --allow-out-of-order"])
    HasOOO -->|Yes, flag set| WarnOOO["Log warning, continue"]
    HasOOO -->|No| FilesRemain
    WarnOOO --> FilesRemain

    FilesRemain{Files remaining?}
    FilesRemain -->|No| EarlyOk2["RepositoryMigrationRunUpdate(Ok)<br/>Return Success"]
    FilesRemain -->|Yes| SafetyWarn["LogMigrationSafetyWarnings()"]

    SafetyWarn --> P3["<b>Phase 3: Execute Migrations</b>"]
    P3 --> ReleaseLoop{"foreach Release"}

    ReleaseLoop --> TGOrder["ResolveTargetGroupMigrationOrder()<br/>(CLI &gt; migsettings &gt; appsettings &gt; config order)"]
    TGOrder --> TGLoop{"foreach TargetGroup<br/>(resolved order)"}
    TGLoop --> HasFiles{Files for<br/>Release + TG?}
    HasFiles -->|No| TGLoop
    HasFiles -->|Yes| OrderCheck{TargetMigrationOrder?}

    OrderCheck -->|Simultaneously| ExecSimul["ExecuteTargetGroupSimultaneously()<br/>Loop: File → Target"]
    OrderCheck -->|Successively| ExecSucc["ExecuteTargetGroupSuccessively()<br/>Loop: Target → File"]

    ExecSimul --> TGResult{result.Success?}
    ExecSucc --> TGResult

    TGResult -->|Yes| AccumOk["Accumulate counts"]
    AccumOk --> TGLoop

    TGResult -->|No| HandleErr["HandleMigrationError()<br/>RepositoryMigrationRunUpdate(Error)"]
    HandleErr --> ReturnErr["Return Result = Error"]

    TGLoop -->|exhausted| ReleaseLoop
    ReleaseLoop -->|exhausted| P5

    P5["<b>Phase 5: Finalization</b>"] --> FinalCheck{failedMigrations > 0?}
    FinalCheck -->|Yes| FinalErr["RepositoryMigrationRunUpdate(Error)"]
    FinalCheck -->|No| FinalOk["RepositoryMigrationRunUpdate(Ok)"]

    style Start fill:#e1f5fe
    style EarlyOk1 fill:#c8e6c9
    style EarlyOk2 fill:#c8e6c9
    style FinalOk fill:#c8e6c9
    style ThrowOOO fill:#ffcdd2
    style ReturnErr fill:#ffcdd2
    style FinalErr fill:#ffcdd2
```

### TargetGroup Execution (Simultaneously Mode)

In Simultaneously mode, the outer loop is files, the inner loop is targets. Each file is applied to all targets before moving to the next file.

```mermaid
flowchart TD
    Start([ExecuteTargetGroupSimultaneously]) --> FileLoop{"foreach File"}

    FileLoop --> ResolveErr["Resolve MigrationErrorAction:<br/>file TOML override ?? product level"]
    ResolveErr --> TargetLoop{"foreach Target"}

    TargetLoop --> TryFinalize["TryFinalizeCompletedMigration()"]
    TryFinalize --> WasFinalized{Already<br/>complete?}
    WasFinalized -->|Yes| AddRecovered["Add to successfullyMigratedRecords"]
    AddRecovered --> TargetLoop
    WasFinalized -->|No| InsertMig["RepositoryMigrationInsert()"]

    InsertMig --> CliCheck{UseCliToolAlias?}
    CliCheck -->|Yes| ExecCli["ExecuteWithCliTool()"]
    CliCheck -->|No| ExecSql["ExecuteSqlBlocks()"]
    ExecCli --> BlockResult
    ExecSql --> BlockResult

    BlockResult{Failed blocks?}
    BlockResult -->|No| MarkOk["Update record → Migrated<br/>Add to successfullyMigratedRecords"]
    MarkOk --> TargetLoop

    BlockResult -->|Yes| MarkFailed["Update record → Failed<br/>Break target loop"]
    MarkFailed --> FailCount["result.FailCount++"]
    FailCount --> FileLoop

    TargetLoop -->|exhausted| FileOk["result.SuccessCount++"]
    FileOk --> FileLoop

    FileLoop -->|exhausted| ReturnOk(["Return result"])

    subgraph Exception Path
        CatchEx["Update record → Failed"] --> IsIgnore{Ignore mode?}
        IsIgnore -->|Yes| IgnoreEx["FailCount++, continue"]
        IsIgnore -->|No| AbortTG["result.Success = false<br/>Return immediately"]
    end

    style ReturnOk fill:#c8e6c9
    style AbortTG fill:#ffcdd2
```

### Error Handling (HandleMigrationError)

When a TargetGroup execution fails (non-Ignore), this method determines the rollback scope based on the resolved `MigrationErrorAction`.

```mermaid
flowchart TD
    Start([HandleMigrationError]) --> Switch{MigrationErrorAction?}

    Switch -->|Terminate| LogTerminate["Log CRITICAL<br/>No rollback<br/>Database in unclear state"]

    Switch -->|Ignore| LogIgnore["Log debug<br/>No rollback<br/>Continue execution"]

    Switch -->|RollbackErrorOnly| BuildOne["Rollback list:<br/>failed file only"]
    BuildOne --> ExecRB

    Switch -->|RollbackRelease| BuildRelease["Rollback list:<br/>failed file +<br/>all files from same Release<br/>(reverse order)"]
    BuildRelease --> ExecRB

    Switch -->|Rollback| BuildAll["Rollback list:<br/>failed file +<br/>ALL successfully migrated files<br/>(reverse order)"]
    BuildAll --> ExecRB

    ExecRB["ExecuteRollbackForMigrations()"]

    style LogTerminate fill:#fff3e0
    style LogIgnore fill:#fff3e0
    style ExecRB fill:#e1f5fe
```

### Rollback Execution (ExecuteRollbackForMigrations)

Shared by both Migrate-Up (error recovery) and Migrate-Down (explicit rollback). Processes records in the provided order and is fail-fast by default.

```mermaid
flowchart TD
    Start([ExecuteRollbackForMigrations]) --> RecordLoop{"foreach Record"}

    RecordLoop --> FindFile["Locate rollback file"]
    FindFile --> FileExists{Rollback file<br/>exists?}

    FileExists -->|No| ReqCheck{RequireRollbackFile?}
    ReqCheck -->|true| AbortMissing["Mark Failed<br/>ABORT rollback chain"]
    ReqCheck -->|false| ErrorRecovery{isErrorRecovery?}
    ErrorRecovery -->|Yes| StopCheck{StopRollbackOnMissingRollbackFile?<br/>CLI &gt; TargetGroup &gt; Product<br/>default: true}
    StopCheck -->|true| StopChain["Log warning<br/>STOP rollback chain<br/>(no status change)"]
    StopCheck -->|false| WarnSkip["Log warning<br/>Continue to next record<br/>(no status change)"]
    ErrorRecovery -->|No - MigrateDown| WarnSkip
    WarnSkip --> RecordLoop

    FileExists -->|Yes| ParseFile["ParseMigrationFile()"]
    ParseFile --> UpdateMeta["RepositoryMigrationUpdateRollback()<br/>status = Executing"]
    UpdateMeta --> ShouldExec{RunMode = Migrate?}

    ShouldExec -->|No| SkipExec["Skip SQL execution"]
    ShouldExec -->|Yes| CliCheck{UseCliToolAlias?}
    CliCheck -->|Yes| ExecCli["ExecuteWithCliTool()"]
    CliCheck -->|No| BlockLoop{"foreach SQL Block"}

    ExecCli --> CliOk{Success?}
    CliOk -->|Yes| SkipExec
    CliOk -->|No| BlockError["fileHadBlockError = true"]
    BlockError --> SkipExec

    BlockLoop --> ExecBlock["ExecuteNonQueryAsync()"]
    ExecBlock --> BlockOk["Update rollback progress"]
    BlockOk --> BlockLoop

    ExecBlock -.->|Exception| RbAction{RollbackErrorAction?}
    RbAction -->|Terminate| AbortBlock["Mark Failed<br/>ABORT rollback chain"]
    RbAction -->|Ignore| IgnoreBlock["Log warning, continue"]
    IgnoreBlock --> BlockLoop

    BlockLoop -->|exhausted| SkipExec
    SkipExec --> HadErrors{Block errors?}
    HadErrors -->|Yes| MarkFailed["Update record → Failed"]
    HadErrors -->|No| MarkNotMigrated["Update record → NotMigrated"]
    MarkFailed --> RecordLoop
    MarkNotMigrated --> RecordLoop

    RecordLoop -->|exhausted| ReturnResult(["Return RollbackResult"])

    style AbortMissing fill:#ffcdd2
    style StopChain fill:#fff3e0
    style AbortBlock fill:#ffcdd2
    style ReturnResult fill:#c8e6c9
```

---

## Migrate-Down

Migrate-Down rolls back all migrations after the target release. It reuses `ExecuteRollbackForMigrations` (see [above](#rollback-execution-executerollbackformigrations)).

```mermaid
flowchart TD
    Start([MigrateDownAsync]) --> ValidateMode{RunMode = Validate?}

    ValidateMode -->|Yes| ValBranch["Discover files<br/>FilterReleasesAfterTarget()<br/>FilterByTargetGroups()"]
    ValBranch --> ValLoop{"foreach File"}
    ValLoop --> CheckRb{Rollback file<br/>exists?}
    CheckRb -->|No| ValWarn["Add warning"]
    CheckRb -->|Yes| ValParse["Parse and validate"]
    ValWarn --> ValLoop
    ValParse --> ValLoop
    ValLoop -->|done| ValReturn(["Return validation result"])

    ValidateMode -->|No| P1["<b>Phase 1: Initialization</b><br/>RepositoryCheckCreate()<br/>RepositoryProductCheckInsert()<br/>RepositoryEnvironmentCheckInsert()<br/>MigrationRunResult = Running<br/>MigrationOperation = MigrateDown<br/>RepositoryMigrationRunInsertWithAutoFix()"]

    P1 --> P2["<b>Phase 2: Query Repository</b><br/>RepositoryMigrationSelect()"]
    P2 --> FilterRollback["Filter records:<br/>Status = Migrated or partial<br/>Release > target release<br/>Match --target-group filter<br/>Order by FileOrderId DESC"]

    FilterRollback --> HasRecords{Records to<br/>rollback?}
    HasRecords -->|No| EarlyOk["RepositoryMigrationRunUpdate(Ok)<br/>Already at target release"]

    HasRecords -->|Yes| P3["<b>Phase 3: Execute Rollbacks</b><br/>ExecuteRollbackForMigrations()"]

    P3 --> FinalCheck{All successful?}
    FinalCheck -->|Yes| FinalOk["RepositoryMigrationRunUpdate(Ok)"]
    FinalCheck -->|No| FinalErr["RepositoryMigrationRunUpdate(Error)"]

    style Start fill:#e1f5fe
    style ValReturn fill:#c8e6c9
    style EarlyOk fill:#c8e6c9
    style FinalOk fill:#c8e6c9
    style FinalErr fill:#ffcdd2
```

**Key difference from Migrate-Up**: Migrate-Down operates on a flat list of repository records (ordered by FileOrderId descending), not the nested Release → TargetGroup → Target loop structure.

---

## Baseline

Baseline marks migration files as applied in the repository **without executing any SQL**. Uses the same Release → TargetGroup loop as Migrate-Up but skips all SQL execution.

```mermaid
flowchart TD
    Start([BaselineAsync]) --> P1["<b>Phase 1: Initialization</b><br/>RepositoryCheckCreate()<br/>RepositoryProductCheckInsert()<br/>RepositoryEnvironmentCheckInsert()<br/>MigrationRunResult = Running<br/>MigrationOperation = MigrateUp<br/>RepositoryMigrationRunInsertWithAutoFix()"]

    P1 --> P2["<b>Phase 2: File Discovery</b><br/>DiscoverAndPrepareMigrationFiles()"]
    P2 --> FilesExist{Files found?}
    FilesExist -->|No| EarlyOk1["RepositoryMigrationRunUpdate(Ok)"]

    FilesExist -->|Yes| P3["<b>Phase 3: Filter</b><br/>FilterByTargetRelease()<br/>ValidateTargetGroupAliases()<br/>FilterByTargetGroups()"]
    P3 --> FilteredExist{Files after<br/>filter?}
    FilteredExist -->|No| EarlyOk2["RepositoryMigrationRunUpdate(Ok)"]

    FilteredExist -->|Yes| QueryRepo["RepositoryMigrationSelect()<br/>FilterAlreadyMigratedFiles()"]
    QueryRepo --> StillRemain{Files after<br/>dedup?}
    StillRemain -->|No| EarlyOk3["RepositoryMigrationRunUpdate(Ok)"]

    StillRemain -->|Yes| P4["<b>Phase 4: Record as Migrated</b>"]

    P4 --> ReleaseLoop{"foreach Release"}
    ReleaseLoop --> TGOrder["ResolveTargetGroupMigrationOrder()<br/>(CLI &gt; migsettings &gt; appsettings &gt; config order)"]
    TGOrder --> TGLoop{"foreach TargetGroup<br/>(resolved order)"}
    TGLoop --> HasFiles{Files for<br/>Release + TG?}
    HasFiles -->|No| TGLoop

    HasFiles -->|Yes| OrderCheck{TargetMigrationOrder?}
    OrderCheck -->|Simultaneously| SimulOrder["File → Target order"]
    OrderCheck -->|Successively| SuccOrder["Target → File order"]

    SimulOrder --> BaselineFile
    SuccOrder --> BaselineFile

    BaselineFile["<b>BaselineFile()</b><br/>1. RepositoryMigrationInsert()<br/>2. RepositoryMigrationUpdate(<br/>   status = Migrated,<br/>   blocks = allBlocks)<br/><i>No SQL execution</i>"]

    BaselineFile --> MorePairs{More pairs?}
    MorePairs -->|Yes| BaselineFile
    MorePairs -->|No| TGLoop

    TGLoop -->|exhausted| ReleaseLoop
    ReleaseLoop -->|exhausted| P5

    P5["<b>Phase 5: Finalization</b><br/>RepositoryMigrationRunUpdate(Ok)"] --> ReturnOk(["Return BaselineResult"])

    style Start fill:#e1f5fe
    style EarlyOk1 fill:#c8e6c9
    style EarlyOk2 fill:#c8e6c9
    style EarlyOk3 fill:#c8e6c9
    style ReturnOk fill:#c8e6c9
```

**Note**: Baseline records `MigrationOperation = MigrateUp` in the repository, making baselined files indistinguishable from executed ones at the record level.

---

## Related Documentation

- [migration-service.md](migration-service.md) — Method signatures and phase descriptions
- [block-execution.md](block-execution.md) — SQL block parsing and execution details
- [file-discovery.md](file-discovery.md) — Migration file scanning and filtering
- [../02-core-concepts/error-handling.md](../02-core-concepts/error-handling.md) — MigrationErrorAction and RollbackErrorAction enums
- [../02-core-concepts/execution-modes.md](../02-core-concepts/execution-modes.md) — TargetMigrationOrder and RunMode details
